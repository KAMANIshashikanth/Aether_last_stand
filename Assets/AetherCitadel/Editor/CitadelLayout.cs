using System.Collections.Generic;
using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>
    /// Lays the whole fortress town out: two rings of curtain wall with bastions,
    /// a round-towered keep on the east flank, a dense grid of flat-roofed houses,
    /// ruined outskirts on every side and an open dune field beyond.
    /// </summary>
    public static class CitadelLayout
    {
        // Footprint traced from the reference: an irregular polygon roughly 190 x 150 m.
        static readonly Vector2[] Ring =
        {
            new Vector2(-92f, -58f), new Vector2(-58f, -80f), new Vector2(  6f, -84f),
            new Vector2( 62f, -70f), new Vector2( 95f, -34f), new Vector2( 98f,  16f),
            new Vector2( 66f,  58f), new Vector2(  4f,  70f), new Vector2(-56f,  52f),
            new Vector2(-95f,   6f)
        };

        // The five great round towers of the keep: x, z, radius, height.
        static readonly Vector4[] KeepTowers =
        {
            new Vector4(44f, -22f,  9.5f, 30f),
            new Vector4(66f, -24f, 10.0f, 33f),
            new Vector4(46f,   2f,  9.5f, 29f),
            new Vector4(68f,   0f, 10.5f, 34f),
            new Vector4(86f, -12f,  8.5f, 27f)
        };

        static readonly Rect KeepArea = new Rect(28f, -40f, 74f, 60f); // x, z, w, h

        /// <summary>Radius of the wind-scoured pan the city stands on. Dunes build up outside it.</summary>
        public const float FlatRadius = 128f;
        public const float TerrainSize = 900f;

        public class Chunk
        {
            public string name;
            public MeshBuilder mb = new MeshBuilder();
        }

        public class Result
        {
            public List<Chunk> chunks = new List<Chunk>();
            public int houses, towers, ruins;
        }

        public static Result Build(int seed, bool includeGround)
        {
            var rng = new System.Random(seed);
            var res = new Result();
            Vector2 duneOff = Pieces.DuneOffset(seed);

            System.Func<Vector3, float> groundY = p =>
                Pieces.DuneHeight(p.x + duneOff.x, p.z + duneOff.y, FlatRadius, p.x, p.z);

            var outer = Outward(new List<Vector3>(ToV3(Ring)));
            var inner = Inset(outer, 16f);
            var buildable = Inset(inner, 5.5f);
            var outsetWall = Inset(outer, -12f);

            var curtain = WallSpec.Curtain();
            var innerSpec = WallSpec.Inner();

            // ---- desert floor -------------------------------------------------
            if (includeGround)
            {
                var terrain = New(res, "Terrain");
                Pieces.Dunes(terrain.mb, TerrainSize, 180, FlatRadius, seed);

                var detail = New(res, "Desert_Detail");
                var farOutset = Inset(outer, -20f);
                for (int i = 0; i < 300; i++)
                {
                    float a = Rng.F(rng, 0f, Mathf.PI * 2f);
                    float r = Rng.F(rng, 90f, 420f);
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (Inside(farOutset, p)) continue;
                    p.y = groundY(p) - 0.1f;
                    Pieces.Rock(detail.mb, p, Rng.F(rng, 0.4f, 2.6f), Rng.F(rng, 0.25f, 1.5f), rng);
                }
                // loose sand blown into low mounds across the pan
                for (int i = 0; i < 90; i++)
                {
                    float a = Rng.F(rng, 0f, Mathf.PI * 2f);
                    float r = Rng.F(rng, 105f, 320f);
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (Inside(farOutset, p)) continue;
                    p.y = groundY(p) - 0.2f;
                    Pieces.SandMound(detail.mb, p, Rng.F(rng, 2.5f, 9f), Rng.F(rng, 0.5f, 2.2f), rng);
                }
            }

            // ---- outer defences -------------------------------------------
            var outerChunk = New(res, "Wall_Outer");
            int gateSlot;
            var slots = TowerSlots(outer, 30f, out gateSlot);

            for (int i = 0; i < slots.Count; i++)
            {
                var a = slots[i].pos;
                var b = slots[(i + 1) % slots.Count].pos;
                if (i == gateSlot)
                    Pieces.GateWall(outerChunk.mb, a, b, curtain, 6.5f, 5.5f);
                else
                    Pieces.Wall(outerChunk.mb, a, b, curtain);
            }
            foreach (var s in slots)
            {
                float size = s.corner ? 11f : 9f;
                float h = s.corner ? Rng.F(rng, 15f, 17.5f) : Rng.F(rng, 13f, 15f);
                Pieces.SquareTower(outerChunk.mb, s.pos, s.yaw, size, h, curtain);
                res.towers++;
                if (Rng.Chance(rng, 0.35f))
                    Pieces.Turret(outerChunk.mb, new Vector3(s.pos.x, h + curtain.parapetH + curtain.merlonH, s.pos.z),
                                  2.1f, 4.2f, curtain);
            }

            // sand banked against the windward face of the curtain
            var drifts = New(res, "Sand_Drifts");
            for (int i = 0; i < slots.Count; i++)
            {
                var a = slots[i].pos;
                var b = slots[(i + 1) % slots.Count].pos;
                Vector3 dir = (b - a).normalized;
                Vector3 outward = Vector3.Cross(Vector3.up, dir);
                // the wind runs from the south-west, so it piles up on the faces turned into it
                float exposure = Mathf.Clamp01(Vector3.Dot(outward, new Vector3(-0.72f, 0f, -0.69f)));
                if (exposure < 0.12f) continue;
                Pieces.WallDrift(drifts.mb, a + outward * 2.1f, b + outward * 2.1f,
                                 Mathf.Lerp(2f, 5.5f, exposure), Mathf.Lerp(0.8f, 2.6f, exposure), rng);
            }

            // ---- inner ring -------------------------------------------------
            var innerChunk = New(res, "Wall_Inner");
            int dummy;
            var innerSlots = TowerSlots(inner, 34f, out dummy);
            for (int i = 0; i < innerSlots.Count; i++)
            {
                var a = innerSlots[i].pos;
                var b = innerSlots[(i + 1) % innerSlots.Count].pos;
                if (i == 1) Pieces.GateWall(innerChunk.mb, a, b, innerSpec, 5.5f, 4.2f);
                else Pieces.Wall(innerChunk.mb, a, b, innerSpec);
            }
            foreach (var s in innerSlots)
            {
                Pieces.SquareTower(innerChunk.mb, s.pos, s.yaw, 7f, Rng.F(rng, 9.5f, 11f), innerSpec);
                res.towers++;
            }

            // ramps up to the wall walk, on the inside face
            for (int i = 0; i < innerSlots.Count; i += 3)
            {
                var a = innerSlots[i].pos;
                var b = innerSlots[(i + 1) % innerSlots.Count].pos;
                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = (b - a).normalized;
                Vector3 outward = Vector3.Cross(Vector3.up, dir);
                float rise = innerSpec.height, run = rise * 1.7f;
                Vector3 pos = mid - outward * (innerSpec.thickness * 0.5f + run * 0.5f);
                float yaw = Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg;
                Pieces.Stairs(innerChunk.mb, pos, yaw, 3.2f, rise, run);
            }

            // ---- the keep ---------------------------------------------------
            var keep = New(res, "Keep");
            var compound = WallSpec.Compound();
            compound.height = 8f;
            compound.thickness = 2.2f;

            var keepRing = new List<Vector3>
            {
                new Vector3(31f, 0f, -37f), new Vector3(78f, 0f, -39f), new Vector3(97f, 0f, -16f),
                new Vector3(93f, 0f, 11f),  new Vector3(58f, 0f, 17f),  new Vector3(33f, 0f, 8f)
            };
            keepRing = Outward(keepRing);
            for (int i = 0; i < keepRing.Count; i++)
            {
                var a = keepRing[i];
                var b = keepRing[(i + 1) % keepRing.Count];
                if (i == 5) Pieces.GateWall(keep.mb, a, b, compound, 5f, 4f);
                else Pieces.Wall(keep.mb, a, b, compound);
            }

            foreach (var t in KeepTowers)
            {
                Pieces.RoundTower(keep.mb, new Vector3(t.x, 0f, t.y), t.z, t.w, curtain, 20);
                res.towers++;
            }
            LinkTowers(keep.mb, KeepTowers, curtain);
            Pieces.House(keep.mb, new Vector3(58f, 0f, -10f), 12f, new Vector2(15f, 11f), 2, HouseStyle.Flat, rng, Mat.PlasterPale);

            // ---- town -------------------------------------------------------
            var reserved = new List<Rect>
            {
                RectAt(-30f, 10f, 20f, 16f),   // great hall
                RectAt(-5f, -35f, 17f, 14f),   // second hall
                RectAt(-24f, 2f, 6f, 6f),      // minaret
                RectAt(-2f, -42f, 6f, 6f),
                KeepArea
            };

            var roads = new List<Vector3[]>
            {
                Poly(new Vector3(-26f, 0f, -74f), new Vector3(-22f, 0f, -30f), new Vector3(-4f, 0f, -10f), new Vector3(30f, 0f, -6f)),
                Poly(new Vector3(-72f, 0f, 4f), new Vector3(-20f, 0f, 10f), new Vector3(28f, 0f, 6f)),
                Poly(new Vector3(-46f, 0f, -46f), new Vector3(-36f, 0f, 6f), new Vector3(-30f, 0f, 44f)),
                Poly(new Vector3(6f, 0f, -62f), new Vector3(10f, 0f, -14f), new Vector3(16f, 0f, 40f))
            };

            var townChunks = new Chunk[4];
            for (int i = 0; i < 4; i++) townChunks[i] = New(res, "Town_" + i);

            const float cell = 8.6f;
            var bounds = Bounds2(buildable);
            for (float x = bounds.xMin; x < bounds.xMax; x += cell)
                for (float z = bounds.yMin; z < bounds.yMax; z += cell)
                {
                    Vector3 p = new Vector3(x + Rng.F(rng, -1.1f, 1.1f), 0f, z + Rng.F(rng, -1.1f, 1.1f));
                    if (!Inside(buildable, p)) continue;
                    if (InAny(reserved, p, 3f)) continue;
                    if (NearRoad(roads, p, 3.4f)) continue;

                    var size = new Vector2(Rng.F(rng, 5.2f, 8.0f), Rng.F(rng, 4.8f, 7.2f));
                    int stories = Rng.Chance(rng, 0.42f) ? 1 : (Rng.Chance(rng, 0.72f) ? 2 : 3);
                    HouseStyle style = HouseStyle.Flat;
                    double roll = rng.NextDouble();
                    if (roll > 0.90) style = HouseStyle.Pitched;
                    else if (roll > 0.80) style = HouseStyle.Tower;

                    int q = (p.x < 0f ? 0 : 1) + (p.z < 0f ? 0 : 2);
                    Pieces.House(townChunks[q].mb, p, Rng.F(rng, -12f, 12f), size, stories, style, rng, TownMat(rng));
                    res.houses++;
                }

            var landmarks = New(res, "Landmarks");
            Pieces.DomedHall(landmarks.mb, new Vector3(-30f, 0f, 10f), 6f, new Vector2(17f, 13f), 9.5f, 5.2f, rng, Mat.PlasterPale);
            Pieces.DomedHall(landmarks.mb, new Vector3(-5f, 0f, -35f), -8f, new Vector2(14f, 11f), 8f, 4.2f, rng, Mat.Plaster);
            Pieces.Minaret(landmarks.mb, new Vector3(-24f, 0f, 2f), 1.7f, 19f);
            Pieces.Minaret(landmarks.mb, new Vector3(-2f, 0f, -42f), 1.45f, 15f);

            // ---- outskirts, all the way round --------------------------------
            var suburb = New(res, "Outskirts");
            var hug = Inset(outer, -7.5f);

            Vector3 gateA = slots[gateSlot].pos, gateB = slots[(gateSlot + 1) % slots.Count].pos;
            Vector3 gateMid = (gateA + gateB) * 0.5f;
            Vector3 gateOut = Vector3.Cross(Vector3.up, (gateB - gateA).normalized);

            // a continuous belt of dwellings pressed right up against the curtain,
            // the way real suburbs grow against a town wall
            const int beltSteps = 115;
            for (int i = 0; i < beltSteps; i++)
            {
                float t = (i + Rng.F(rng, -0.35f, 0.35f)) / beltSteps;
                Vector3 wallPt, outward;
                PointOnRing(outer, t, out wallPt, out outward);
                Vector3 along = Vector3.Cross(Vector3.up, outward);

                int here = Rng.Chance(rng, 0.32f) ? 2 : 1;
                for (int k = 0; k < here; k++)
                {
                    Vector3 p = wallPt + outward * Rng.F(rng, 9f, 30f) + along * Rng.F(rng, -5f, 5f);
                    if (Inside(hug, p)) continue;
                    if (Vector3.Distance(p, gateMid) < 20f) continue;   // keep the gate road clear
                    p.y = groundY(p);
                    Outbuilding(suburb.mb, p, rng, res, 0.26f, groundY);
                }
                if (Rng.Chance(rng, 0.35f))
                {
                    Vector3 p = wallPt + outward * Rng.F(rng, 6f, 26f) + along * Rng.F(rng, -6f, 6f);
                    if (!Inside(hug, p))
                    {
                        p.y = groundY(p) - 0.2f;
                        Pieces.SandMound(suburb.mb, p, Rng.F(rng, 1.8f, 5f), Rng.F(rng, 0.4f, 1.4f), rng);
                    }
                }
            }

            // looser hamlets further out
            const int clusters = 18;
            for (int c = 0; c < clusters; c++)
            {
                float t = (c + Rng.F(rng, -0.28f, 0.28f)) / clusters;
                Vector3 anchorOnWall, outward;
                PointOnRing(outer, t, out anchorOnWall, out outward);
                Vector3 anchor = anchorOnWall + outward * Rng.F(rng, 34f, 62f);

                int count = Rng.I(rng, 5, 12);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = anchor + new Vector3(Rng.F(rng, -18f, 18f), 0f, Rng.F(rng, -18f, 18f));
                    if (Inside(outsetWall, p)) continue;
                    p.y = groundY(p);
                    Outbuilding(suburb.mb, p, rng, res, 0.40f, groundY);
                }

                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = anchor + new Vector3(Rng.F(rng, -24f, 24f), 0f, Rng.F(rng, -24f, 24f));
                    if (Inside(outsetWall, p)) continue;
                    p.y = groundY(p) - 0.2f;
                    Pieces.SandMound(suburb.mb, p, Rng.F(rng, 2f, 6f), Rng.F(rng, 0.5f, 1.6f), rng);
                }
            }

            // caravan camp pitched off the main gate
            Vector3 camp = gateMid + gateOut * 30f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = camp + new Vector3(Rng.F(rng, -13f, 13f), 0f, Rng.F(rng, -11f, 11f));
                p.y = groundY(p);
                Pieces.Tent(suburb.mb, p, Rng.F(rng, 0f, 360f), Rng.F(rng, 3.5f, 6.5f), Rng.F(rng, 4f, 8f), Rng.F(rng, 2.2f, 3.4f));
            }

            // abandoned holdings swallowed by the dunes
            var lost = New(res, "Lost_Ruins");
            for (int i = 0; i < 14; i++)
            {
                float a = Rng.F(rng, 0f, Mathf.PI * 2f);
                float r = Rng.F(rng, 175f, 330f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                p.y = groundY(p) - Rng.F(rng, 0.3f, 1.6f);   // sunk into the sand
                Pieces.Ruin(lost.mb, p, Rng.F(rng, 0f, 360f),
                            new Vector2(Rng.F(rng, 5f, 11f), Rng.F(rng, 4.5f, 9f)),
                            Rng.F(rng, 1.2f, 3.8f), rng, RuinMat(rng));
                res.ruins++;
                int mounds = Rng.I(rng, 2, 6);
                for (int m = 0; m < mounds; m++)
                {
                    Vector3 q = p + new Vector3(Rng.F(rng, -7f, 7f), 0f, Rng.F(rng, -7f, 7f));
                    q.y = groundY(q) - 0.2f;
                    Pieces.SandMound(lost.mb, q, Rng.F(rng, 2.5f, 6f), Rng.F(rng, 0.8f, 2.4f), rng);
                }
            }

            return res;
        }

        /// <summary>One dwelling outside the wall: house, ruined shell, tent or rubble.</summary>
        static void Outbuilding(MeshBuilder mb, Vector3 p, System.Random rng, Result res,
                                float ruinChance, System.Func<Vector3, float> groundY)
        {
            float yaw = Rng.F(rng, 0f, 360f);
            double roll = rng.NextDouble();

            if (roll < ruinChance)
            {
                Pieces.Ruin(mb, p, yaw, new Vector2(Rng.F(rng, 4.5f, 8f), Rng.F(rng, 4f, 7f)),
                            Rng.F(rng, 1.6f, 4.5f), rng, RuinMat(rng));
                res.ruins++;
                if (Rng.Chance(rng, 0.5f))
                {
                    Vector3 q = p + new Vector3(Rng.F(rng, -4f, 4f), 0f, Rng.F(rng, -4f, 4f));
                    q.y = groundY(q) - 0.2f;
                    Pieces.SandMound(mb, q, Rng.F(rng, 2.5f, 5f), Rng.F(rng, 0.7f, 1.8f), rng);
                }
            }
            else if (roll < ruinChance + 0.10f)
            {
                Pieces.Tent(mb, p, yaw, Rng.F(rng, 3.5f, 6f), Rng.F(rng, 4f, 7f), Rng.F(rng, 2.2f, 3.2f));
            }
            else if (roll < ruinChance + 0.16f)
            {
                Pieces.RubblePile(mb, p, Rng.F(rng, 2f, 4.5f), Rng.I(rng, 5, 14), rng, RuinMat(rng));
            }
            else
            {
                var size = new Vector2(Rng.F(rng, 4.5f, 8f), Rng.F(rng, 4f, 7f));
                int stories = Rng.Chance(rng, 0.55f) ? 1 : (Rng.Chance(rng, 0.8f) ? 2 : 3);
                HouseStyle style = Rng.Chance(rng, 0.38f) ? HouseStyle.Pitched : HouseStyle.Flat;
                Pieces.House(mb, p, yaw, size, stories, style, rng, SuburbMat(rng));
                res.houses++;
            }
        }

        static Mat TownMat(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.40) return Mat.Plaster;
            if (r < 0.68) return Mat.PlasterWarm;
            if (r < 0.86) return Mat.PlasterPale;
            return Mat.MudBrick;
        }

        static Mat SuburbMat(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.45) return Mat.MudBrick;
            if (r < 0.75) return Mat.PlasterWarm;
            return Mat.Plaster;
        }

        static Mat RuinMat(System.Random rng)
        {
            return Rng.Chance(rng, 0.7f) ? Mat.MudBrick : Mat.StoneBlock;
        }

        static void LinkTowers(MeshBuilder mb, Vector4[] towers, WallSpec spec)
        {
            var link = WallSpec.Compound();
            link.height = 13f;
            link.thickness = 2.6f;
            link.batterH = 4f;
            link.slits = true;

            for (int i = 0; i < towers.Length; i++)
                for (int j = i + 1; j < towers.Length; j++)
                {
                    Vector3 a = new Vector3(towers[i].x, 0f, towers[i].y);
                    Vector3 b = new Vector3(towers[j].x, 0f, towers[j].y);
                    float d = Vector3.Distance(a, b);
                    float gap = d - towers[i].z - towers[j].z;
                    if (gap < 0.5f || gap > 9f) continue;

                    Vector3 dir = (b - a).normalized;
                    Vector3 p0 = a + dir * (towers[i].z - 0.6f);
                    Vector3 p1 = b - dir * (towers[j].z - 0.6f);
                    Pieces.Wall(mb, p0, p1, link);
                }
        }

        // ------------------------------------------------------------------
        //  polygon helpers
        // ------------------------------------------------------------------

        public struct Slot
        {
            public Vector3 pos;
            public float yaw;
            public bool corner;
        }

        static Chunk New(Result r, string name)
        {
            var c = new Chunk { name = name };
            r.chunks.Add(c);
            return c;
        }

        static Vector3[] ToV3(Vector2[] pts)
        {
            var o = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = new Vector3(pts[i].x, 0f, pts[i].y);
            return o;
        }

        static Vector3[] Poly(params Vector3[] pts) { return pts; }

        static Rect RectAt(float cx, float cz, float w, float h)
        {
            return new Rect(cx - w * 0.5f, cz - h * 0.5f, w, h);
        }

        /// <summary>Rewinds a ring so that the right-hand side of travel is the outside.</summary>
        public static List<Vector3> Outward(List<Vector3> ring)
        {
            float a = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                var p = ring[i];
                var q = ring[(i + 1) % ring.Count];
                a += p.x * q.z - q.x * p.z;
            }
            if (a < 0f) ring.Reverse();
            return ring;
        }

        /// <summary>Offsets a ring inwards along vertex bisectors. Negative distance offsets outwards.</summary>
        public static List<Vector3> Inset(List<Vector3> ring, float d)
        {
            var outp = new List<Vector3>(ring.Count);
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = ring[(i - 1 + n) % n], cur = ring[i], next = ring[(i + 1) % n];
                Vector3 n1 = -Vector3.Cross(Vector3.up, (cur - prev).normalized);
                Vector3 n2 = -Vector3.Cross(Vector3.up, (next - cur).normalized);
                Vector3 bis = (n1 + n2).normalized;
                float cos = Mathf.Max(0.35f, Vector3.Dot(bis, n1));
                outp.Add(cur + bis * (d / cos));
            }
            return outp;
        }

        /// <summary>Point at normalised distance t around a ring, with its outward normal.</summary>
        public static void PointOnRing(List<Vector3> ring, float t, out Vector3 pos, out Vector3 outward)
        {
            int n = ring.Count;
            var lens = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                lens[i] = Vector3.Distance(ring[i], ring[(i + 1) % n]);
                total += lens[i];
            }
            float target = Mathf.Repeat(t, 1f) * total;
            for (int i = 0; i < n; i++)
            {
                if (target <= lens[i] || i == n - 1)
                {
                    Vector3 a = ring[i], b = ring[(i + 1) % n];
                    float f = lens[i] > 0.001f ? Mathf.Clamp01(target / lens[i]) : 0f;
                    pos = Vector3.Lerp(a, b, f);
                    outward = Vector3.Cross(Vector3.up, (b - a).normalized);
                    return;
                }
                target -= lens[i];
            }
            pos = ring[0];
            outward = Vector3.forward;
        }

        /// <summary>Tower positions: every vertex, plus subdivisions of long edges.</summary>
        public static List<Slot> TowerSlots(List<Vector3> ring, float maxSpacing, out int gateSlot)
        {
            var slots = new List<Slot>();
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = ring[(i - 1 + n) % n], cur = ring[i], next = ring[(i + 1) % n];
                Vector3 dPrev = (cur - prev).normalized, dNext = (next - cur).normalized;
                Vector3 outward = (Vector3.Cross(Vector3.up, dPrev) + Vector3.Cross(Vector3.up, dNext)).normalized;
                slots.Add(new Slot { pos = cur, yaw = YawOut(outward), corner = true });

                float len = Vector3.Distance(cur, next);
                int div = Mathf.FloorToInt(len / maxSpacing);
                Vector3 outN = Vector3.Cross(Vector3.up, dNext);
                for (int k = 1; k <= div; k++)
                {
                    float t = (float)k / (div + 1);
                    slots.Add(new Slot { pos = Vector3.Lerp(cur, next, t), yaw = YawOut(outN), corner = false });
                }
            }

            // main gate: the slot on the long south edge closest to x = -26
            gateSlot = 0;
            float best = float.MaxValue;
            for (int i = 0; i < slots.Count; i++)
            {
                Vector3 mid = (slots[i].pos + slots[(i + 1) % slots.Count].pos) * 0.5f;
                if (mid.z > -50f) continue;
                float dist = Mathf.Abs(mid.x + 26f);
                if (dist < best) { best = dist; gateSlot = i; }
            }
            return slots;
        }

        static float YawOut(Vector3 outward)
        {
            return Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
        }

        public static bool Inside(List<Vector3> poly, Vector3 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if ((poly[i].z > p.z) != (poly[j].z > p.z) &&
                    p.x < (poly[j].x - poly[i].x) * (p.z - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Rect Bounds2(List<Vector3> poly)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in poly)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        static bool InAny(List<Rect> rects, Vector3 p, float pad)
        {
            foreach (var r in rects)
            {
                if (p.x > r.xMin - pad && p.x < r.xMax + pad && p.z > r.yMin - pad && p.z < r.yMax + pad)
                    return true;
            }
            return false;
        }

        static bool NearRoad(List<Vector3[]> roads, Vector3 p, float halfWidth)
        {
            foreach (var road in roads)
                for (int i = 0; i < road.Length - 1; i++)
                    if (DistToSegment(p, road[i], road[i + 1]) < halfWidth) return true;
            return false;
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
