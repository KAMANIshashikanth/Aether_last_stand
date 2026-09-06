using UnityEditor;
using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>
    /// Sets the scene up as an open desert: hazy sky, warm low sun, distance haze
    /// and a three-layer sandstorm that follows the viewer.
    /// </summary>
    public static class CitadelEnvironment
    {
        public const string RootName = "Aether_Desert_Environment";

        /// <summary>Direction the wind blows towards. Sand drifts are banked on the faces turned into it.</summary>
        public static readonly Vector3 Wind = new Vector3(0.72f, 0f, 0.69f).normalized;

        public static GameObject Build(string root, float storm, bool sandstorm, bool regenerate)
        {
            storm = Mathf.Clamp01(storm);
            string texDir = root + "/Textures";
            string matDir = root + "/Materials";
            CitadelTextures.EnsureFolder(texDir);
            CitadelTextures.EnsureFolder(matDir);

            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var rig = new GameObject(RootName);

            var sun = BuildSun(rig.transform, storm);
            ApplySky(matDir, storm, sun);
            ApplyAtmosphere(storm);

            if (sandstorm) BuildStorm(rig.transform, texDir, matDir, storm, regenerate);

            AssetDatabase.SaveAssets();
            return rig;
        }

        // ------------------------------------------------------------------

        static Light BuildSun(Transform parent, float storm)
        {
            // reuse the scene's existing directional light if there is one, so we do
            // not end up with two suns lighting the city from different angles
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }

            if (sun == null)
            {
                var go = new GameObject("Sun_Desert");
                go.transform.SetParent(parent, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(42f, 33f, storm), -38f, 0f);
            sun.color = Color.Lerp(new Color(1f, 0.925f, 0.804f), new Color(1f, 0.761f, 0.486f), storm);
            sun.intensity = Mathf.Lerp(1.45f, 0.85f, storm);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = Mathf.Lerp(0.85f, 0.45f, storm);
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;
            return sun;
        }

        static void ApplySky(string matDir, float storm, Light sun)
        {
            string path = matDir + "/M_Citadel_Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;

            if (sky == null)
            {
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.shader = shader;
            sky.SetFloat("_SunDisk", 1f);                              // simple disc, mostly veiled anyway
            sky.SetFloat("_SunSize", Mathf.Lerp(0.05f, 0.14f, storm));
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.1f, 2.05f, storm));
            sky.SetColor("_SkyTint", Color.Lerp(new Color(0.678f, 0.565f, 0.443f), new Color(0.788f, 0.533f, 0.302f), storm));
            // match the haze exactly: from an aerial camera you look DOWN past the terrain edge
            // into the skybox ground hemisphere, and any mismatch shows as a dark band
            sky.SetColor("_GroundColor", FogColour(storm));
            sky.SetFloat("_Exposure", Mathf.Lerp(1.3f, 1.02f, storm));
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
        }

        static Color FogColour(float storm)
        {
            return Color.Lerp(new Color(0.796f, 0.655f, 0.463f), new Color(0.745f, 0.529f, 0.322f), storm);
        }

        static void ApplyAtmosphere(float storm)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColour(storm);
            // exponential-squared falls off hard: keep this low or a 400 m aerial shot
            // washes out completely long before a ground-level view looks stormy
            RenderSettings.fogDensity = Mathf.Lerp(0.0004f, 0.0055f, storm * storm);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.557f, 0.514f, 0.494f), new Color(0.647f, 0.506f, 0.345f), storm);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.471f, 0.373f, 0.286f), new Color(0.553f, 0.412f, 0.278f), storm);
            RenderSettings.ambientGroundColor = new Color(0.306f, 0.212f, 0.141f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.6f;
        }

        // ------------------------------------------------------------------
        //  sandstorm
        // ------------------------------------------------------------------

        static void BuildStorm(Transform parent, string texDir, string matDir, float storm, bool regenerate)
        {
            var puff = CitadelTextures.BakeSprite(texDir, "DustPuff", regenerate, 256, PuffPixel);
            var streak = CitadelTextures.BakeSprite(texDir, "SandStreak", regenerate, 256, StreakPixel);

            var puffMat = ParticleMaterial(matDir, "DustPuff", puff, new Color(0.882f, 0.706f, 0.502f, 1f));
            var streakMat = ParticleMaterial(matDir, "SandStreak", streak, new Color(0.898f, 0.741f, 0.545f, 1f));

            var rig = new GameObject("Sandstorm");
            rig.transform.SetParent(parent, false);
            var follow = rig.AddComponent<SandstormFollow>();
            follow.windDirection = Wind;
            follow.upwindLead = 70f;
            follow.snap = 12f;
            follow.height = 0f;

            // 1. slow banks of haze drifting across the whole view
            Layer(rig.transform, "Dust_Banks", puffMat,
                  size: new Vector2(80f, 180f), lifetime: new Vector2(16f, 26f),
                  speed: new Vector2(9f, 16f), rate: Mathf.Lerp(2f, 12f, storm),
                  box: new Vector3(400f, 55f, 400f), y: 28f,
                  alpha: Mathf.Lerp(0.04f, 0.11f, storm), turbulence: 1.2f, stretch: false);

            // 2. sheets of sand skimming the ground
            Layer(rig.transform, "Sand_Sheets", puffMat,
                  size: new Vector2(18f, 48f), lifetime: new Vector2(7f, 13f),
                  speed: new Vector2(20f, 32f), rate: Mathf.Lerp(7f, 48f, storm),
                  box: new Vector3(230f, 14f, 230f), y: 5f,
                  alpha: Mathf.Lerp(0.07f, 0.22f, storm), turbulence: 3.5f, stretch: false);

            // 3. grains streaking past the viewer - this is what sells the motion, so it
            //    needs a tight box and a high rate or it just disappears into the haze
            Layer(rig.transform, "Sand_Grains", streakMat,
                  size: new Vector2(0.4f, 1.8f), lifetime: new Vector2(2.5f, 4.5f),
                  speed: new Vector2(28f, 46f), rate: Mathf.Lerp(70f, 750f, storm),
                  box: new Vector3(95f, 24f, 95f), y: 9f,
                  alpha: Mathf.Lerp(0.20f, 0.50f, storm), turbulence: 6f, stretch: true);
        }

        static void Layer(Transform parent, string name, Material mat, Vector2 size, Vector2 lifetime,
                          Vector2 speed, float rate, Vector3 box, float y, float alpha, float turbulence, bool stretch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = 12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, alpha);
            main.maxParticles = Mathf.CeilToInt(rate * lifetime.y) + 32;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            shape.rotation = Vector3.zero;
            // emit from the upwind half so particles blow through the scene
            shape.position = -Wind * (box.x * 0.28f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(Wind.x * speed.y * 0.55f);
            vel.z = new ParticleSystem.MinMaxCurve(Wind.z * speed.y * 0.55f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.6f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = turbulence;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.55f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var life = ps.colorOverLifetime;
            life.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f)
                });
            life.color = new ParticleSystem.MinMaxGradient(g);

            var rot = ps.rotationOverLifetime;
            rot.enabled = !stretch;
            rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretch)
            {
                r.velocityScale = 0.055f;
                r.lengthScale = 3.0f;
            }
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
        }

        static Material ParticleMaterial(string matDir, string name, Texture2D tex, Color tint)
        {
            string path = matDir + "/M_Citadel_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;

            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);

            // transparent, alpha blended, no depth write
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------
        //  sprite painters
        // ------------------------------------------------------------------

        static Color PuffPixel(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float falloff = Mathf.Clamp01(1f - d);
            falloff = falloff * falloff * (3f - 2f * falloff);

            // break the circle up so the billboards do not read as soft discs
            float n = CitadelTextures.Noise(u, v, 4, 4, 17) * 0.75f + CitadelTextures.Noise(u, v, 12, 3, 33) * 0.35f;
            float a = Mathf.Clamp01(falloff * (0.35f + n));
            a *= Mathf.Clamp01(1f - Mathf.Pow(d, 3f));
            return new Color(1f, 1f, 1f, a);
        }

        static Color StreakPixel(float u, float v)
        {
            float dx = (u - 0.5f) * 2f;      // long axis
            float dy = (v - 0.5f) * 4.5f;    // pinched across
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - d);
            a = a * a;
            a *= 0.55f + CitadelTextures.Noise(u * 2f, v, 8, 3, 91) * 0.8f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }
    }
}
