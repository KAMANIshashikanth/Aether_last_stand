using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aether.Citadel
{
    /// <summary>Material slots shared by every generated piece.</summary>
    public enum Mat
    {
        StoneBlock  = 0, // rough ashlar - wall bases, tower bodies
        Plaster     = 1, // smooth sandstone render - upper walls, houses
        Wood        = 2, // doors, shutters, window voids, pitched roofs, tent cloth
        RoofFlat    = 3, // beaten-earth roofs / wall walks
        Sand        = 4, // desert floor and drifts
        PlasterWarm = 5, // ochre render, sun-baked
        PlasterPale = 6, // bleached whitewash, flaking
        MudBrick    = 7  // exposed adobe courses - poor quarters and ruins
    }

    /// <summary>
    /// Minimal procedural geometry accumulator.
    /// Winding convention: a face spanned by (u, v) from an origin is visible
    /// from the Cross(v, u) side, which matches Unity clockwise-front triangles.
    /// </summary>
    public class MeshBuilder
    {
        public const int MatCount = 8;

        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int>[] _t = new List<int>[MatCount];
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _m = Matrix4x4.identity;
        int _g;

        public MeshBuilder()
        {
            for (int i = 0; i < MatCount; i++) _t[i] = new List<int>();
        }

        public int VertexCount { get { return _v.Count; } }

        public int TriangleCount
        {
            get { int c = 0; for (int i = 0; i < MatCount; i++) c += _t[i].Count / 3; return c; }
        }

        public MeshBuilder Use(Mat m) { _g = (int)m; return this; }

        // ---- transform stack -------------------------------------------------

        public void Push(Matrix4x4 m) { _stack.Push(_m); _m = _m * m; }

        public void Push(Vector3 pos, float yawDegrees)
        {
            Push(Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDegrees, 0f), Vector3.one));
        }

        public void Pop() { _m = _stack.Pop(); }

        Vector3 P(Vector3 p) { return _m.MultiplyPoint3x4(p); }

        // ---- primitives ------------------------------------------------------

        void Emit(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(p); _n.Add(n); _uv.Add(uv);
        }

        /// <summary>Adds a shared vertex and returns its index. Use with <see cref="Triangle"/>
        /// to build smooth-shaded surfaces such as the dune field.</summary>
        public int Vertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            int i = _v.Count;
            Emit(P(p), _m.MultiplyVector(n).normalized, uv);
            return i;
        }

        /// <summary>Indexed triangle. Visible from the Cross(p2-p0, p1-p0) side, like <see cref="Tri"/>.</summary>
        public void Triangle(int i0, int i1, int i2)
        {
            var t = _t[_g];
            t.Add(i0); t.Add(i2); t.Add(i1);
        }

        /// <summary>Triangle in local space, corners in clockwise-front order.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 wa = P(a), wb = P(b), wc = P(c);
            Vector3 n = Vector3.Cross(wc - wa, wb - wa).normalized;
            int i = _v.Count;
            Emit(wa, n, ua); Emit(wb, n, ub); Emit(wc, n, uc);
            var t = _t[_g];
            // Unity front faces are clockwise: the visible side of (p0,p1,p2) is
            // (p1-p0) x (p2-p0), so wind a-c-b to face along n.
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
        }

        /// <summary>Quad a-b-c-d around the rim. Visible from the Cross(d-a, b-a) side.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 wa = P(a), wb = P(b), wc = P(c), wd = P(d);
            Vector3 n = Vector3.Cross(wc - wa, wb - wa).normalized;
            if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(wd - wa, wb - wa).normalized;
            int i = _v.Count;
            Emit(wa, n, ua); Emit(wb, n, ub); Emit(wc, n, uc); Emit(wd, n, ud);
            var t = _t[_g];
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        /// <summary>Quad with UVs derived from world edge lengths.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float density, Vector2 uvOffset = default(Vector2))
        {
            float uw = (b - a).magnitude * density;
            float vh = (d - a).magnitude * density;
            float uwTop = (c - d).magnitude * density;
            Quad(a, b, c, d,
                uvOffset,
                uvOffset + new Vector2(uw, 0f),
                uvOffset + new Vector2(uwTop, vh),
                uvOffset + new Vector2(0f, vh));
        }

        /// <summary>Vertical quad whose V follows local Y so masonry courses line up across pieces.</summary>
        public void WallQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float density, float uOffset = 0f)
        {
            float uw = new Vector2(b.x - a.x, b.z - a.z).magnitude * density;
            float uwTop = new Vector2(c.x - d.x, c.z - d.z).magnitude * density;
            Quad(a, b, c, d,
                new Vector2(uOffset, a.y * density),
                new Vector2(uOffset + uw, b.y * density),
                new Vector2(uOffset + uwTop, c.y * density),
                new Vector2(uOffset, d.y * density));
        }

        /// <summary>Planar face from an origin and two edge vectors.</summary>
        public void Face(Vector3 origin, Vector3 u, Vector3 v, float density, Vector2 uvOffset = default(Vector2))
        {
            Quad(origin, origin + u, origin + u + v, origin + v, density, uvOffset);
        }

        /// <summary>Axis-aligned box; pos is the centre of the bottom face.</summary>
        public void Box(Vector3 pos, Vector3 size, float density, bool top = true, bool bottom = false)
        {
            BoxOriented(pos, Vector3.right, Vector3.forward, size, density, top, bottom);
        }

        /// <summary>Box on an arbitrary horizontal frame; size is (width along right, height, depth along fwd).</summary>
        public void BoxOriented(Vector3 pos, Vector3 right, Vector3 fwd, Vector3 size, float density, bool top = true, bool bottom = false)
        {
            Vector3 r = right.normalized * (size.x * 0.5f);
            Vector3 f = fwd.normalized * (size.z * 0.5f);
            Vector3 up = Vector3.up * size.y;

            Vector3 b0 = pos - r - f;
            Vector3 b1 = pos + r - f;
            Vector3 b2 = pos + r + f;
            Vector3 b3 = pos - r + f;

            WallQuad(b0, b1, b1 + up, b0 + up, density);
            WallQuad(b1, b2, b2 + up, b1 + up, density);
            WallQuad(b2, b3, b3 + up, b2 + up, density);
            WallQuad(b3, b0, b0 + up, b3 + up, density);
            if (top) Quad(b0 + up, b1 + up, b2 + up, b3 + up, density);
            if (bottom) Quad(b3, b2, b1, b0, density);
        }

        /// <summary>Box with a battered profile: the top face is inset relative to the base.</summary>
        public void TaperedBox(Vector3 pos, Vector3 right, Vector3 fwd, Vector2 baseSize, Vector2 topSize, float height, float density, bool top = true)
        {
            Vector3 rn = right.normalized, fn = fwd.normalized;
            Vector3 rb = rn * (baseSize.x * 0.5f), fb = fn * (baseSize.y * 0.5f);
            Vector3 rt = rn * (topSize.x * 0.5f), ft = fn * (topSize.y * 0.5f);
            Vector3 up = Vector3.up * height;

            Vector3 b0 = pos - rb - fb, b1 = pos + rb - fb, b2 = pos + rb + fb, b3 = pos - rb + fb;
            Vector3 t0 = pos - rt - ft + up, t1 = pos + rt - ft + up, t2 = pos + rt + ft + up, t3 = pos - rt + ft + up;

            WallQuad(b0, b1, t1, t0, density);
            WallQuad(b1, b2, t2, t1, density);
            WallQuad(b2, b3, t3, t2, density);
            WallQuad(b3, b0, t0, t3, density);
            if (top) Quad(t0, t1, t2, t3, density);
        }

        /// <summary>N-sided prism; pos is the centre of the bottom cap.</summary>
        public void Cylinder(Vector3 pos, float rBottom, float rTop, float height, int sides, float density,
                             bool capTop = true, bool capBottom = false, float angleOffset = 0f)
        {
            var bot = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = angleOffset + Mathf.PI * 2f * i / sides;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                bot[i] = pos + new Vector3(cs * rBottom, 0f, sn * rBottom);
                top[i] = pos + new Vector3(cs * rTop, height, sn * rTop);
            }
            float circ = Mathf.PI * 2f * Mathf.Max(rBottom, 0.01f) * density;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                float u0 = circ * i / sides, u1 = circ * (i + 1) / sides;
                Quad(bot[i], bot[j], top[j], top[i],
                    new Vector2(u0, bot[i].y * density),
                    new Vector2(u1, bot[j].y * density),
                    new Vector2(u1, top[j].y * density),
                    new Vector2(u0, top[i].y * density));
            }
            if (capTop) Fan(top, pos + Vector3.up * height, density, true);
            if (capBottom) Fan(bot, pos, density, false);
        }

        void Fan(Vector3[] rim, Vector3 centre, float density, bool up)
        {
            for (int i = 0; i < rim.Length; i++)
            {
                int j = (i + 1) % rim.Length;
                Vector2 uc = new Vector2(centre.x, centre.z) * density;
                Vector2 ui = new Vector2(rim[i].x, rim[i].z) * density;
                Vector2 uj = new Vector2(rim[j].x, rim[j].z) * density;
                if (up) Tri(centre, rim[i], rim[j], uc, ui, uj);
                else Tri(centre, rim[j], rim[i], uc, uj, ui);
            }
        }

        /// <summary>Dome cap standing on the XZ plane at pos.</summary>
        public void Hemisphere(Vector3 pos, float radius, float heightScale, int sides, int rings, float density)
        {
            for (int ring = 0; ring < rings; ring++)
            {
                float p0 = Mathf.PI * 0.5f * ring / rings;
                float p1 = Mathf.PI * 0.5f * (ring + 1) / rings;
                float r0 = Mathf.Cos(p0) * radius, y0 = Mathf.Sin(p0) * radius * heightScale;
                float r1 = Mathf.Cos(p1) * radius, y1 = Mathf.Sin(p1) * radius * heightScale;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                    Vector3 b0 = pos + new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0);
                    Vector3 b1 = pos + new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
                    Vector3 t0 = pos + new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1);
                    Vector3 t1 = pos + new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
                    float u0 = radius * 2f * Mathf.PI * i / sides * density;
                    float u1 = radius * 2f * Mathf.PI * (i + 1) / sides * density;
                    float v0 = ring * radius * density, v1 = (ring + 1) * radius * density;
                    if (r1 < 0.01f)
                        Tri(b0, b1, t0, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u0, v1));
                    else
                        Quad(b0, b1, t1, t0, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
                }
            }
        }

        /// <summary>Row of merlons (crenellation teeth) running from a to b.</summary>
        public void Merlons(Vector3 a, Vector3 b, float thickness, float height, float merlonW, float gapW, float density)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.01f) return;
            dir /= len;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float step = merlonW + gapW;
            int count = Mathf.Max(1, Mathf.FloorToInt((len + gapW) / step));
            float used = count * step - gapW;
            float pad = (len - used) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float s = pad + i * step + merlonW * 0.5f;
                BoxOriented(a + dir * s, dir, side, new Vector3(merlonW, height, thickness), density);
            }
        }

        /// <summary>Ring of merlons around a circular tower top.</summary>
        public void MerlonRing(Vector3 centre, float radius, float thickness, float height, int count, float coverage, float density)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * 2f * i / count;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 tangent = Vector3.Cross(Vector3.up, outward);
                float arc = Mathf.PI * 2f * radius / count;
                BoxOriented(centre + outward * radius, tangent, outward,
                    new Vector3(arc * coverage, height, thickness), density);
            }
        }

        /// <summary>Flat dark panel laid on a wall face (window, shutter, door).</summary>
        public void Panel(Vector3 centre, Vector3 right, Vector3 normal, float width, float height, float lift = 0.04f)
        {
            Vector3 rn = right.normalized;
            Vector3 nn = normal.normalized;
            // the quad faces Cross(up, right); flip the tangent if that points the wrong way
            if (Vector3.Dot(Vector3.Cross(Vector3.up, rn), nn) < 0f) rn = -rn;
            Vector3 r = rn * (width * 0.5f);
            Vector3 u = Vector3.up * (height * 0.5f);
            Vector3 o = centre + nn * lift;
            Quad(o - r - u, o + r - u, o + r + u, o - r + u,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }

        // ---- output ----------------------------------------------------------

        public Mesh Bake(string name, Material[] palette, out Material[] materials)
        {
            var used = new List<int>();
            for (int i = 0; i < MatCount; i++) if (_t[i].Count > 0) used.Add(i);
            if (used.Count == 0) used.Add(0);

            var mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = _v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetUVs(0, _uv);
            mesh.subMeshCount = used.Count;
            for (int s = 0; s < used.Count; s++) mesh.SetTriangles(_t[used[s]], s, true);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            materials = new Material[used.Count];
            for (int s = 0; s < used.Count; s++) materials[s] = palette[used[s]];
            return mesh;
        }
    }

    public static class Rng
    {
        public static float F(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
        public static int I(System.Random r, int a, int b) { return r.Next(a, b); }
        public static bool Chance(System.Random r, float p) { return r.NextDouble() < p; }
        public static T Pick<T>(System.Random r, params T[] items) { return items[r.Next(items.Length)]; }
    }
}
