using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>Bakes the sandstone / masonry / adobe / timber texture set and the URP materials that use it.</summary>
    public static class CitadelTextures
    {
        public static Material[] BuildPalette(string root, bool regenerate)
        {
            string texDir = root + "/Textures";
            string matDir = root + "/Materials";
            EnsureFolder(texDir);
            EnsureFolder(matDir);

            var mats = new Material[MeshBuilder.MatCount];

            mats[(int)Mat.StoneBlock]  = Make(texDir, matDir, "Stone", regenerate, 1024, StoneBlock, 0.10f, 0.7f);
            mats[(int)Mat.Plaster]     = Make(texDir, matDir, "Plaster", regenerate, 1024,
                                              (sz, c, h) => RenderCoat(sz, c, h, SandLight, SandDark, 0.30f, 0.9f, 11), 0.06f);
            mats[(int)Mat.Wood]        = Make(texDir, matDir, "Timber", regenerate, 256, Timber, 0.14f);
            mats[(int)Mat.RoofFlat]    = Make(texDir, matDir, "Roof", regenerate, 512, RoofDust, 0.04f);
            // the ground tiles ~50x, so keep its relief soft or the repeat reads as a checkerboard
            mats[(int)Mat.Sand]        = Make(texDir, matDir, "Sand", regenerate, 1024, Sand, 0.03f, 0.45f);
            mats[(int)Mat.PlasterWarm] = Make(texDir, matDir, "PlasterWarm", regenerate, 1024,
                                              (sz, c, h) => RenderCoat(sz, c, h, OchreLight, OchreDark, 0.45f, 1.2f, 47), 0.05f);
            mats[(int)Mat.PlasterPale] = Make(texDir, matDir, "PlasterPale", regenerate, 1024,
                                              (sz, c, h) => RenderCoat(sz, c, h, PaleLight, PaleDark, 0.55f, 1.4f, 83), 0.07f);
            mats[(int)Mat.MudBrick]    = Make(texDir, matDir, "MudBrick", regenerate, 512, MudBrick, 0.04f, 0.75f);

            AssetDatabase.SaveAssets();
            return mats;
        }

        // ------------------------------------------------------------------
        //  asset plumbing
        // ------------------------------------------------------------------

        delegate void Painter(int size, Color[] albedo, float[] height);

        static Material Make(string texDir, string matDir, string name, bool regenerate, int size,
                             Painter painter, float smoothness, float bumpScale = 1f)
        {
            string albedoPath = texDir + "/T_" + name + "_Albedo.png";
            string normalPath = texDir + "/T_" + name + "_Normal.png";
            string matPath = matDir + "/M_Citadel_" + name + ".mat";

            if (regenerate || !File.Exists(albedoPath))
            {
                var albedo = new Color[size * size];
                var height = new float[size * size];
                painter(size, albedo, height);
                WritePng(albedoPath, size, albedo, false);
                WritePng(normalPath, size, NormalFromHeight(size, height, 2.2f), true);
                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                Configure(albedoPath, false);
                Configure(normalPath, true);
            }

            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(FindShader());
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = FindShader();
            SetTex(mat, "_BaseMap", albedoTex);
            SetTex(mat, "_MainTex", albedoTex);
            SetTex(mat, "_BumpMap", normalTex);
            if (normalTex != null) mat.EnableKeyword("_NORMALMAP");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Shader FindShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static void SetTex(Material m, string prop, Texture2D t)
        {
            if (t != null && m.HasProperty(prop)) m.SetTexture(prop, t);
        }

        static void WritePng(string path, int size, Color[] pixels, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void Configure(string path, bool normalMap)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normalMap;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            ti.SaveAndReimport();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Color[] NormalFromHeight(int size, float[] h, float strength)
        {
            var outp = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = h[Idx(size, x - 1, y)], r = h[Idx(size, x + 1, y)];
                    float d = h[Idx(size, x, y - 1)], u = h[Idx(size, x, y + 1)];
                    Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    outp[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return outp;
        }

        static int Idx(int size, int x, int y)
        {
            x = ((x % size) + size) % size;
            y = ((y % size) + size) % size;
            return y * size + x;
        }

        // ------------------------------------------------------------------
        //  seamless value noise
        // ------------------------------------------------------------------

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h = h ^ (h >> 16);
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        static int Wrap(int v, int p) { return ((v % p) + p) % p; }

        static float Value(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(Wrap(xi, period), Wrap(yi, period), seed);
            float b = Hash(Wrap(xi + 1, period), Wrap(yi, period), seed);
            float c = Hash(Wrap(xi, period), Wrap(yi + 1, period), seed);
            float d = Hash(Wrap(xi + 1, period), Wrap(yi + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Tileable fractal noise in 0..1 over a unit square.</summary>
        static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int p = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(u * p, v * p, p, seed + i * 71) * amp;
                norm += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / norm;
        }

        static Color Lerp(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }

        /// <summary>Tileable fractal noise, exposed for the sandstorm sprites.</summary>
        public static float Noise(float u, float v, int basePeriod, int octaves, int seed)
        {
            return Fbm(u, v, basePeriod, octaves, seed);
        }

        /// <summary>Bakes an RGBA sprite (particle billboards and the like) and returns the imported texture.</summary>
        public static Texture2D BakeSprite(string texDir, string name, bool regenerate, int size,
                                           System.Func<float, float, Color> paint)
        {
            EnsureFolder(texDir);
            string path = texDir + "/T_" + name + ".png";
            if (regenerate || !File.Exists(path))
            {
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        px[y * size + x] = paint((x + 0.5f) / size, (y + 0.5f) / size);
                WritePng(path, size, px, false);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = 256;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------
        //  painters
        // ------------------------------------------------------------------

        static readonly Color SandLight = new Color(0.886f, 0.694f, 0.443f);
        static readonly Color SandDark = new Color(0.663f, 0.451f, 0.243f);
        static readonly Color StoneLight = new Color(0.827f, 0.663f, 0.463f);
        static readonly Color StoneDark = new Color(0.647f, 0.478f, 0.310f);
        static readonly Color Mortar = new Color(0.573f, 0.427f, 0.294f);
        static readonly Color TimberBase = new Color(0.263f, 0.176f, 0.110f);
        static readonly Color OchreLight = new Color(0.812f, 0.565f, 0.325f);
        static readonly Color OchreDark = new Color(0.573f, 0.353f, 0.184f);
        static readonly Color PaleLight = new Color(0.910f, 0.804f, 0.643f);
        static readonly Color PaleDark = new Color(0.729f, 0.580f, 0.427f);
        static readonly Color AdobeLight = new Color(0.769f, 0.525f, 0.310f);
        static readonly Color AdobeDark = new Color(0.529f, 0.337f, 0.184f);

        /// <summary>Coursed ashlar: 4 courses per tile, offset every other row, with chipped arrises.</summary>
        static void StoneBlock(int size, Color[] col, float[] hgt)
        {
            const int rows = 4, cols = 6;
            float rowH = 1f / rows, colW = 1f / cols;
            float joint = 2.5f / size;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                int row = Mathf.FloorToInt(v / rowH);
                float vLocal = v / rowH - row;
                float offset = (row % 2 == 0) ? 0f : 0.5f;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float uShift = u + offset * colW;
                    int colIdx = Mathf.FloorToInt(uShift / colW);
                    float uLocal = uShift / colW - colIdx;

                    // wobble the joint so the courses are not machine-straight
                    float wobble = (Fbm(u, v, 24, 3, 5) - 0.5f) * 0.012f;
                    float edgeU = Mathf.Min(uLocal, 1f - uLocal) * colW + wobble;
                    float edgeV = Mathf.Min(vLocal, 1f - vLocal) * rowH + wobble;
                    float edge = Mathf.Min(edgeU, edgeV);
                    float face = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / joint));

                    // keep the per-block tone step small: a strong one turns the wall into a
                    // checkerboard of flat squares that reads as pixellation at any distance
                    float tone = Hash(colIdx, row, 17) * 0.12f + 0.91f;
                    float grain = Fbm(u, v, 20, 4, 3);
                    float coarse = Fbm(u, v, 5, 3, 91);

                    // chipped arrises, driven by continuous noise so no hard square edges appear
                    float chip = Mathf.Clamp01((Fbm(u, v, 44, 3, 61) - 0.60f) * 3.5f);
                    chip *= 1f - Mathf.SmoothStep(0f, 1f, edge / (joint * 3.5f));

                    Color stone = Lerp(StoneDark, StoneLight, grain * 0.5f + coarse * 0.5f) * tone;
                    Color dirt = Mortar * (0.8f + grain * 0.35f);
                    Color c = Lerp(dirt, stone, face);
                    c = Lerp(c, dirt * 0.92f, chip);

                    // weathering wash running down the face
                    float streak = Fbm(u * 7f, v * 0.3f, 16, 3, 71);
                    c = Lerp(c, c * 0.92f, Mathf.Clamp01(streak - 0.57f) * 1.3f);
                    c.a = 1f;
                    col[y * size + x] = c;

                    float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / (joint * 2.2f)));
                    hgt[y * size + x] = bevel * 0.42f + grain * 0.30f + coarse * 0.14f - chip * 0.30f;
                }
            }
        }

        /// <summary>
        /// Lime or mud render over adobe. <paramref name="flake"/> controls how much of the
        /// coat has fallen away to reveal the brick beneath, <paramref name="crackAmt"/>
        /// how heavily the surface is crazed.
        /// </summary>
        static void RenderCoat(int size, Color[] col, float[] hgt, Color light, Color dark,
                               float flake, float crackAmt, int seed)
        {
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    float fine = Fbm(u, v, 28, 4, seed);
                    float blotch = Fbm(u, v, 4, 3, seed + 13);
                    Color coat = Lerp(dark, light, 0.32f + blotch * 0.52f + fine * 0.22f);

                    // brick showing through where the render has spalled off
                    float patch = Fbm(u, v, 6, 4, seed + 29);
                    float exposed = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.68f - flake * 0.16f, 0.86f - flake * 0.16f, patch));
                    float brickH;
                    Color brick = AdobeAt(u, v, seed + 5, out brickH);
                    // only a partial reveal - a full blend reads as dark mould rather than
                    // sun-bleached masonry showing through a thin coat
                    Color c = Lerp(coat, brick, exposed * 0.38f);

                    // crazing
                    float crack = Mathf.Pow(1f - Mathf.Abs(Fbm(u, v, 9, 4, seed + 41) * 2f - 1f), 26f) * crackAmt;
                    c = Lerp(c, c * 0.82f, Mathf.Clamp01(crack));

                    // dust washing down the wall
                    float streak = Fbm(u * 6f, v * 0.32f, 14, 3, seed + 67);
                    c = Lerp(c, c * 0.94f, Mathf.Clamp01(streak - 0.56f) * 1.4f);

                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = Mathf.Lerp(fine * 0.32f + blotch * 0.2f, brickH - 0.25f, exposed * 0.45f) - crack * 0.3f;
                }
            }
        }

        /// <summary>Adobe brick courses - also the layer showing under flaking render.</summary>
        static Color AdobeAt(float u, float v, int seed, out float height)
        {
            const int rows = 9, cols = 7;
            float rowH = 1f / rows, colW = 1f / cols;
            int row = Mathf.FloorToInt(v / rowH);
            float vLocal = v / rowH - row;
            float offset = (row % 2 == 0) ? 0f : 0.5f;
            float uShift = u + offset * colW;
            int colIdx = Mathf.FloorToInt(uShift / colW);
            float uLocal = uShift / colW - colIdx;

            float wobble = (Fbm(u, v, 30, 3, seed + 2) - 0.5f) * 0.0012f;
            float edge = Mathf.Min(Mathf.Min(uLocal, 1f - uLocal) * colW,
                                   Mathf.Min(vLocal, 1f - vLocal) * rowH) + wobble;
            float face = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.0068f));

            float tone = Hash(colIdx, row, seed) * 0.11f + 0.92f;
            float grain = Fbm(u, v, 26, 3, seed + 8);
            Color brick = Lerp(AdobeDark, AdobeLight, grain * 0.6f + 0.2f) * tone;
            Color joint = AdobeDark * 0.90f;
            height = face * 0.8f + grain * 0.2f;
            Color c = Lerp(joint, brick, face);
            c.a = 1f;
            return c;
        }

        static void MudBrick(int size, Color[] col, float[] hgt)
        {
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float h;
                    Color c = AdobeAt(u, v, 5, out h);
                    float streak = Fbm(u * 5f, v * 0.35f, 14, 3, 121);
                    c = Lerp(c, c * 0.93f, Mathf.Clamp01(streak - 0.55f) * 1.4f);
                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            }
        }

        /// <summary>Dark boards - doors, shutters, window voids, pitched roofs, tent cloth.</summary>
        static void Timber(int size, Color[] col, float[] hgt)
        {
            const int planks = 6;
            float pw = 1f / planks;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                int p = Mathf.FloorToInt(v / pw);
                float vLocal = v / pw - p;
                float tone = 0.75f + Hash(p, 3, 29) * 0.5f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float grain = Fbm(u * 3f, v * 22f, 16, 3, 5);
                    float gap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(vLocal, 1f - vLocal) / (2.5f / size / pw)));
                    Color wood = TimberBase * tone * (0.8f + grain * 0.45f);
                    Color c = Lerp(TimberBase * 0.35f, wood, gap);
                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = gap * 0.7f + grain * 0.3f;
                }
            }
        }

        /// <summary>Beaten-earth roof deck and wall walk, with sand collecting in the hollows.</summary>
        static void RoofDust(int size, Color[] col, float[] hgt)
        {
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float n = Fbm(u, v, 20, 4, 61);
                    float patch = Fbm(u, v, 5, 3, 13);
                    Color c = Lerp(SandDark * 0.74f, SandLight * 0.80f, 0.3f + patch * 0.5f + n * 0.25f);

                    // wind-blown sand settling on the deck
                    float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.80f, Fbm(u, v, 7, 3, 137)));
                    c = Lerp(c, SandLight * (0.95f + n * 0.1f), drift * 0.65f);

                    float grit = Hash(x, y, 7);
                    if (grit > 0.985f) c *= 0.62f;
                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = n * 0.45f + patch * 0.28f + drift * 0.2f;
                }
            }
        }

        /// <summary>Desert floor: wind ripples across the prevailing wind, plus pebble scatter.</summary>
        static void Sand(int size, Color[] col, float[] hgt)
        {
            const int cells = 42;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    // keep the low frequencies weak: this tiles ~50x across the terrain
                    // and any large blotch turns into an obvious repeating grid
                    float dunes = Fbm(u, v, 8, 4, 101);
                    float grain = Fbm(u, v, 40, 3, 202);

                    // ripples: integer harmonics so the pattern still tiles seamlessly
                    float warp = (Fbm(u, v, 6, 3, 303) - 0.5f) * 0.09f;
                    float rip = Mathf.Sin((18f * u + 9f * v + warp) * Mathf.PI * 2f);
                    rip = Mathf.Sign(rip) * Mathf.Pow(Mathf.Abs(rip), 0.65f);

                    Color c = Lerp(SandDark, SandLight, 0.44f + dunes * 0.2f + grain * 0.16f + rip * 0.12f);
                    float h = dunes * 0.25f + grain * 0.2f + rip * 0.55f;

                    int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
                    if (Hash(cx, cy, 55) > 0.74f)
                    {
                        float jx = (cx + Hash(cx, cy, 77)) / cells;
                        float jy = (cy + Hash(cx, cy, 99)) / cells;
                        float rad = (0.14f + Hash(cx, cy, 123) * 0.26f) / cells;
                        float dist = Mathf.Sqrt((u - jx) * (u - jx) + (v - jy) * (v - jy));
                        float m = 1f - Mathf.SmoothStep(rad * 0.6f, rad, dist);
                        c = Lerp(c, new Color(0.322f, 0.294f, 0.239f) * (0.7f + Hash(cx, cy, 31) * 0.6f), m);
                        h = Mathf.Lerp(h, 0.85f, m);
                    }

                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            }
        }
    }
}
