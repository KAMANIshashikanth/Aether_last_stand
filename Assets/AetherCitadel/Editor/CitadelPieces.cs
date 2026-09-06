using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>Shared tuning for the whole kit. Units are metres.</summary>
    public static class CK
    {
        /// <summary>Texture tiles per metre. 0.25 = one tile every 4 m.</summary>
        public const float D = 0.25f;

        /// <summary>How far foundations sink below y = 0 so nothing floats on uneven ground.</summary>
        public const float Sink = 0.6f;
    }

    public struct WallSpec
    {
        public float height;      // walkway level
        public float thickness;
        public float batterH;     // height of the sloped stone base
        public float batterOut;   // how much wider the base is, per side
        public float parapetH;
        public float parapetT;
        public float merlonW;
        public float merlonGap;
        public float merlonH;
        public bool innerParapet;
        public bool slits;        // arrow slits on the outer face

        public static WallSpec Curtain()
        {
            return new WallSpec
            {
                height = 11f,
                thickness = 3.2f,
                batterH = 4.5f,
                batterOut = 0.9f,
                parapetH = 1.0f,
                parapetT = 0.5f,
                merlonW = 0.95f,
                merlonGap = 0.8f,
                merlonH = 0.75f,
                innerParapet = true,
                slits = true
            };
        }

        public static WallSpec Inner()
        {
            var s = Curtain();
            s.height = 7.5f;
            s.thickness = 2.4f;
            s.batterH = 3.0f;
            s.batterOut = 0.6f;
            s.merlonW = 0.8f;
            s.merlonGap = 0.65f;
            s.merlonH = 0.6f;
            s.innerParapet = false;
            return s;
        }

        public static WallSpec Compound()
        {
            var s = Inner();
            s.height = 4.5f;
            s.thickness = 1.4f;
            s.batterH = 1.6f;
            s.batterOut = 0.3f;
            s.merlonH = 0.45f;
            s.merlonW = 0.6f;
            s.merlonGap = 0.5f;
            s.slits = false;
            return s;
        }
    }

    public enum HouseStyle { Flat, Pitched, Tower, Shed }

    /// <summary>
    /// The kit of parts. Every piece is authored in a local frame with its
    /// footprint centred on the origin and its base at y = 0, except walls,
    /// which run along +X from x = 0 with their outer face towards -Z.
    /// </summary>
    public static class Pieces
    {
        // =====================================================================
        //  WALLS
        // =====================================================================

        /// <summary>
        /// Straight curtain wall from a to b (XZ centreline, y ignored).
        /// The outer face is on the right-hand side of a -> b.
        /// </summary>
        public static void Wall(MeshBuilder mb, Vector3 a, Vector3 b, WallSpec s, bool capStart = false, bool capEnd = false)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.05f) return;
            d /= len;

            mb.Push(new Vector3(a.x, 0f, a.z), YawFor(d));
            WallLocal(mb, len, s, capStart, capEnd);
            mb.Pop();
        }

        /// <summary>Gate section: same profile as a curtain wall, pierced by an arched opening.</summary>
        public static void GateWall(MeshBuilder mb, Vector3 a, Vector3 b, WallSpec s, float openW, float openH)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.05f) return;
            d /= len;

            mb.Push(new Vector3(a.x, 0f, a.z), YawFor(d));

            float xc = len * 0.5f;
            float r = openW * 0.5f;
            float xL = xc - r, xR = xc + r;

            WallSpan(mb, 0f, xL, -CK.Sink, s.height, s, true);
            WallSpan(mb, xR, len, -CK.Sink, s.height, s, true);

            // Stepped voussoirs form the arch soffit.
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float t0 = (float)i / steps, t1 = (float)(i + 1) / steps;
                float x0 = xL + openW * t0, x1 = xL + openW * t1;
                float xm = (x0 + x1) * 0.5f;
                float dx = Mathf.Clamp((xm - xc) / r, -1f, 1f);
                float y = openH + Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx)) * r;
                WallSpan(mb, x0, x1, y, s.height, s, false);
            }

            WallCrown(mb, len, s);

            // Dark timber gate leaves filling the opening.
            mb.Use(Mat.Wood);
            float gateH = openH + r * 0.92f;
            mb.Box(new Vector3(xc, -CK.Sink, 0f), new Vector3(openW * 0.94f, gateH + CK.Sink, s.thickness * 0.55f), CK.D);

            mb.Pop();
        }

        static float YawFor(Vector3 dir)
        {
            // Local +X must land on dir; local -Z then lands on Cross(up, dir).
            return Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
        }

        static void WallLocal(MeshBuilder mb, float len, WallSpec s, bool capStart, bool capEnd)
        {
            WallSpan(mb, 0f, len, -CK.Sink, s.height, s, true);
            WallCrown(mb, len, s);

            if (capStart || capEnd)
            {
                float t = s.thickness * 0.5f;
                mb.Use(Mat.StoneBlock);
                if (capStart)
                    mb.WallQuad(new Vector3(0f, -CK.Sink, t), new Vector3(0f, -CK.Sink, -t),
                                new Vector3(0f, s.height, -t), new Vector3(0f, s.height, t), CK.D);
                if (capEnd)
                    mb.WallQuad(new Vector3(len, -CK.Sink, -t), new Vector3(len, -CK.Sink, t),
                                new Vector3(len, s.height, t), new Vector3(len, s.height, -t), CK.D);
            }
        }

        /// <summary>Solid body of a wall between two x positions. Outer face towards -Z.</summary>
        static void WallSpan(MeshBuilder mb, float x0, float x1, float yBottom, float yTop, WallSpec s, bool batter)
        {
            if (x1 - x0 < 0.001f || yTop - yBottom < 0.001f) return;

            float t = s.thickness * 0.5f;
            float bo = batter ? s.batterOut : 0f;
            float bh = batter ? Mathf.Min(s.batterH, yTop) : yBottom;
            float uOff = x0 * CK.D;

            // ---- outer side (-Z) ----
            if (batter)
            {
                mb.Use(Mat.StoneBlock);
                mb.WallQuad(new Vector3(x0, yBottom, -(t + bo)), new Vector3(x1, yBottom, -(t + bo)),
                            new Vector3(x1, bh, -t), new Vector3(x0, bh, -t), CK.D, uOff);
            }
            mb.Use(batter ? Mat.Plaster : Mat.StoneBlock);
            mb.WallQuad(new Vector3(x0, bh, -t), new Vector3(x1, bh, -t),
                        new Vector3(x1, yTop, -t), new Vector3(x0, yTop, -t), CK.D, uOff);

            // ---- inner side (+Z) ----
            float boi = bo * 0.5f;
            float bhi = batter ? Mathf.Min(s.batterH * 0.7f, yTop) : yBottom;
            if (batter)
            {
                mb.Use(Mat.StoneBlock);
                mb.WallQuad(new Vector3(x1, yBottom, t + boi), new Vector3(x0, yBottom, t + boi),
                            new Vector3(x0, bhi, t), new Vector3(x1, bhi, t), CK.D, uOff);
            }
            mb.Use(Mat.Plaster);
            mb.WallQuad(new Vector3(x1, bhi, t), new Vector3(x0, bhi, t),
                        new Vector3(x0, yTop, t), new Vector3(x1, yTop, t), CK.D, uOff);

            // ---- underside of an arch span ----
            if (!batter && yBottom > 0.01f)
            {
                mb.Use(Mat.StoneBlock);
                mb.Quad(new Vector3(x1, yBottom, -t), new Vector3(x0, yBottom, -t),
                        new Vector3(x0, yBottom, t), new Vector3(x1, yBottom, t), CK.D);
            }
        }

        /// <summary>Walkway deck, parapets, merlons and arrow slits over the full length.</summary>
        static void WallCrown(MeshBuilder mb, float len, WallSpec s)
        {
            float t = s.thickness * 0.5f;
            float H = s.height;
            float pT = s.parapetT;

            mb.Use(Mat.RoofFlat);
            mb.Quad(new Vector3(0f, H, -t), new Vector3(len, H, -t),
                    new Vector3(len, H, t), new Vector3(0f, H, t), CK.D);

            mb.Use(Mat.Plaster);
            mb.BoxOriented(new Vector3(len * 0.5f, H, -t + pT * 0.5f), Vector3.right, Vector3.forward,
                           new Vector3(len, s.parapetH, pT), CK.D);
            mb.Merlons(new Vector3(0f, H + s.parapetH, -t + pT * 0.5f),
                       new Vector3(len, H + s.parapetH, -t + pT * 0.5f),
                       pT, s.merlonH, s.merlonW, s.merlonGap, CK.D);

            if (s.innerParapet)
            {
                float pTi = pT * 0.8f;
                mb.BoxOriented(new Vector3(len * 0.5f, H, t - pTi * 0.5f), Vector3.right, Vector3.forward,
                               new Vector3(len, s.parapetH * 0.7f, pTi), CK.D);
            }

            if (s.slits && len > 6f)
            {
                mb.Use(Mat.Wood);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / 5.5f));
                for (int i = 0; i < n; i++)
                {
                    float x = len * (i + 0.5f) / n;
                    mb.Panel(new Vector3(x, H * 0.66f, -t), Vector3.right, Vector3.back, 0.45f, 1.3f);
                }
            }
        }

        // =====================================================================
        //  TOWERS
        // =====================================================================

        /// <summary>Square bastion. pos is the footprint centre at ground level.</summary>
        public static void SquareTower(MeshBuilder mb, Vector3 pos, float yaw, float baseSize, float height,
                                       WallSpec s, bool corbel = true)
        {
            mb.Push(new Vector3(pos.x, 0f, pos.z), yaw);

            float top = baseSize - 1.4f;
            float bh = Mathf.Min(s.batterH + 1.5f, height * 0.5f);

            mb.Use(Mat.StoneBlock);
            mb.TaperedBox(new Vector3(0f, -CK.Sink, 0f), Vector3.right, Vector3.forward,
                          new Vector2(baseSize, baseSize), new Vector2(top, top), bh + CK.Sink, CK.D, false);

            mb.Use(Mat.Plaster);
            mb.BoxOriented(new Vector3(0f, bh, 0f), Vector3.right, Vector3.forward,
                           new Vector3(top, height - bh, top), CK.D, false);

            float rim = top;
            if (corbel)
            {
                mb.Use(Mat.StoneBlock);
                rim = top + 0.7f;
                mb.TaperedBox(new Vector3(0f, height - 1.0f, 0f), Vector3.right, Vector3.forward,
                              new Vector2(top, top), new Vector2(rim, rim), 1.0f, CK.D, false);
            }

            // deck + parapet ring + merlons
            float pT = s.parapetT;
            mb.Use(Mat.RoofFlat);
            mb.Face(new Vector3(-rim * 0.5f, height, -rim * 0.5f), Vector3.right * rim, Vector3.forward * rim, CK.D);

            mb.Use(Mat.Plaster);
            float h2 = rim * 0.5f;
            mb.BoxOriented(new Vector3(0f, height, -h2 + pT * 0.5f), Vector3.right, Vector3.forward, new Vector3(rim, s.parapetH, pT), CK.D);
            mb.BoxOriented(new Vector3(0f, height, h2 - pT * 0.5f), Vector3.right, Vector3.forward, new Vector3(rim, s.parapetH, pT), CK.D);
            mb.BoxOriented(new Vector3(-h2 + pT * 0.5f, height, 0f), Vector3.forward, Vector3.right, new Vector3(rim - pT * 2f, s.parapetH, pT), CK.D);
            mb.BoxOriented(new Vector3(h2 - pT * 0.5f, height, 0f), Vector3.forward, Vector3.right, new Vector3(rim - pT * 2f, s.parapetH, pT), CK.D);

            float my = height + s.parapetH;
            float e = h2 - pT * 0.5f;
            mb.Merlons(new Vector3(-e, my, -e), new Vector3(e, my, -e), pT, s.merlonH, s.merlonW, s.merlonGap, CK.D);
            mb.Merlons(new Vector3(e, my, e), new Vector3(-e, my, e), pT, s.merlonH, s.merlonW, s.merlonGap, CK.D);
            mb.Merlons(new Vector3(-e, my, e), new Vector3(-e, my, -e), pT, s.merlonH, s.merlonW, s.merlonGap, CK.D);
            mb.Merlons(new Vector3(e, my, -e), new Vector3(e, my, e), pT, s.merlonH, s.merlonW, s.merlonGap, CK.D);

            // windows
            mb.Use(Mat.Wood);
            float half = top * 0.5f;
            for (int f = 0; f < 4; f++)
            {
                float fa = f * 90f * Mathf.Deg2Rad;
                Vector3 nrm = new Vector3(Mathf.Sin(fa), 0f, Mathf.Cos(fa));
                Vector3 rt = Vector3.Cross(Vector3.up, nrm);
                for (int row = 0; row < 2; row++)
                {
                    float y = bh + 1.8f + row * 3.4f;
                    if (y > height - 1.8f) continue;
                    mb.Panel(nrm * half + Vector3.up * y - rt * 1.5f, rt, nrm, 0.7f, 1.15f);
                    mb.Panel(nrm * half + Vector3.up * y + rt * 1.5f, rt, nrm, 0.7f, 1.15f);
                }
            }

            mb.Pop();
        }

        /// <summary>Big round keep tower with a machicolated crown.</summary>
        public static void RoundTower(MeshBuilder mb, Vector3 pos, float rBase, float height, WallSpec s,
                                      int sides = 20, bool windows = true)
        {
            mb.Push(new Vector3(pos.x, 0f, pos.z), 0f);

            float rMid = rBase - 0.55f;
            float rTop = rMid - 0.35f;
            float bh = Mathf.Min(s.batterH + 1.5f, height * 0.35f);

            mb.Use(Mat.StoneBlock);
            mb.Cylinder(Vector3.up * -CK.Sink, rBase, rMid, bh + CK.Sink, sides, CK.D, false);
            mb.Use(Mat.Plaster);
            mb.Cylinder(Vector3.up * bh, rMid, rTop, height - bh - 1.2f, sides, CK.D, false);

            // corbelled machicolation flare
            float rRim = rTop + 0.8f;
            mb.Use(Mat.StoneBlock);
            mb.Cylinder(Vector3.up * (height - 1.2f), rTop, rRim, 1.2f, sides, CK.D, false);

            // open deck + parapet ring
            float pT = s.parapetT * 1.1f;
            float pH = s.parapetH * 1.35f;
            Ring(mb, Vector3.zero, rRim, rRim - pT, height, pH, sides, true);

            mb.Use(Mat.RoofFlat);
            mb.Cylinder(Vector3.up * (height - 0.05f), rRim - pT, rRim - pT, 0.05f, sides, CK.D, true);

            mb.Use(Mat.Plaster);
            mb.MerlonRing(Vector3.up * (height + pH), rRim - pT * 0.5f, pT, s.merlonH * 1.25f, sides, 0.55f, CK.D);

            if (windows)
            {
                mb.Use(Mat.Wood);
                int cols = Mathf.Max(4, sides / 3);
                for (int row = 0; row < 3; row++)
                {
                    float y = bh + 2.5f + row * 5.5f;
                    if (y > height - 3f) continue;
                    for (int i = 0; i < cols; i++)
                    {
                        float a = Mathf.PI * 2f * (i + (row % 2) * 0.5f) / cols;
                        Vector3 nrm = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Vector3 rt = Vector3.Cross(Vector3.up, nrm);
                        float r = Mathf.Lerp(rMid, rTop, Mathf.InverseLerp(bh, height - 1.2f, y));
                        mb.Panel(nrm * r + Vector3.up * y, rt, nrm, 0.65f, 1.2f);
                    }
                }
            }

            mb.Pop();
        }

        /// <summary>Small round turret that sits on top of a wall or corner.</summary>
        public static void Turret(MeshBuilder mb, Vector3 pos, float radius, float height, WallSpec s, int sides = 12)
        {
            mb.Push(new Vector3(pos.x, 0f, pos.z), 0f);
            mb.Use(Mat.Plaster);
            mb.Cylinder(new Vector3(0f, pos.y, 0f), radius, radius * 0.94f, height, sides, CK.D, false);
            float rRim = radius * 0.94f + 0.35f;
            mb.Use(Mat.StoneBlock);
            mb.Cylinder(new Vector3(0f, pos.y + height - 0.6f, 0f), radius * 0.94f, rRim, 0.6f, sides, CK.D, false);
            mb.Use(Mat.RoofFlat);
            mb.Cylinder(new Vector3(0f, pos.y + height, 0f), rRim - 0.3f, rRim - 0.3f, 0.05f, sides, CK.D, true);
            mb.Use(Mat.Plaster);
            mb.MerlonRing(new Vector3(0f, pos.y + height, 0f), rRim - 0.2f, 0.35f, s.merlonH * 0.8f, sides, 0.55f, CK.D);
            mb.Pop();
        }

        /// <summary>Hollow ring wall (used for open tower crowns).</summary>
        static void Ring(MeshBuilder mb, Vector3 centre, float rOuter, float rInner, float yBase, float height, int sides, bool topFace)
        {
            mb.Use(Mat.Plaster);
            float circ = Mathf.PI * 2f * rOuter * CK.D;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 o0 = centre + new Vector3(Mathf.Cos(a0) * rOuter, yBase, Mathf.Sin(a0) * rOuter);
                Vector3 o1 = centre + new Vector3(Mathf.Cos(a1) * rOuter, yBase, Mathf.Sin(a1) * rOuter);
                Vector3 i0 = centre + new Vector3(Mathf.Cos(a0) * rInner, yBase, Mathf.Sin(a0) * rInner);
                Vector3 i1 = centre + new Vector3(Mathf.Cos(a1) * rInner, yBase, Mathf.Sin(a1) * rInner);
                Vector3 up = Vector3.up * height;

                float u0 = circ * i / sides, u1 = circ * (i + 1) / sides;
                float vb = yBase * CK.D, vt = (yBase + height) * CK.D;
                // outer skin (faces out)
                mb.Quad(o0, o1, o1 + up, o0 + up,
                    new Vector2(u0, vb), new Vector2(u1, vb), new Vector2(u1, vt), new Vector2(u0, vt));
                // inner skin (faces in)
                mb.Quad(i1, i0, i0 + up, i1 + up,
                    new Vector2(u0, vb), new Vector2(u1, vb), new Vector2(u1, vt), new Vector2(u0, vt));
                if (topFace)
                    mb.Quad(o0 + up, o1 + up, i1 + up, i0 + up,
                        new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f));
            }
        }

        // =====================================================================
        //  BUILDINGS
        // =====================================================================

        /// <summary>Town house. pos is the footprint centre at ground level.</summary>
        public static void House(MeshBuilder mb, Vector3 pos, float yaw, Vector2 size, int stories,
                                 HouseStyle style, System.Random rng, Mat wall = Mat.Plaster)
        {
            float storyH = Rng.F(rng, 2.9f, 3.4f);
            float h = storyH * stories;

            mb.Push(new Vector3(pos.x, pos.y, pos.z), yaw);

            Block(mb, Vector3.zero, size, h, style == HouseStyle.Flat || style == HouseStyle.Tower, rng, stories, storyH, true, wall);

            if (style == HouseStyle.Pitched)
            {
                mb.Use(Mat.Wood);
                PitchedRoof(mb, Vector3.up * h, size + new Vector2(0.5f, 0.5f),
                            Mathf.Min(size.x, size.y) * Rng.F(rng, 0.28f, 0.38f), size.x >= size.y);
            }
            else if (style == HouseStyle.Flat && Rng.Chance(rng, 0.45f))
            {
                // setback upper room, the stepped silhouette the reference is full of
                Vector2 s2 = new Vector2(size.x * Rng.F(rng, 0.45f, 0.7f), size.y * Rng.F(rng, 0.45f, 0.7f));
                Vector3 off = new Vector3((size.x - s2.x) * 0.5f * Rng.F(rng, -0.8f, 0.8f), h,
                                          (size.y - s2.y) * 0.5f * Rng.F(rng, -0.8f, 0.8f));
                Block(mb, off, s2, storyH, true, rng, 1, storyH, false, wall);
                if (Rng.Chance(rng, 0.35f))
                {
                    mb.Use(Mat.Wood);
                    PitchedRoof(mb, off + Vector3.up * storyH, s2 + new Vector2(0.4f, 0.4f), Mathf.Min(s2.x, s2.y) * 0.3f, s2.x >= s2.y);
                }
            }
            else if (style == HouseStyle.Tower)
            {
                // slim stair-head box on the roof
                Vector2 s2 = new Vector2(Rng.F(rng, 1.8f, 2.6f), Rng.F(rng, 1.8f, 2.6f));
                Block(mb, new Vector3(Rng.F(rng, -1f, 1f), h, Rng.F(rng, -1f, 1f)), s2, storyH * 0.85f, true, rng, 1, storyH, false, wall);
            }

            mb.Pop();
        }

        /// <summary>One rectangular volume: walls, roof deck, parapet, openings.</summary>
        static void Block(MeshBuilder mb, Vector3 basePos, Vector2 size, float h, bool parapet,
                          System.Random rng, int stories, float storyH, bool groundFloor, Mat wall = Mat.Plaster)
        {
            float hw = size.x * 0.5f, hd = size.y * 0.5f;
            float y0 = basePos.y - (groundFloor ? CK.Sink : 0f);
            Vector3 c = new Vector3(basePos.x, y0, basePos.z);
            float wallH = basePos.y + h - y0;

            mb.Use(wall);
            Vector3 b0 = c + new Vector3(-hw, 0f, -hd);
            Vector3 b1 = c + new Vector3(hw, 0f, -hd);
            Vector3 b2 = c + new Vector3(hw, 0f, hd);
            Vector3 b3 = c + new Vector3(-hw, 0f, hd);
            Vector3 up = Vector3.up * wallH;
            mb.WallQuad(b0, b1, b1 + up, b0 + up, CK.D);
            mb.WallQuad(b1, b2, b2 + up, b1 + up, CK.D);
            mb.WallQuad(b2, b3, b3 + up, b2 + up, CK.D);
            mb.WallQuad(b3, b0, b0 + up, b3 + up, CK.D);

            mb.Use(Mat.RoofFlat);
            mb.Quad(b0 + up, b1 + up, b2 + up, b3 + up, CK.D);

            if (parapet)
            {
                float pH = Rng.F(rng, 0.55f, 0.9f), pT = 0.25f;
                float ry = basePos.y + h;
                mb.Use(wall);
                mb.BoxOriented(new Vector3(basePos.x, ry, basePos.z - hd + pT * 0.5f), Vector3.right, Vector3.forward, new Vector3(size.x, pH, pT), CK.D);
                mb.BoxOriented(new Vector3(basePos.x, ry, basePos.z + hd - pT * 0.5f), Vector3.right, Vector3.forward, new Vector3(size.x, pH, pT), CK.D);
                mb.BoxOriented(new Vector3(basePos.x - hw + pT * 0.5f, ry, basePos.z), Vector3.forward, Vector3.right, new Vector3(size.y - pT * 2f, pH, pT), CK.D);
                mb.BoxOriented(new Vector3(basePos.x + hw - pT * 0.5f, ry, basePos.z), Vector3.forward, Vector3.right, new Vector3(size.y - pT * 2f, pH, pT), CK.D);
            }

            // openings
            mb.Use(Mat.Wood);
            for (int f = 0; f < 4; f++)
            {
                Vector3 nrm, rt;
                float span;
                switch (f)
                {
                    case 0: nrm = Vector3.back; rt = Vector3.right; span = size.x; break;
                    case 1: nrm = Vector3.right; rt = Vector3.forward; span = size.y; break;
                    case 2: nrm = Vector3.forward; rt = Vector3.left; span = size.x; break;
                    default: nrm = Vector3.left; rt = Vector3.back; span = size.y; break;
                }
                Vector3 faceC = new Vector3(basePos.x, 0f, basePos.z) + nrm * (f % 2 == 0 ? hd : hw);

                int cols = Mathf.Max(1, Mathf.FloorToInt(span / 2.6f));
                for (int s = 0; s < stories; s++)
                {
                    float wy = basePos.y + s * storyH + storyH * 0.62f;
                    for (int i = 0; i < cols; i++)
                    {
                        if (Rng.Chance(rng, 0.22f)) continue;
                        float x = (i + 0.5f) / cols * span - span * 0.5f;
                        mb.Panel(faceC + rt * x + Vector3.up * wy, rt, nrm, Rng.F(rng, 0.6f, 0.85f), Rng.F(rng, 0.85f, 1.15f));
                    }
                }
                if (groundFloor && f == 0)
                    mb.Panel(faceC + Vector3.up * 1.05f, rt, nrm, 1.05f, 2.1f);
            }
        }

        static void PitchedRoof(MeshBuilder mb, Vector3 basePos, Vector2 size, float rise, bool ridgeAlongX)
        {
            float hw = size.x * 0.5f, hd = size.y * 0.5f;
            Vector3 c = basePos;

            if (ridgeAlongX)
            {
                Vector3 a0 = c + new Vector3(-hw, 0f, -hd), a1 = c + new Vector3(hw, 0f, -hd);
                Vector3 b0 = c + new Vector3(-hw, 0f, hd), b1 = c + new Vector3(hw, 0f, hd);
                Vector3 r0 = c + new Vector3(-hw, rise, 0f), r1 = c + new Vector3(hw, rise, 0f);
                mb.Quad(a0, a1, r1, r0, CK.D);
                mb.Quad(b1, b0, r0, r1, CK.D);
                mb.Tri(a0, r0, b0, new Vector2(0f, 0f), new Vector2(hd * CK.D, rise * CK.D), new Vector2(hd * 2f * CK.D, 0f));
                mb.Tri(b1, r1, a1, new Vector2(0f, 0f), new Vector2(hd * CK.D, rise * CK.D), new Vector2(hd * 2f * CK.D, 0f));
            }
            else
            {
                Vector3 a0 = c + new Vector3(-hw, 0f, -hd), a1 = c + new Vector3(-hw, 0f, hd);
                Vector3 b0 = c + new Vector3(hw, 0f, -hd), b1 = c + new Vector3(hw, 0f, hd);
                Vector3 r0 = c + new Vector3(0f, rise, -hd), r1 = c + new Vector3(0f, rise, hd);
                mb.Quad(a1, a0, r0, r1, CK.D);
                mb.Quad(b0, b1, r1, r0, CK.D);
                mb.Tri(b0, r0, a0, new Vector2(0f, 0f), new Vector2(hw * CK.D, rise * CK.D), new Vector2(hw * 2f * CK.D, 0f));
                mb.Tri(a1, r1, b1, new Vector2(0f, 0f), new Vector2(hw * CK.D, rise * CK.D), new Vector2(hw * 2f * CK.D, 0f));
            }
        }

        /// <summary>Domed hall - the landmark buildings inside the town.</summary>
        public static void DomedHall(MeshBuilder mb, Vector3 pos, float yaw, Vector2 size, float bodyH, float domeR, System.Random rng, Mat wall = Mat.PlasterPale)
        {
            mb.Push(new Vector3(pos.x, pos.y, pos.z), yaw);
            Block(mb, Vector3.zero, size, bodyH, true, rng, Mathf.Max(1, Mathf.RoundToInt(bodyH / 3.2f)), 3.2f, true, wall);

            mb.Use(wall);
            mb.Cylinder(Vector3.up * bodyH, domeR * 1.05f, domeR, 1.6f, 24, CK.D, false);
            mb.Hemisphere(Vector3.up * (bodyH + 1.6f), domeR, 0.92f, 26, 12, CK.D);
            mb.Use(Mat.Wood);
            mb.Cylinder(Vector3.up * (bodyH + 1.6f + domeR * 0.92f), 0.16f, 0.06f, 1.1f, 6, CK.D);
            mb.Pop();
        }

        /// <summary>Minaret / watch spire.</summary>
        public static void Minaret(MeshBuilder mb, Vector3 pos, float radius, float height)
        {
            mb.Push(new Vector3(pos.x, pos.y, pos.z), 0f);
            mb.Use(Mat.Plaster);
            mb.Cylinder(Vector3.up * -CK.Sink, radius * 1.25f, radius, height * 0.72f + CK.Sink, 12, CK.D, false);
            mb.Use(Mat.StoneBlock);
            mb.Cylinder(Vector3.up * (height * 0.72f), radius * 1.35f, radius * 1.35f, 0.9f, 12, CK.D, false);
            mb.Use(Mat.Plaster);
            mb.Cylinder(Vector3.up * (height * 0.72f + 0.9f), radius * 0.85f, radius * 0.8f, height * 0.28f - 0.9f, 12, CK.D, false);
            mb.Hemisphere(Vector3.up * height, radius * 0.85f, 1.15f, 16, 8, CK.D);
            mb.Pop();
        }

        /// <summary>Flight of steps climbing to a wall walk.</summary>
        public static void Stairs(MeshBuilder mb, Vector3 pos, float yaw, float width, float rise, float run)
        {
            mb.Push(new Vector3(pos.x, pos.y, pos.z), yaw);
            mb.Use(Mat.StoneBlock);
            int steps = Mathf.Max(2, Mathf.RoundToInt(rise / 0.38f));
            float stepH = rise / steps, stepD = run / steps;
            for (int i = 0; i < steps; i++)
            {
                float d = run - i * stepD;
                mb.Box(new Vector3(0f, i * stepH, -(run - d) * 0.5f),
                       new Vector3(width, stepH + 0.02f, d), CK.D, i == steps - 1);
            }
            mb.Pop();
        }

        /// <summary>
        /// Abandoned shell of a house: walls broken down to a ragged line, roof gone,
        /// rubble spilling out of the footprint.
        /// </summary>
        public static void Ruin(MeshBuilder mb, Vector3 pos, float yaw, Vector2 size, float maxH,
                                System.Random rng, Mat wall)
        {
            mb.Push(new Vector3(pos.x, pos.y, pos.z), yaw);

            float hw = size.x * 0.5f, hd = size.y * 0.5f;
            float t = Rng.F(rng, 0.4f, 0.55f);

            Vector3 c0 = new Vector3(-hw, 0f, -hd), c1 = new Vector3(hw, 0f, -hd);
            Vector3 c2 = new Vector3(hw, 0f, hd), c3 = new Vector3(-hw, 0f, hd);
            RuinRun(mb, c0, c1, t, maxH, rng, wall);
            RuinRun(mb, c1, c2, t, maxH, rng, wall);
            RuinRun(mb, c2, c3, t, maxH, rng, wall);
            RuinRun(mb, c3, c0, t, maxH, rng, wall);

            // packed earth floor still showing inside the shell
            mb.Use(Mat.RoofFlat);
            mb.Box(new Vector3(0f, -CK.Sink, 0f), new Vector3(size.x - t, CK.Sink + 0.12f, size.y - t), CK.D);

            RubblePile(mb, Vector3.zero, Mathf.Max(hw, hd) * 1.2f, Rng.I(rng, 7, 18), rng, wall);
            mb.Pop();
        }

        /// <summary>One run of collapsed wall: slabs whose height random-walks and drops out entirely.</summary>
        static void RuinRun(MeshBuilder mb, Vector3 a, Vector3 b, float thickness, float maxH,
                            System.Random rng, Mat wall)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            d /= len;
            Vector3 side = Vector3.Cross(Vector3.up, d);

            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.85f));
            float w = len / n;
            float h = Rng.F(rng, 0.3f, 1f) * maxH;

            mb.Use(wall);
            for (int i = 0; i < n; i++)
            {
                h = Mathf.Clamp(h + Rng.F(rng, -0.5f, 0.5f), 0f, maxH);
                if (Rng.Chance(rng, 0.14f)) h = Rng.F(rng, 0f, maxH * 0.28f);
                if (h < 0.3f) continue;
                Vector3 c = a + d * ((i + 0.5f) * w);
                mb.BoxOriented(c + Vector3.down * CK.Sink, d, side,
                               new Vector3(w * 1.03f, h + CK.Sink, thickness), CK.D);
            }
        }

        /// <summary>Scatter of fallen masonry.</summary>
        public static void RubblePile(MeshBuilder mb, Vector3 centre, float radius, int count,
                                      System.Random rng, Mat mat)
        {
            mb.Use(mat);
            for (int i = 0; i < count; i++)
            {
                float a = Rng.F(rng, 0f, Mathf.PI * 2f);
                float r = radius * Mathf.Sqrt((float)rng.NextDouble());
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, -0.1f, Mathf.Sin(a) * r);
                float ya = Rng.F(rng, 0f, Mathf.PI * 2f);
                Vector3 right = new Vector3(Mathf.Cos(ya), 0f, Mathf.Sin(ya));
                Vector3 fwd = Vector3.Cross(Vector3.up, right);
                mb.BoxOriented(p, right, fwd,
                    new Vector3(Rng.F(rng, 0.3f, 0.9f), Rng.F(rng, 0.15f, 0.5f), Rng.F(rng, 0.3f, 0.8f)),
                    CK.D * 2.5f);
            }
        }

        /// <summary>Black goat-hair caravan tent.</summary>
        public static void Tent(MeshBuilder mb, Vector3 pos, float yaw, float width, float depth, float height)
        {
            mb.Push(new Vector3(pos.x, pos.y, pos.z), yaw);
            float hw = width * 0.5f, hd = depth * 0.5f;

            Vector3 a0 = new Vector3(-hw, 0f, -hd), a1 = new Vector3(-hw, 0f, hd);
            Vector3 b0 = new Vector3(hw, 0f, -hd), b1 = new Vector3(hw, 0f, hd);
            Vector3 r0 = new Vector3(0f, height, -hd), r1 = new Vector3(0f, height, hd);

            mb.Use(Mat.Wood);
            mb.Quad(a1, a0, r0, r1, CK.D);
            mb.Quad(b0, b1, r1, r0, CK.D);
            mb.Tri(b0, r0, a0, new Vector2(0f, 0f), new Vector2(hw * CK.D, height * CK.D), new Vector2(hw * 2f * CK.D, 0f));

            mb.Box(new Vector3(0f, 0f, -hd), new Vector3(0.13f, height, 0.13f), CK.D);
            mb.Box(new Vector3(0f, 0f, hd), new Vector3(0.13f, height, 0.13f), CK.D);
            mb.Pop();
        }

        // =====================================================================
        //  TERRAIN
        // =====================================================================

        /// <summary>
        /// Open dune field. The city stands on a wind-scoured flat inside
        /// <paramref name="flatRadius"/>; beyond it the sand builds into ridged barchan
        /// dunes that grow towards the horizon. Built as a shared-vertex grid with
        /// analytic normals so the sand reads smooth instead of faceted.
        /// </summary>
        public static void Dunes(MeshBuilder mb, float size, int res, float flatRadius, int seed)
        {
            mb.Use(Mat.Sand);
            float half = size * 0.5f;
            float step = size / res;
            Vector2 off = DuneOffset(seed);

            System.Func<float, float, float> h = (x, z) => DuneHeight(x + off.x, z + off.y, flatRadius, x, z);

            int n = res + 1;
            var idx = new int[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = -half + i * step, z = -half + j * step;
                    float y = h(x, z);
                    // central differences give a smooth normal without averaging faces
                    float e = step * 0.5f;
                    float dx = h(x + e, z) - h(x - e, z);
                    float dz = h(x, z + e) - h(x, z - e);
                    Vector3 nrm = new Vector3(-dx, 2f * e, -dz).normalized;
                    idx[j * n + i] = mb.Vertex(new Vector3(x, y, z), nrm, new Vector2(x, z) * CK.D * 0.5f);
                }

            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    int a = idx[j * n + i], b = idx[j * n + i + 1];
                    int c = idx[(j + 1) * n + i + 1], d = idx[(j + 1) * n + i];
                    mb.Triangle(a, b, c);
                    mb.Triangle(a, c, d);
                }
        }

        /// <summary>Noise offset for a seed. Anything placed on the dunes must use the same one.</summary>
        public static Vector2 DuneOffset(int seed)
        {
            return new Vector2(Mathf.Abs(seed % 733) * 3.17f, Mathf.Abs(seed % 941) * 2.71f);
        }

        /// <summary>Dune elevation in metres at a world position.</summary>
        public static float DuneHeight(float nx, float nz, float flatRadius, float wx, float wz)
        {
            float dist = new Vector2(wx, wz).magnitude;
            // flat scoured pan under the walls, dunes ramp up outside it
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flatRadius, flatRadius * 1.6f, dist));
            if (open <= 0.0005f) return 0f;

            float big = Ridged(nx * 0.0030f, nz * 0.0039f) * 23f;   // barchan crests
            float mid = Fbm(nx * 0.009f, nz * 0.011f) * 5.5f;        // secondary swells
            float fine = Fbm(nx * 0.06f, nz * 0.062f) * 0.5f;        // surface texture
            float ripple = Mathf.Sin((nx * 0.62f + nz * 0.22f) + Fbm(nx * 0.02f, nz * 0.02f) * 5f) * 0.13f;
            return (big + mid + fine + ripple) * open;
        }

        /// <summary>Ridged multifractal - gives dunes sharp crests and soft troughs.</summary>
        static float Ridged(float x, float y)
        {
            float sum = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < 5; i++)
            {
                float v = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f, y * f) * 2f - 1f);
                sum += v * v * amp;
                amp *= 0.5f; f *= 2.13f;
            }
            return sum - 0.55f;
        }

        /// <summary>Low blown-sand mound. Used for drifts banked against walls and buried ruins.</summary>
        public static void SandMound(MeshBuilder mb, Vector3 pos, float radius, float height, System.Random rng)
        {
            mb.Use(Mat.Sand);
            const int sides = 12, rings = 3;
            var prev = new Vector3[sides];
            var jitter = new float[sides];
            for (int i = 0; i < sides; i++) jitter[i] = Rng.F(rng, 0.65f, 1.35f);

            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                prev[i] = pos + new Vector3(Mathf.Cos(a) * radius * jitter[i], -0.15f, Mathf.Sin(a) * radius * jitter[i]);
            }

            for (int r = 1; r <= rings; r++)
            {
                float t = (float)r / rings;
                float rad = Mathf.Cos(t * Mathf.PI * 0.5f);
                float y = Mathf.Sin(t * Mathf.PI * 0.5f) * height;
                var cur = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = Mathf.PI * 2f * i / sides;
                    cur[i] = pos + new Vector3(Mathf.Cos(a) * radius * jitter[i] * rad, y, Mathf.Sin(a) * radius * jitter[i] * rad);
                }
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    if (r == rings)
                        mb.Tri(prev[i], prev[j], pos + Vector3.up * height,
                            new Vector2(prev[i].x, prev[i].z) * CK.D, new Vector2(prev[j].x, prev[j].z) * CK.D,
                            new Vector2(pos.x, pos.z) * CK.D);
                    else
                        mb.Quad(prev[i], prev[j], cur[j], cur[i], CK.D);
                }
                prev = cur;
            }
        }

        /// <summary>Sand banked along the windward face of a wall.</summary>
        public static void WallDrift(MeshBuilder mb, Vector3 a, Vector3 b, float reach, float height, System.Random rng)
        {
            Vector3 dir = b - a; dir.y = 0f;
            float len = dir.magnitude;
            if (len < 3f) return;
            dir /= len;
            int count = Mathf.Max(1, Mathf.RoundToInt(len / Rng.F(rng, 7f, 12f)));
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < count; i++)
            {
                float t = (i + Rng.F(rng, 0.2f, 0.8f)) / count;
                Vector3 p = a + dir * (len * t) + side * Rng.F(rng, 0.2f, reach * 0.45f);
                SandMound(mb, p, Rng.F(rng, reach * 0.6f, reach * 1.3f), Rng.F(rng, height * 0.5f, height), rng);
            }
        }

        /// <summary>Scattered desert rock.</summary>
        public static void Rock(MeshBuilder mb, Vector3 pos, float radius, float height, System.Random rng)
        {
            mb.Use(Mat.StoneBlock);
            mb.Push(new Vector3(pos.x, pos.y, pos.z), Rng.F(rng, 0f, 360f));
            int sides = 6;
            var bot = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                float rb = radius * Rng.F(rng, 0.7f, 1.2f);
                float rt = rb * Rng.F(rng, 0.25f, 0.55f);
                bot[i] = new Vector3(Mathf.Cos(a) * rb, -0.15f, Mathf.Sin(a) * rb);
                top[i] = new Vector3(Mathf.Cos(a) * rt, height * Rng.F(rng, 0.75f, 1.1f), Mathf.Sin(a) * rt);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.Quad(bot[i], bot[j], top[j], top[i], CK.D * 2f);
            }
            Vector3 apex = Vector3.zero;
            for (int i = 0; i < sides; i++) apex += top[i];
            apex /= sides;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.Tri(apex, top[i], top[j],
                    new Vector2(apex.x, apex.z) * CK.D * 2f,
                    new Vector2(top[i].x, top[i].z) * CK.D * 2f,
                    new Vector2(top[j].x, top[j].z) * CK.D * 2f);
            }
            mb.Pop();
        }

        // ---- noise ----------------------------------------------------------

        public static float Fbm(float x, float y)
        {
            float v = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < 4; i++)
            {
                v += (Mathf.PerlinNoise(x * f, y * f) - 0.5f) * 2f * amp;
                amp *= 0.5f; f *= 2.07f;
            }
            return v;
        }
    }
}
