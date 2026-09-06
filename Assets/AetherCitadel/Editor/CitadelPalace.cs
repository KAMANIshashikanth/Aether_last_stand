using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>
    /// The closed palace that sits at the heart of the town.
    ///
    /// Plan is a regular pentagon. Face 0 looks along -Z and carries the main
    /// portal; faces 1 and 4 flank it and carry the side portals, so three
    /// approaches feed the arena. Faces 2 and 3 are the solid rear wall.
    /// Outer surfaces batter inward like an Egyptian pylon, inner surfaces are
    /// vertical, and the top is sealed - the roof is emitted into a separate
    /// mesh so it can be hidden in the editor or shattered at runtime.
    /// </summary>
    public static class Palace
    {
        public struct Spec
        {
            public float radius;      // circumradius of the outer pentagon at its base
            public float height;      // base of wall to underside of roof
            public float thickness;   // wall thickness
            public float batter;      // how far the outer face leans in over the full height
            public float podium;      // height of the stepped platform it stands on
            public float mainW, mainH;
            public float sideW, sideH;

            public static Spec Default()
            {
                return new Spec
                {
                    radius = 34f,
                    height = 21f,
                    thickness = 4.6f,
                    batter = 2.6f,
                    podium = 3.6f,
                    mainW = 12f,
                    mainH = 15f,
                    sideW = 8f,
                    sideH = 11f
                };
            }
        }

        const float D = CK.D;

        /// <summary>Inner circumradius: offsetting every edge in by t shrinks the apothem by t.</summary>
        static float InnerRadius(Spec s) { return s.radius - s.thickness / Mathf.Cos(36f * Mathf.Deg2Rad); }

        // =====================================================================
        //  entry point
        // =====================================================================

        public static void Build(MeshBuilder body, MeshBuilder roof, Vector3 pos, float yaw, Spec s, System.Random rng)
        {
            body.Push(pos, yaw);
            roof.Push(pos, yaw);

            float floor = s.podium;                 // interior floor level
            float top = floor + s.height;           // wall head
            float rIn = InnerRadius(s);

            Podium(body, s);
            Approach(body, s);

            var baseRing = Pentagon(s.radius, floor);
            for (int i = 0; i < 5; i++)
            {
                Vector3 a = baseRing[i], b = baseRing[(i + 1) % 5];
                float w = 0f, h = 0f;
                if (i == 0) { w = s.mainW; h = s.mainH; }
                else if (i == 1 || i == 4) { w = s.sideW; h = s.sideH; }
                Wall(body, a, b, s, floor, w, h);
                Cornice(body, a, b, s, top);
                if (w > 0f) Portal(body, a, b, s, floor, w, h, rng);
                else Pilasters(body, a, b, s);   // the blank rear faces need relief
            }

            CornerPylons(body, s, floor, top);

            // the pair of colossi guarding the main portal, as in the concept sheet
            float apothem = s.radius * Mathf.Cos(36f * Mathf.Deg2Rad);
            float guardX = s.mainW * 0.5f + 5.5f;
            float guardH = s.mainH * 1.05f;
            Colossus(body, new Vector3(-guardX, floor, -(apothem + 2.2f)), 180f, guardH);
            Colossus(body, new Vector3(guardX, floor, -(apothem + 2.2f)), 180f, guardH);

            Interior(body, s, floor, top, rIn, rng);
            Roof(roof, s, top, rIn);

            body.Pop();
            roof.Pop();
        }

        // =====================================================================
        //  shell
        // =====================================================================

        /// <summary>One pentagon face, with an optional centred portal punched through it.</summary>
        static void Wall(MeshBuilder mb, Vector3 a, Vector3 b, Spec s, float y0, float openW, float openH)
        {
            Vector3 dir = b - a;
            float L = dir.magnitude;
            dir /= L;
            Vector3 inward = -Vector3.Cross(Vector3.up, dir);

            // outer surface leans in with height, inner surface stays vertical
            System.Func<float, float, Vector3> O = (d, y) =>
                a + dir * d + inward * (s.batter * (y / s.height)) + Vector3.up * y;
            System.Func<float, float, Vector3> I = (d, y) =>
                a + dir * d + inward * s.thickness + Vector3.up * y;

            float h = s.height;

            if (openW <= 0f)
            {
                mb.Use(Mat.StoneBlock);
                mb.Quad(O(0f, 0f), O(L, 0f), O(L, h), O(0f, h), D);
                mb.Use(Mat.Plaster);
                mb.Quad(I(L, 0f), I(0f, 0f), I(0f, h), I(L, h), D);
                mb.Use(Mat.StoneBlock);
                mb.Quad(O(0f, h), O(L, h), I(L, h), I(0f, h), D);
                return;
            }

            float s0 = (L - openW) * 0.5f, s1 = (L + openW) * 0.5f;

            mb.Use(Mat.StoneBlock);
            mb.Quad(O(0f, 0f), O(s0, 0f), O(s0, h), O(0f, h), D);        // left pier
            mb.Quad(O(s1, 0f), O(L, 0f), O(L, h), O(s1, h), D);          // right pier
            mb.Quad(O(s0, openH), O(s1, openH), O(s1, h), O(s0, h), D);  // lintel

            mb.Use(Mat.Plaster);
            mb.Quad(I(s0, 0f), I(0f, 0f), I(0f, h), I(s0, h), D);
            mb.Quad(I(L, 0f), I(s1, 0f), I(s1, h), I(L, h), D);
            mb.Quad(I(s1, openH), I(s0, openH), I(s0, h), I(s1, h), D);

            mb.Use(Mat.StoneBlock);
            mb.Quad(O(0f, h), O(L, h), I(L, h), I(0f, h), D);            // wall head

            // reveals: two jambs and the soffit under the lintel
            mb.Quad(O(s0, 0f), I(s0, 0f), I(s0, openH), O(s0, openH), D);
            mb.Quad(I(s1, 0f), O(s1, 0f), O(s1, openH), I(s1, openH), D);
            mb.Quad(I(s0, openH), I(s1, openH), O(s1, openH), O(s0, openH), D);
        }

        /// <summary>Lapis banners, torch brackets and a threshold slab dressing one portal.</summary>
        static void Portal(MeshBuilder mb, Vector3 a, Vector3 b, Spec s, float y0, float openW, float openH, System.Random rng)
        {
            Vector3 dir = b - a;
            float L = dir.magnitude;
            dir /= L;
            Vector3 outward = Vector3.Cross(Vector3.up, dir);
            Vector3 inward = -outward;

            System.Func<float, float, Vector3> O = (d, y) =>
                a + dir * d + inward * (s.batter * (y / s.height)) + Vector3.up * y;

            float s0 = (L - openW) * 0.5f, s1 = (L + openW) * 0.5f;

            // banner panels either side of the opening, lying on the battered face
            mb.Use(Mat.Lapis);
            float bw = Mathf.Min(2.4f, s0 * 0.45f);
            foreach (float centre in new[] { s0 - bw * 1.4f, s1 + bw * 1.4f })
            {
                float lo = 1.2f, hi = s.height - 1.6f;
                Vector3 p0 = O(centre - bw * 0.5f, lo) + outward * 0.12f;
                Vector3 p1 = O(centre + bw * 0.5f, lo) + outward * 0.12f;
                Vector3 p2 = O(centre + bw * 0.5f, hi) + outward * 0.12f;
                Vector3 p3 = O(centre - bw * 0.5f, hi) + outward * 0.12f;
                mb.Quad(p0, p1, p2, p3,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            }

            // threshold slab and a pair of braziers flanking the doorway
            mb.Use(Mat.StoneBlock);
            Vector3 mid = a + dir * (L * 0.5f);
            mb.BoxOriented(mid + outward * 0.6f, dir, outward, new Vector3(openW + 2.2f, 0.28f, 3.2f), D);

            Brazier(mb, mid + dir * (openW * 0.5f + 1.9f) + outward * 1.9f, 2.6f);
            Brazier(mb, mid - dir * (openW * 0.5f + 1.9f) + outward * 1.9f, 2.6f);
            Brazier(mb, mid + dir * (openW * 0.5f + 1.4f) + inward * (s.thickness + 2.4f), 2.4f);
            Brazier(mb, mid - dir * (openW * 0.5f + 1.4f) + inward * (s.thickness + 2.4f), 2.4f);
        }

        /// <summary>
        /// Shallow buttress strips up a solid face. Without these the rear walls are
        /// twenty-eight metres of unbroken flat sandstone and read as a crate.
        /// </summary>
        static void Pilasters(MeshBuilder mb, Vector3 a, Vector3 b, Spec s)
        {
            Vector3 dir = b - a;
            float L = dir.magnitude;
            dir /= L;
            Vector3 outward = Vector3.Cross(Vector3.up, dir);
            Vector3 inward = -outward;

            mb.Use(Mat.StoneBlock);
            const int count = 4;
            for (int i = 1; i <= count; i++)
            {
                float d = L * i / (count + 1);
                Vector3 foot = a + dir * d;
                // follow the batter so the strip stays flush against the leaning face
                float h = s.height - 1.2f;
                Vector3 lean = inward * (s.batter * (h / s.height));
                mb.Quad(foot + outward * 0.35f - dir * 0.9f,
                        foot + outward * 0.35f + dir * 0.9f,
                        foot + outward * 0.35f + dir * 0.9f + lean + Vector3.up * h,
                        foot + outward * 0.35f - dir * 0.9f + lean + Vector3.up * h, D);
                foreach (float side in new[] { -0.9f, 0.9f })
                {
                    Vector3 e = foot + dir * side;
                    Vector3 o = e + outward * 0.35f, n = e;
                    mb.Quad(side < 0f ? n : o, side < 0f ? o : n,
                            (side < 0f ? o : n) + lean + Vector3.up * h,
                            (side < 0f ? n : o) + lean + Vector3.up * h, D);
                }
            }
        }

        /// <summary>Flared cavetto band capping each face.</summary>
        static void Cornice(MeshBuilder mb, Vector3 a, Vector3 b, Spec s, float top)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 inward = -Vector3.Cross(Vector3.up, dir);
            float L = (b - a).magnitude;

            Vector3 baseIn = inward * s.batter;
            Vector3 lift = Vector3.up * 1.5f;
            Vector3 flare = -inward * 0.85f;

            Vector3 p0 = a + baseIn + Vector3.up * (top - 0f);
            Vector3 p1 = b + baseIn + Vector3.up * (top - 0f);

            mb.Use(Mat.StoneBlock);
            mb.Quad(p0, p1, p1 + lift + flare, p0 + lift + flare, D);
            mb.Quad(p0 + lift + flare, p1 + lift + flare,
                    p1 + lift + flare + inward * 1.4f, p0 + lift + flare + inward * 1.4f, D);

            // a lapis frieze tucked under the flare
            mb.Use(Mat.Lapis);
            Vector3 f0 = p0 + Vector3.up * 0.25f - inward * 0.03f;
            Vector3 f1 = p1 + Vector3.up * 0.25f - inward * 0.03f;
            mb.Quad(f0, f1, f1 + Vector3.up * 0.85f, f0 + Vector3.up * 0.85f,
                new Vector2(0f, 0f), new Vector2(L * 0.25f, 0f), new Vector2(L * 0.25f, 1f), new Vector2(0f, 1f));
        }

        /// <summary>Thick vertical masses at the five corners; they also hide the face joints.</summary>
        static void CornerPylons(MeshBuilder mb, Spec s, float floor, float top)
        {
            mb.Use(Mat.StoneBlock);
            var v = Pentagon(s.radius, floor);
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = v[i];
                Vector3 outward = new Vector3(c.x, 0f, c.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                float h = top - floor + 1.1f;
                mb.TaperedBox(new Vector3(c.x, floor, c.z) - outward * 1.6f, side, outward,
                              new Vector2(5.6f, 5.2f), new Vector2(4.4f, 4.0f), h, D);
                mb.Box(new Vector3(c.x, floor + h, c.z) - outward * 1.6f, new Vector3(5.0f, 0.6f, 4.6f), D);
            }
        }

        /// <summary>Three stepped pentagon rings the whole building stands on.</summary>
        static void Podium(MeshBuilder mb, Spec s)
        {
            mb.Use(Mat.StoneBlock);
            for (int k = 0; k < 3; k++)
            {
                float r = s.radius + 7f - k * 2f;
                float h = s.podium * (k + 1) / 3f;
                PentPrism(mb, r, h);
            }
        }

        /// <summary>Broad flight up to the main portal.</summary>
        static void Approach(MeshBuilder mb, Spec s)
        {
            mb.Use(Mat.StoneBlock);
            float front = -(s.radius * Mathf.Cos(36f * Mathf.Deg2Rad) + 7f);
            int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 1f) / steps;
                float y = s.podium * t;
                float z = front - (steps - i) * 0.9f;
                mb.Box(new Vector3(0f, 0f, z), new Vector3(s.mainW * 2.4f - i * 0.6f, y, 1.2f), D);
            }
        }

        // =====================================================================
        //  interior
        // =====================================================================

        static void Interior(MeshBuilder mb, Spec s, float floor, float top, float rIn, System.Random rng)
        {
            // floor slab and the sigil burned into the middle of it
            mb.Use(Mat.RoofFlat);
            PentCap(mb, floor, rIn, true);
            RuneDisc(mb, floor + 0.04f, rIn * 0.62f, 64);

            // colonnade ringing the arena, spaced off the shell so it scales with it
            float colRing = rIn * 0.78f;
            int cols = Mathf.Clamp(Mathf.RoundToInt(colRing * 0.72f), 10, 16);
            for (int i = 0; i < cols; i++)
            {
                float ang = Mathf.PI * 2f * i / cols + Mathf.PI / cols;
                Vector3 p = new Vector3(Mathf.Cos(ang) * colRing, floor, Mathf.Sin(ang) * colRing);
                Pillar(mb, p, rIn * 0.045f, top - floor);
            }

            // gods along the solid rear arc, all facing the centre
            for (int i = 0; i < 5; i++)
            {
                float deg = 18f + 36f * i;
                float ang = deg * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(ang) * rIn * 0.88f, floor, Mathf.Sin(ang) * rIn * 0.88f);
                float yaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;
                Colossus(mb, p, yaw, (top - floor) * 0.62f);
            }

            // braziers around the sigil
            for (int i = 0; i < 6; i++)
            {
                float ang = Mathf.PI * 2f * i / 6f + 0.3f;
                Brazier(mb, new Vector3(Mathf.Cos(ang) * rIn * 0.72f, floor, Mathf.Sin(ang) * rIn * 0.72f), 3.2f);
            }
        }

        static void Roof(MeshBuilder mb, Spec s, float top, float rIn)
        {
            float deck = top + 0.9f;
            mb.Use(Mat.StoneBlock);
            PentCap(mb, top, rIn, false);          // ceiling, seen from inside
            mb.Use(Mat.RoofFlat);
            PentCap(mb, deck, rIn + 1.2f, true);   // deck, seen from above

            // exposed slab edge
            var lo = Pentagon(rIn + 1.2f, top);
            var hi = Pentagon(rIn + 1.2f, deck);
            mb.Use(Mat.StoneBlock);
            for (int i = 0; i < 5; i++)
            {
                int j = (i + 1) % 5;
                mb.Quad(lo[i], lo[j], hi[j], hi[i], D);
            }

            // the medallion the top view shows at the crown
            mb.Use(Mat.StoneBlock);
            mb.Cylinder(new Vector3(0f, deck, 0f), rIn * 0.34f, rIn * 0.32f, 0.5f, 8, D, false);
            RuneDisc(mb, deck + 0.55f, rIn * 0.31f, 44);
        }

        // =====================================================================
        //  furniture
        // =====================================================================

        static void Pillar(MeshBuilder mb, Vector3 pos, float r, float h)
        {
            mb.Use(Mat.StoneBlock);
            mb.Box(pos, new Vector3(r * 2.9f, 0.45f, r * 2.9f), D);

            mb.Use(Mat.Plaster);
            mb.Cylinder(pos + Vector3.up * 0.45f, r, r * 0.86f, h - 2.1f, 16, D, false);

            mb.Use(Mat.Lapis);
            mb.Cylinder(pos + Vector3.up * (h - 1.9f), r * 0.93f, r * 0.93f, 0.55f, 16, D, false);

            mb.Use(Mat.StoneBlock);
            mb.Cylinder(pos + Vector3.up * (h - 1.35f), r * 0.9f, r * 1.4f, 0.95f, 16, D);
            mb.Box(pos + Vector3.up * (h - 0.4f), new Vector3(r * 3.2f, 0.4f, r * 3.2f), D);
        }

        static void Brazier(MeshBuilder mb, Vector3 pos, float h)
        {
            Pieces.Brazier(mb, pos, h);
        }

        /// <summary>
        /// Osiride colossus - a mummiform standing god against a back pillar.
        /// Deliberately blocky: real Egyptian colossi read as stacked masses, and
        /// it keeps every statue under a few hundred triangles.
        /// </summary>
        public static void Colossus(MeshBuilder mb, Vector3 pos, float yaw, float h)
        {
            mb.Push(pos, yaw);
            float u = h / 10f;

            mb.Use(Mat.StoneBlock);
            mb.Box(Vector3.zero, new Vector3(3.0f * u, 0.7f * u, 2.4f * u), D);                        // plinth
            mb.Box(new Vector3(0f, 0.7f * u, -0.95f * u), new Vector3(2.2f * u, 8.4f * u, 0.5f * u), D); // back pillar

            // one mummiform mass from ankle to chest - the wrapped body has no waist
            mb.TaperedBox(new Vector3(0f, 0.7f * u, 0f), Vector3.right, Vector3.forward,
                          new Vector2(2.6f * u, 1.75f * u), new Vector2(2.3f * u, 1.5f * u), 5.5f * u, D, false);

            // arms crossed high on the chest, standing proud so they catch light
            mb.Box(new Vector3(0f, 4.30f * u, 0.80f * u), new Vector3(2.25f * u, 0.46f * u, 0.42f * u), D);
            mb.Box(new Vector3(0f, 4.82f * u, 0.74f * u), new Vector3(2.05f * u, 0.44f * u, 0.40f * u), D);

            // shoulders taper in rather than shelving out
            mb.TaperedBox(new Vector3(0f, 6.2f * u, 0f), Vector3.right, Vector3.forward,
                          new Vector2(2.85f * u, 1.7f * u), new Vector2(2.4f * u, 1.5f * u), 0.4f * u, D, false);
            mb.Box(new Vector3(0f, 6.6f * u, 0f), new Vector3(1.05f * u, 0.25f * u, 1.0f * u), D);      // neck

            // head is a sixth of total height, the proportion that makes a colossus read
            mb.Box(new Vector3(0f, 6.85f * u, 0.05f * u), new Vector3(1.5f * u, 1.6f * u, 1.6f * u), D);
            mb.Box(new Vector3(0f, 6.2f * u, 0.85f * u), new Vector3(0.36f * u, 0.9f * u, 0.34f * u), D); // beard

            // nemes headdress: lappets down the chest, then the cap over the crown
            mb.Use(Mat.Lapis);
            mb.Box(new Vector3(-0.98f * u, 6.35f * u, 0.30f * u), new Vector3(0.48f * u, 1.95f * u, 1.15f * u), D);
            mb.Box(new Vector3(0.98f * u, 6.35f * u, 0.30f * u), new Vector3(0.48f * u, 1.95f * u, 1.15f * u), D);
            mb.Box(new Vector3(0f, 8.30f * u, 0.03f * u), new Vector3(1.62f * u, 0.26f * u, 1.72f * u), D);
            mb.TaperedBox(new Vector3(0f, 8.42f * u, -0.03f * u), Vector3.right, Vector3.forward,
                          new Vector2(1.9f * u, 1.85f * u), new Vector2(1.15f * u, 1.15f * u), 0.75f * u, D);

            mb.Use(Mat.StoneBlock);
            mb.Cylinder(new Vector3(0f, 9.17f * u, 0f), 0.46f * u, 0.32f * u, 0.85f * u, 10, D);        // crown

            mb.Pop();
        }

        // =====================================================================
        //  pentagon helpers
        // =====================================================================

        /// <summary>Vertices ordered so that face 0 (v0 to v1) faces -Z.</summary>
        static Vector3[] Pentagon(float radius, float y)
        {
            var v = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                float a = (234f + 72f * i) * Mathf.Deg2Rad;
                v[i] = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            }
            return v;
        }

        static void PentPrism(MeshBuilder mb, float radius, float height)
        {
            var lo = Pentagon(radius, 0f);
            var hi = Pentagon(radius, height);
            for (int i = 0; i < 5; i++)
            {
                int j = (i + 1) % 5;
                mb.Quad(lo[i], lo[j], hi[j], hi[i], D);
            }
            PentCap(mb, height, radius, true);
        }

        static void PentCap(MeshBuilder mb, float y, float radius, bool up)
        {
            var v = Pentagon(radius, y);
            Vector3 c = new Vector3(0f, y, 0f);
            Vector2 uc = Vector2.zero;
            for (int i = 0; i < 5; i++)
            {
                Vector3 a = v[i], b = v[(i + 1) % 5];
                Vector2 ua = new Vector2(a.x, a.z) * D;
                Vector2 ub = new Vector2(b.x, b.z) * D;
                if (up) mb.Tri(c, a, b, uc, ua, ub);
                else mb.Tri(c, b, a, uc, ub, ua);
            }
        }

        /// <summary>
        /// Upward disc mapped so the sigil texture lands on it exactly once.
        /// <paramref name="uvSpan"/> below 1 samples only the middle of the texture,
        /// which is how a brazier gets a glow instead of a shrunken floor sigil.
        /// </summary>
        static void RuneDisc(MeshBuilder mb, float y, float radius, int sides,
                             float cx = 0f, float cz = 0f, float uvSpan = 1f)
        {
            mb.Use(Mat.Rune);
            Vector3 c = new Vector3(cx, y, cz);
            Vector2 uc = new Vector2(0.5f, 0.5f);
            float half = 0.5f * uvSpan;
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector2 u0 = new Vector2(0.5f + Mathf.Cos(a0) * half, 0.5f + Mathf.Sin(a0) * half);
                Vector2 u1 = new Vector2(0.5f + Mathf.Cos(a1) * half, 0.5f + Mathf.Sin(a1) * half);
                mb.Tri(c, p0, p1, uc, u0, u1);
            }
        }

        /// <summary>Where the palace clears ground for itself, in metres from its centre.</summary>
        public static float ClearRadius(Spec s) { return s.radius + 11f; }
    }
}
