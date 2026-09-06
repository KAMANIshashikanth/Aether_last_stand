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
                                              (sz, c, h, e) => RenderCoat(sz, c, h, SandLight, SandDark, 0.30f, 0.9f, 11), 0.06f);
            mats[(int)Mat.Wood]        = Make(texDir, matDir, "Timber", regenerate, 256, Timber, 0.14f);
            mats[(int)Mat.RoofFlat]    = Make(texDir, matDir, "Roof", regenerate, 512, RoofDust, 0.04f);
            // The floor tiles every 8 m and repeats well over a hundred times across the
            // dune field. Its base map therefore carries nothing finer than ~10 cm and
            // no strong low frequencies either - fine detail would alias into speckle,
            // coarse blotches would read as a grid. Close-up crispness comes from the
            // shared detail map wired up below.
            mats[(int)Mat.Sand]        = Make(texDir, matDir, "Sand", regenerate, 1024, Sand, 0.02f, 0.55f,
                                              false, 1f, true);
            mats[(int)Mat.CityFloor]   = Make(texDir, matDir, "CityFloor", regenerate, 1024, CityFloorTex, 0.07f, 0.8f,
                                              false, 1f, true);
            mats[(int)Mat.PlasterWarm] = Make(texDir, matDir, "PlasterWarm", regenerate, 1024,
                                              (sz, c, h, e) => RenderCoat(sz, c, h, OchreLight, OchreDark, 0.45f, 1.2f, 47), 0.05f);
            mats[(int)Mat.PlasterPale] = Make(texDir, matDir, "PlasterPale", regenerate, 1024,
                                              (sz, c, h, e) => RenderCoat(sz, c, h, PaleLight, PaleDark, 0.55f, 1.4f, 83), 0.07f);
            mats[(int)Mat.MudBrick]    = Make(texDir, matDir, "MudBrick", regenerate, 512, MudBrick, 0.04f, 0.75f);
            mats[(int)Mat.Rune]        = Make(texDir, matDir, "Rune", regenerate, 1024, RuneSigil, 0.30f, 1f, true, 2.4f);
            mats[(int)Mat.Lapis]       = Make(texDir, matDir, "Lapis", regenerate, 512, LapisPanel, 0.42f, 0.9f);

            // one grain sheet shared by both floor materials, tiled 6x inside each base
            // tile so it lands at roughly 1.3 m and mips away to neutral in the distance
            AttachGroundDetail(texDir, mats[(int)Mat.Sand], regenerate, 6f);
            AttachGroundDetail(texDir, mats[(int)Mat.CityFloor], regenerate, 6f);

            AssetDatabase.SaveAssets();
            return mats;
        }

        // ------------------------------------------------------------------
        //  asset plumbing
        // ------------------------------------------------------------------

        delegate void Painter(int size, Color[] albedo, float[] height, Color[] emission);

        static Material Make(string texDir, string matDir, string name, bool regenerate, int size,
                             Painter painter, float smoothness, float bumpScale = 1f,
                             bool emissive = false, float emissionStrength = 1f, bool ground = false)
        {
            // the floor is seen at grazing angles across a hundred tile repeats, where
            // bilinear mip banding and 8x aniso are exactly what read as pixellation
            FilterMode filter = ground ? FilterMode.Trilinear : FilterMode.Bilinear;
            int aniso = ground ? 16 : 8;
            string albedoPath = texDir + "/T_" + name + "_Albedo.png";
            string normalPath = texDir + "/T_" + name + "_Normal.png";
            string emissPath = texDir + "/T_" + name + "_Emission.png";
            string matPath = matDir + "/M_Citadel_" + name + ".mat";

            if (regenerate || !File.Exists(albedoPath))
            {
                var albedo = new Color[size * size];
                var height = new float[size * size];
                var emission = emissive ? new Color[size * size] : null;
                painter(size, albedo, height, emission);
                WritePng(albedoPath, size, albedo, false);
                WritePng(normalPath, size, NormalFromHeight(size, height, 2.2f), true);
                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                Configure(albedoPath, false, size, filter, aniso);
                Configure(normalPath, true, size, filter, aniso);
                if (emissive)
                {
                    WritePng(emissPath, size, emission, false);
                    AssetDatabase.ImportAsset(emissPath, ImportAssetOptions.ForceUpdate);
                    Configure(emissPath, false, size, filter, aniso);
                }
            }

            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var emissTex = emissive ? AssetDatabase.LoadAssetAtPath<Texture2D>(emissPath) : null;

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

            if (emissTex != null)
            {
                SetTex(mat, "_EmissionMap", emissTex);
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.white * emissionStrength);
                // without this the bake ignores emission and the runes read as flat paint
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

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

        static void Configure(string path, bool normalMap, int maxSize = 1024,
                              FilterMode filter = FilterMode.Bilinear, int aniso = 8)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normalMap;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = filter;
            ti.anisoLevel = aniso;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = maxSize;
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

        // The floor palette is deliberately narrow. Dry sand varies by only a few per
        // cent across a dune face, and a wide albedo range on a surface this heavily
        // tiled is what turns the ground into tonal noise instead of sand.
        static readonly Color SandSun = new Color(0.933f, 0.812f, 0.616f);    // lit crest
        static readonly Color SandBase = new Color(0.855f, 0.702f, 0.494f);   // open sand
        static readonly Color SandShade = new Color(0.729f, 0.573f, 0.396f);  // ripple trough
        static readonly Color Gravel = new Color(0.549f, 0.478f, 0.388f);     // loose stones
        static readonly Color FlagLight = new Color(0.855f, 0.753f, 0.588f);  // bleached slab
        static readonly Color FlagDark = new Color(0.678f, 0.561f, 0.404f);
        static readonly Color EarthPack = new Color(0.702f, 0.565f, 0.396f);  // beaten earth

        const float Tau = Mathf.PI * 2f;

        /// <summary>Coursed ashlar: 4 courses per tile, offset every other row, with chipped arrises.</summary>
        static void StoneBlock(int size, Color[] col, float[] hgt, Color[] em)
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

        /// <summary>
        /// Basalt slab carved with a circular sigil. Not a tiling pattern - it is mapped
        /// once across the arena floor disc, so the design is centred in the texture.
        /// </summary>
        static void RuneSigil(int size, Color[] col, float[] hgt, Color[] em)
        {
            Color stone = new Color(0.208f, 0.176f, 0.161f);
            Color stoneLit = new Color(0.310f, 0.259f, 0.227f);
            Color glowCore = new Color(1f, 0.647f, 0.243f);
            Color glowRim = new Color(0.984f, 0.361f, 0.106f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float dx = u - 0.5f, dy = v - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;   // 0 at centre, 1 at edge
                    float ang = Mathf.Atan2(dy, dx);

                    float grain = Fbm(u, v, 26, 4, 205);
                    Color c = Lerp(stone, stoneLit, grain * 0.7f + 0.15f);
                    float h = grain * 0.25f;
                    float glow = 0f;

                    // concentric bands
                    glow = Mathf.Max(glow, Band(r, 0.94f, 0.016f));
                    glow = Mathf.Max(glow, Band(r, 0.88f, 0.008f));
                    glow = Mathf.Max(glow, Band(r, 0.62f, 0.012f));
                    glow = Mathf.Max(glow, Band(r, 0.57f, 0.006f));
                    glow = Mathf.Max(glow, Band(r, 0.24f, 0.010f));

                    // radial spokes between the outer rings
                    if (r > 0.62f && r < 0.88f)
                    {
                        float spoke = Mathf.Abs(Mathf.Sin(ang * 12f));
                        glow = Mathf.Max(glow, Mathf.Clamp01((spoke - 0.985f) * 120f));
                    }

                    // glyph blocks around the outer band
                    if (r > 0.885f && r < 0.938f)
                    {
                        int slot = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 48f);
                        float local = (ang + Mathf.PI) / (Mathf.PI * 2f) * 48f - slot;
                        float bit = Hash(slot, Mathf.FloorToInt((r - 0.885f) * 90f), 17);
                        if (bit > 0.45f && local > 0.22f && local < 0.78f) glow = Mathf.Max(glow, 0.85f);
                    }

                    // inner five-pointed star matching the pentagonal plan
                    float star = Mathf.Abs(Mathf.Cos(ang * 2.5f));
                    glow = Mathf.Max(glow, Band(r, 0.10f + star * 0.30f, 0.009f) * (r > 0.05f ? 1f : 0f));

                    if (r > 1.0f) glow = 0f;
                    glow *= 0.55f + grain * 0.45f;      // carving wear

                    c = Lerp(c, glowRim, Mathf.Clamp01(glow * 0.9f));
                    h -= glow * 0.55f;                   // the sigil is cut into the slab
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                    if (em != null)
                    {
                        float e = Mathf.Clamp01(glow);
                        em[y * size + x] = Lerp(Color.black, glowCore, e * e);
                    }
                }
        }

        /// <summary>Distance to a ring, as a soft 0..1 mask.</summary>
        static float Band(float r, float at, float halfWidth)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(r - at) / halfWidth);
        }

        /// <summary>Lapis panel with a gold chevron border - the palace banners and regalia.</summary>
        static void LapisPanel(int size, Color[] col, float[] hgt, Color[] em)
        {
            Color lapis = new Color(0.129f, 0.216f, 0.451f);
            Color lapisLit = new Color(0.220f, 0.353f, 0.639f);
            Color gold = new Color(0.855f, 0.667f, 0.290f);
            Color goldDim = new Color(0.596f, 0.435f, 0.157f);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float grain = Fbm(u, v, 22, 4, 311);
                    float fleck = Fbm(u, v, 90, 2, 77);

                    // lapis body with pyrite flecking
                    Color c = Lerp(lapis, lapisLit, grain * 0.8f);
                    c = Lerp(c, gold, Mathf.Clamp01((fleck - 0.80f) * 4f) * 0.5f);
                    float h = grain * 0.2f;

                    // gold border framing the panel
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    // SmoothStep(from, to, t) interpolates between from and to - it does not
                    // remap edge out of that range, so the bounds go through InverseLerp
                    float border = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.055f, 0.075f, edge));
                    float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.020f, 0.032f, edge));
                    float frame = border * inner;

                    // gold chevrons running up the centre
                    float chev = Mathf.Abs(((u * 6f) % 1f) - 0.5f) * 2f;
                    float chevBand = Mathf.Clamp01(1f - Mathf.Abs(chev - (v * 3f % 1f)) * 9f);
                    chevBand *= (edge > 0.09f) ? 1f : 0f;

                    float g = Mathf.Clamp01(frame + chevBand * 0.75f);
                    c = Lerp(c, Lerp(goldDim, gold, grain), g);
                    h += g * 0.5f;

                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
        }

        static void MudBrick(int size, Color[] col, float[] hgt, Color[] em)
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
        static void Timber(int size, Color[] col, float[] hgt, Color[] em)
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
        static void RoofDust(int size, Color[] col, float[] hgt, Color[] em)
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

                    // a per-texel threshold here used to punch isolated near-black
                    // pixels into the deck, which reads as dither rather than grit
                    float grit = Fbm(u, v, 64, 2, 7);
                    c *= 0.95f + grit * 0.1f;
                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = n * 0.45f + patch * 0.28f + drift * 0.2f;
                }
            }
        }

        /// <summary>
        /// Open sand: a wandering train of wind ripples, a faint tonal swell and a
        /// sparse gravel scatter. Almost all the relief lives in the normal map - albedo
        /// stays close to a single value, which is what makes it read as a smooth drift
        /// rather than the dense dark speckle it used to be.
        /// </summary>
        static void Sand(int size, Color[] col, float[] hgt, Color[] em)
        {
            const int cells = 9;   // one loose stone every few square metres

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    // broad drift shading - the faintest of tonal swells
                    float broad = Fbm(u, v, 3, 3, 101) - 0.5f;
                    // ~12 cm blobs. Anything finer than this in the albedo is sub-pixel
                    // at normal viewing distance and shimmers instead of reading as grain.
                    float grain = Fbm(u, v, 14, 3, 202) - 0.5f;

                    // Wind ripples at ~30 cm, on integer harmonics so the tile still
                    // wraps. One train dominates and the second runs nearly parallel at
                    // double the frequency; crossing two trains at a wide angle instead
                    // weaves a diamond lattice that reads as basketwork, not as sand.
                    // The warp reaches most of a wavelength, so crests meander and pinch
                    // off rather than ruling the tile in straight lines.
                    float warp = (Fbm(u, v, 4, 4, 303) - 0.5f) * 0.7f;
                    float r1 = Mathf.Sin((13f * u + 23f * v + warp) * Tau);
                    float r2 = Mathf.Sin((25f * u + 45f * v + warp * 1.8f) * Tau);
                    float rip = r1 * 0.8f + r2 * 0.2f;
                    // and they die away altogether over the smoother patches of drift
                    rip *= 0.25f + Fbm(u, v, 3, 2, 311) * 1.15f;
                    // an exponent above one gives narrow crests over broad flat troughs,
                    // the way blown sand actually lies; below one it hard-banded
                    rip = Mathf.Sign(rip) * Mathf.Pow(Mathf.Abs(rip), 1.5f);

                    float tone = 0.5f + broad * 0.34f + rip * 0.15f + grain * 0.16f;
                    Color c = tone < 0.5f
                        ? Lerp(SandShade, SandBase, tone * 2f)
                        : Lerp(SandBase, SandSun, (tone - 0.5f) * 2f);

                    float h = rip * 0.5f + broad * 0.3f + grain * 0.34f;

                    // Loose gravel: sparse, blunt-edged and only a little darker than the
                    // sand around it. A dense scatter of near-black specks is precisely
                    // what made the old floor look dithered at any distance.
                    int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
                    if (Hash(cx, cy, 55) > 0.86f)
                    {
                        float jx = (cx + 0.2f + Hash(cx, cy, 77) * 0.6f) / cells;
                        float jy = (cy + 0.2f + Hash(cx, cy, 99) * 0.6f) / cells;
                        float rad = (0.07f + Hash(cx, cy, 123) * 0.1f) / cells;
                        float dist = Mathf.Sqrt((u - jx) * (u - jx) + (v - jy) * (v - jy));
                        // Mathf.SmoothStep takes the interpolant last, not a value to
                        // remap. Feeding it the raw distance - as this did - returns ~1
                        // for every texel in the cell, which filled a quarter of all
                        // cells solid and laid a grid of dark 19 cm squares across the
                        // whole desert. That was the pixellation.
                        float m = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rad * 0.45f, rad, dist));
                        Color stone = Lerp(Gravel, SandShade, Hash(cx, cy, 31) * 0.5f);
                        c = Lerp(c, stone, m * 0.5f);
                        h = Mathf.Lerp(h, 0.5f, m);
                    }

                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            }
        }

        /// <summary>
        /// The floor of the town: ancient flagstones, a fifth of them gone back to
        /// beaten earth, the rest crazed and sunk unevenly with sand filling the joints
        /// and drowning whole patches. Laid on the same 8 m tile as <see cref="Sand"/>
        /// and drawn from the same palette, so the two read as one surface where they
        /// meet under the curtain footing.
        /// </summary>
        static void CityFloorTex(int size, Color[] col, float[] hgt, Color[] em)
        {
            const int cells = 7;        // ~1.1 m slabs
            const float joint = 0.11f;  // joint width, relative to a cell

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    float dust = Fbm(u, v, 22, 4, 401) - 0.5f;
                    float wear = Fbm(u, v, 5, 3, 409);        // where the traffic runs

                    // Worley cells give irregular polygonal slabs; the gap between the
                    // nearest two feature points is the joint, so no two stones match.
                    int sx, sy;
                    float f1, f2;
                    Worley(u, v, cells, 419, out f1, out f2, out sx, out sy);
                    float edge = (f2 - f1) * cells;

                    float face = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / joint));
                    // worn round rather than square-arrised, so the joint reads as a
                    // wide soft hollow in the relief
                    float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / (joint * 2.6f)));

                    float slabGrain = Fbm(u, v, 18, 3, 437 + sx * 3 + sy * 7);
                    float tone = 0.88f + Hash(sx, sy, 443) * 0.2f;
                    Color slab = Lerp(FlagDark, FlagLight, 0.42f + slabGrain * 0.34f) * tone;

                    // finer and fainter: a heavy network reads as cracked granite
                    float crack = Mathf.Pow(1f - Mathf.Abs(Fbm(u, v, 11, 4, 449) * 2f - 1f), 30f);
                    slab = Lerp(slab, slab * 0.87f, Mathf.Clamp01(crack * 1.3f));

                    Color earth = Lerp(EarthPack * 0.88f, EarthPack, 0.35f + dust + wear * 0.4f);
                    Color filled = Lerp(SandShade, SandBase, 0.4f + dust * 1.2f);

                    Color c;
                    float h;
                    if (Hash(sx, sy, 431) > 0.22f)
                    {
                        float sink = Hash(sx, sy, 457) * 0.35f;
                        c = Lerp(filled, slab, face);
                        h = bevel * (0.62f - sink) + slabGrain * 0.12f - crack * 0.22f;
                    }
                    else
                    {
                        // the slab has gone - bare packed earth, joints silted level
                        c = Lerp(earth, filled, face * 0.35f);
                        h = 0.1f + dust * 0.5f;
                    }

                    // Blown sand lying over the paving. Held to sub-metre features on
                    // purpose: a broad drift mask would repeat visibly across the tile,
                    // so the large drifts are geometry (see Pieces.SandMound) instead.
                    float drift = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.46f, 0.76f, Fbm(u, v, 6, 4, 463)));
                    Color sand = Lerp(SandBase, SandSun, 0.35f + dust * 1.4f);
                    c = Lerp(c, sand, drift * 0.85f);
                    h = Mathf.Lerp(h, 0.42f + dust * 0.35f, drift * 0.75f);

                    // polished pale where the feet and the sun have got at it longest
                    c = Lerp(c, c * 1.06f, Mathf.Clamp01(wear - 0.55f) * 1.5f);

                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            }
        }

        /// <summary>
        /// Tileable Worley cells. <paramref name="f1"/> and <paramref name="f2"/> come
        /// back as the distances to the nearest and next-nearest feature point - their
        /// difference draws a clean joint between irregular slabs - along with the id of
        /// the cell owning the texel, so a stone can be tinted and sunk as a unit.
        /// </summary>
        static void Worley(float u, float v, int cells, int seed,
                           out float f1, out float f2, out int ownerX, out int ownerY)
        {
            int gx = Mathf.FloorToInt(u * cells), gy = Mathf.FloorToInt(v * cells);
            f1 = f2 = 4f;
            ownerX = gx; ownerY = gy;

            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int nx = gx + i, ny = gy + j;
                    // the site is offset from the unwrapped cell but seeded from the
                    // wrapped one, which is what keeps the pattern seamless
                    int wx = Wrap(nx, cells), wy = Wrap(ny, cells);
                    float px = (nx + 0.18f + Hash(wx, wy, seed) * 0.64f) / cells;
                    float py = (ny + 0.18f + Hash(wx, wy, seed + 37) * 0.64f) / cells;
                    float dx = u - px, dy = v - py;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < f1) { f2 = f1; f1 = d; ownerX = wx; ownerY = wy; }
                    else if (d < f2) f2 = d;
                }
        }

        /// <summary>
        /// Fine grain shared by both floor materials, wired into URP's detail slot at
        /// <paramref name="repeats"/> tiles per base tile. The base maps hold nothing
        /// smaller than about 10 cm, so this is what keeps the ground from going smooth
        /// and plasticky underfoot; being centred on neutral grey, it fades out through
        /// the mip chain instead of shimmering off into the distance.
        /// </summary>
        static void AttachGroundDetail(string texDir, Material mat, bool regenerate, float repeats)
        {
            if (mat == null || !mat.HasProperty("_DetailAlbedoMap")) return;

            const int size = 512;
            string albedoPath = texDir + "/T_GroundDetail_Albedo.png";
            string normalPath = texDir + "/T_GroundDetail_Normal.png";

            if (regenerate || !File.Exists(albedoPath))
            {
                var albedo = new Color[size * size];
                var height = new float[size * size];
                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size;
                        float grit = Fbm(u, v, 40, 4, 601) - 0.5f;
                        float micro = Mathf.Sin((23f * u + 11f * v) * Tau) * 0.5f
                                    + Mathf.Sin((9f * u - 19f * v) * Tau) * 0.3f;
                        // mid grey is the neutral value for a multiply-by-two detail map
                        float t = 0.5f + grit * 0.14f + micro * 0.035f;
                        albedo[y * size + x] = new Color(t, t * 0.995f, t * 0.985f, 1f);
                        height[y * size + x] = grit * 0.7f + micro * 0.5f;
                    }
                }
                WritePng(albedoPath, size, albedo, false);
                WritePng(normalPath, size, NormalFromHeight(size, height, 1.6f), true);
                AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                Configure(albedoPath, false, size, FilterMode.Trilinear, 16);
                Configure(normalPath, true, size, FilterMode.Trilinear, 16);
            }

            SetTex(mat, "_DetailAlbedoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            SetTex(mat, "_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(repeats, repeats));
            // the scale stays at 1: the deviation is baked into the sheet, and a value
            // off 1 sends URP down its _DETAIL_SCALED variant instead
            if (mat.HasProperty("_DetailAlbedoMapScale")) mat.SetFloat("_DetailAlbedoMapScale", 1f);
            if (mat.HasProperty("_DetailNormalMapScale")) mat.SetFloat("_DetailNormalMapScale", 0.9f);
            mat.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(mat);
        }
    }
}
