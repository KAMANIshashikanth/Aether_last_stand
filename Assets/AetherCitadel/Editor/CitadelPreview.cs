using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>Renders stills of the citadel for documentation and for checking the build.</summary>
    public static class CitadelPreview
    {
        [MenuItem("Tools/Aether/Citadel/Render Preview")]
        public static void MenuRender()
        {
            string path = EditorUtility.SaveFilePanel("Render preview", "", "citadel_preview.png", "png");
            if (string.IsNullOrEmpty(path)) return;
            if (GameObject.Find("Aether_Citadel") == null)
            {
                EditorUtility.DisplayDialog("Preview", "Build the citadel first.", "OK");
                return;
            }
            Orbit(path, 1920, 1080, 31f, 8f, 380f, new Vector3(0f, 6f, -6f), true);
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Batch entry point: -executeMethod Aether.Citadel.CitadelPreview.BatchRender</summary>
        public static void BatchRender()
        {
            string outPath = ArgValue("-citadelOut") ?? "citadel_preview.png";
            int seed = int.Parse(ArgValue("-citadelSeed") ?? "20260905");
            float storm = float.Parse(ArgValue("-citadelStorm") ?? "0.45");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CitadelBuilder.BuildKit(true, false);
            CitadelBuilder.BuildCity(seed, true, false, true, false, true, storm);

            string stem = Path.ChangeExtension(outPath, null);
            Orbit(outPath, 1920, 1080, 30f, 8f, 400f, new Vector3(0f, 6f, -6f), true);
            Orbit(stem + "_close.png", 1600, 900, 17f, 128f, 150f, new Vector3(10f, 8f, -10f), true);
            Orbit(stem + "_ground.png", 1600, 900, 3.5f, -35f, 118f, new Vector3(-30f, 9f, -60f), true);

            // and the same ground view with the storm cranked up, to check the heavy end
            CitadelEnvironment.Build(CitadelBuilder.Root, 0.9f, true, false);
            Orbit(stem + "_storm.png", 1600, 900, 3.5f, -35f, 118f, new Vector3(-30f, 9f, -60f), true);
            CitadelEnvironment.Build(CitadelBuilder.Root, storm, true, false);

            Debug.Log("[Citadel] batch render complete -> " + outPath);
        }

        static string ArgValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        /// <summary>Renders from an orbit position around <paramref name="focus"/>.</summary>
        public static void Orbit(string path, int width, int height, float pitch, float yaw, float distance,
                                 Vector3 focus, bool useSceneLighting)
        {
            GameObject lightGo = null;
            if (!useSceneLighting)
            {
                lightGo = new GameObject("Preview_Sun");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(46f, -38f, 0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.52f);
                RenderSettings.ambientEquatorColor = new Color(0.32f, 0.30f, 0.28f);
                RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.15f);
                RenderSettings.fog = false;
            }

            var camGo = new GameObject("Preview_Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.16f, 0.17f);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 3000f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            camGo.transform.position = focus - rot * Vector3.forward * distance;
            camGo.transform.rotation = rot;

            // LateUpdate does not run for a one-shot render, so place the storm rig by hand
            foreach (var f in UnityEngine.Object.FindObjectsByType<SandstormFollow>(FindObjectsSortMode.None))
            {
                Vector3 wind = f.windDirection.sqrMagnitude < 0.0001f ? Vector3.forward : f.windDirection.normalized;
                Vector3 rigPos = camGo.transform.position - wind * f.upwindLead;
                rigPos.y = f.height;
                f.transform.position = rigPos;
            }

            // particle systems do not tick during a one-shot render either, so wind them
            // forward by hand or the sandstorm is invisible in the still
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                ps.Simulate(14f, true, true);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            if (lightGo != null) UnityEngine.Object.DestroyImmediate(lightGo);
            Debug.Log("[Citadel] preview written to " + path);
        }
    }
}
