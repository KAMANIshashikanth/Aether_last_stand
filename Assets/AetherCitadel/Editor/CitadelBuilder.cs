using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aether.Citadel
{
    /// <summary>Entry points: bake the kit of parts, assemble the town, dress the desert.</summary>
    public class CitadelBuilder : EditorWindow
    {
        public const string Root = "Assets/AetherCitadel";

        int _seed = 20260905;
        bool _ground = true;
        bool _colliders = true;
        bool _regenTextures;
        bool _savePrefab = true;
        bool _environment = true;
        float _storm = 0.55f;

        [MenuItem("Tools/Aether/Citadel Builder")]
        public static void Open()
        {
            var w = GetWindow<CitadelBuilder>(false, "Citadel Builder", true);
            w.minSize = new Vector2(340f, 330f);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Aether - Desert Citadel", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake the kit first (materials + modular prefabs), then build the city.\n" +
                "Everything lands under " + Root + ".", MessageType.None);

            EditorGUILayout.Space();
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _ground = EditorGUILayout.Toggle("Dune terrain", _ground);
            _colliders = EditorGUILayout.Toggle("Mesh colliders", _colliders);
            _savePrefab = EditorGUILayout.Toggle("Save city prefab", _savePrefab);
            _regenTextures = EditorGUILayout.Toggle("Re-bake textures", _regenTextures);

            EditorGUILayout.Space();
            _environment = EditorGUILayout.Toggle("Desert environment", _environment);
            using (new EditorGUI.DisabledScope(!_environment))
                _storm = EditorGUILayout.Slider("Sandstorm", _storm, 0f, 1f);
            EditorGUILayout.LabelField("   0 = clear haze,  1 = full blowing storm", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            if (GUILayout.Button("1 - Bake Kit Pieces", GUILayout.Height(28f)))
                BuildKit(_regenTextures, _colliders);

            if (GUILayout.Button("2 - Build Full Citadel", GUILayout.Height(36f)))
                BuildCity(_seed, _ground, _colliders, _savePrefab, _regenTextures, _environment, _storm);

            if (GUILayout.Button("3 - Desert Environment Only", GUILayout.Height(26f)))
            {
                CitadelEnvironment.Build(Root, _storm, true, _regenTextures);
                Debug.Log("[Citadel] Desert environment rebuilt at storm " + _storm.ToString("0.00"));
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Export selection to OBJ"))
                    ObjExport.ExportSelection();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Randomise the seed for a different street plan.", EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------
        //  menu items
        // ------------------------------------------------------------------

        [MenuItem("Tools/Aether/Citadel/Bake Kit Pieces")]
        public static void MenuKit() { BuildKit(false, true); }

        [MenuItem("Tools/Aether/Citadel/Build Full Citadel")]
        public static void MenuCity() { BuildCity(20260905, true, true, true, false, true, 0.55f); }

        [MenuItem("Tools/Aether/Citadel/Build Desert Environment")]
        public static void MenuEnv() { CitadelEnvironment.Build(Root, 0.55f, true, false); }

        [MenuItem("Tools/Aether/Citadel/Export Selection to OBJ")]
        public static void MenuObj() { ObjExport.ExportSelection(); }

        // ------------------------------------------------------------------
        //  kit
        // ------------------------------------------------------------------

        public static void BuildKit(bool regenTextures, bool colliders)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Citadel", "Baking textures and materials", 0.1f);
                var palette = CitadelTextures.BuildPalette(Root, regenTextures);
                CitadelTextures.EnsureFolder(Root + "/Meshes");
                CitadelTextures.EnsureFolder(Root + "/Prefabs");

                var curtain = WallSpec.Curtain();
                var inner = WallSpec.Inner();
                var compound = WallSpec.Compound();

                var pieces = new List<KeyValuePair<string, System.Action<MeshBuilder>>>();

                pieces.Add(Piece("Wall_Curtain_20m", mb => Pieces.Wall(mb, new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), curtain, true, true)));
                pieces.Add(Piece("Wall_Curtain_10m", mb => Pieces.Wall(mb, new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f), curtain, true, true)));
                pieces.Add(Piece("Wall_Inner_20m", mb => Pieces.Wall(mb, new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), inner, true, true)));
                pieces.Add(Piece("Wall_Compound_12m", mb => Pieces.Wall(mb, new Vector3(-6f, 0f, 0f), new Vector3(6f, 0f, 0f), compound, true, true)));

                pieces.Add(Piece("Gatehouse", mb =>
                {
                    Pieces.GateWall(mb, new Vector3(-12f, 0f, 0f), new Vector3(12f, 0f, 0f), curtain, 6.5f, 5.5f);
                    Pieces.SquareTower(mb, new Vector3(-12f, 0f, 0f), 0f, 10f, 16f, curtain);
                    Pieces.SquareTower(mb, new Vector3(12f, 0f, 0f), 0f, 10f, 16f, curtain);
                }));

                pieces.Add(Piece("Tower_Square_Large", mb => Pieces.SquareTower(mb, Vector3.zero, 0f, 11f, 16.5f, curtain)));
                pieces.Add(Piece("Tower_Square_Small", mb => Pieces.SquareTower(mb, Vector3.zero, 0f, 7f, 10.5f, inner)));
                pieces.Add(Piece("Tower_Round_Great", mb => Pieces.RoundTower(mb, Vector3.zero, 10f, 33f, curtain, 20)));
                pieces.Add(Piece("Tower_Round_Small", mb => Pieces.RoundTower(mb, Vector3.zero, 6f, 18f, curtain, 16)));
                pieces.Add(Piece("Turret", mb => Pieces.Turret(mb, Vector3.zero, 2.1f, 4.5f, curtain)));
                pieces.Add(Piece("Stairs_WallWalk", mb => Pieces.Stairs(mb, Vector3.zero, 0f, 3.2f, curtain.height, curtain.height * 1.7f)));

                pieces.Add(Piece("House_Flat_A", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(7f, 6f), 2, HouseStyle.Flat, new System.Random(11), Mat.Plaster)));
                pieces.Add(Piece("House_Flat_B", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(8.5f, 7f), 3, HouseStyle.Flat, new System.Random(12), Mat.PlasterWarm)));
                pieces.Add(Piece("House_Flat_C", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(6f, 5.5f), 1, HouseStyle.Flat, new System.Random(13), Mat.PlasterPale)));
                pieces.Add(Piece("House_Adobe", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(6.5f, 5.5f), 2, HouseStyle.Flat, new System.Random(18), Mat.MudBrick)));
                pieces.Add(Piece("House_Pitched_A", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(7.5f, 6f), 2, HouseStyle.Pitched, new System.Random(14), Mat.Plaster)));
                pieces.Add(Piece("House_Pitched_B", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(6f, 5f), 1, HouseStyle.Pitched, new System.Random(15), Mat.PlasterWarm)));
                pieces.Add(Piece("House_Tower", mb => Pieces.House(mb, Vector3.zero, 0f, new Vector2(5.5f, 5.5f), 3, HouseStyle.Tower, new System.Random(16), Mat.Plaster)));
                pieces.Add(Piece("Hall_Domed", mb => Pieces.DomedHall(mb, Vector3.zero, 0f, new Vector2(17f, 13f), 9.5f, 5.2f, new System.Random(17), Mat.PlasterPale)));
                pieces.Add(Piece("Minaret", mb => Pieces.Minaret(mb, Vector3.zero, 1.7f, 19f)));

                pieces.Add(Piece("Ruin_House_A", mb => Pieces.Ruin(mb, Vector3.zero, 0f, new Vector2(7f, 6f), 3.6f, new System.Random(31), Mat.MudBrick)));
                pieces.Add(Piece("Ruin_House_B", mb => Pieces.Ruin(mb, Vector3.zero, 0f, new Vector2(9f, 6.5f), 2.4f, new System.Random(32), Mat.StoneBlock)));
                pieces.Add(Piece("Rubble_Pile", mb => Pieces.RubblePile(mb, Vector3.zero, 3.2f, 14, new System.Random(33), Mat.MudBrick)));
                pieces.Add(Piece("Tent_Caravan", mb => Pieces.Tent(mb, Vector3.zero, 0f, 5f, 6.5f, 2.9f)));
                pieces.Add(Piece("Sand_Mound", mb => Pieces.SandMound(mb, Vector3.zero, 5f, 1.6f, new System.Random(34))));

                pieces.Add(Piece("Rock_A", mb => Pieces.Rock(mb, Vector3.zero, 2.2f, 1.4f, new System.Random(21))));
                pieces.Add(Piece("Rock_B", mb => Pieces.Rock(mb, Vector3.zero, 1.2f, 0.7f, new System.Random(22))));
                pieces.Add(Piece("Dune_Tile_120m", mb => Pieces.Dunes(mb, 120f, 40, 0f, 3)));

                for (int i = 0; i < pieces.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Citadel kit", pieces[i].Key, 0.1f + 0.9f * i / pieces.Count);
                    var mb = new MeshBuilder();
                    pieces[i].Value(mb);
                    SavePrefab(pieces[i].Key, mb, palette, colliders);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Citadel] Baked " + pieces.Count + " kit prefabs into " + Root + "/Prefabs");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static KeyValuePair<string, System.Action<MeshBuilder>> Piece(string name, System.Action<MeshBuilder> build)
        {
            return new KeyValuePair<string, System.Action<MeshBuilder>>(name, build);
        }

        static void SavePrefab(string name, MeshBuilder mb, Material[] palette, bool colliders)
        {
            Material[] mats;
            var mesh = mb.Bake("SM_" + name, palette, out mats);
            string meshPath = Root + "/Meshes/SM_" + name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("P_" + name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (colliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;

            string prefabPath = Root + "/Prefabs/P_" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------
        //  city
        // ------------------------------------------------------------------

        public static GameObject BuildCity(int seed, bool ground, bool colliders, bool savePrefab,
                                           bool regenTextures, bool environment, float storm)
        {
            GameObject root = null;
            try
            {
                EditorUtility.DisplayProgressBar("Citadel", "Materials", 0.05f);
                var palette = CitadelTextures.BuildPalette(Root, regenTextures);
                CitadelTextures.EnsureFolder(Root + "/Meshes");
                CitadelTextures.EnsureFolder(Root + "/Prefabs");

                EditorUtility.DisplayProgressBar("Citadel", "Generating geometry", 0.2f);
                var result = CitadelLayout.Build(seed, ground);

                var existing = GameObject.Find("Aether_Citadel");
                if (existing != null) Object.DestroyImmediate(existing);

                root = new GameObject("Aether_Citadel");
                int tris = 0;

                for (int i = 0; i < result.chunks.Count; i++)
                {
                    var chunk = result.chunks[i];
                    EditorUtility.DisplayProgressBar("Citadel", "Baking " + chunk.name, 0.2f + 0.7f * i / result.chunks.Count);
                    if (chunk.mb.TriangleCount == 0) continue;
                    tris += chunk.mb.TriangleCount;

                    Material[] mats;
                    var mesh = chunk.mb.Bake("SM_City_" + chunk.name, palette, out mats);
                    string meshPath = Root + "/Meshes/SM_City_" + chunk.name + ".asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);

                    var go = new GameObject(chunk.name);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = mats;
                    if (colliders) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic |
                                                               StaticEditorFlags.OccluderStatic |
                                                               StaticEditorFlags.OccludeeStatic |
                                                               StaticEditorFlags.ContributeGI);
                }

                AssetDatabase.SaveAssets();

                if (savePrefab)
                {
                    string prefabPath = Root + "/Prefabs/P_Aether_Citadel.prefab";
                    PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
                }

                if (environment)
                {
                    EditorUtility.DisplayProgressBar("Citadel", "Desert environment", 0.95f);
                    CitadelEnvironment.Build(Root, storm, true, regenTextures);
                }

                AssetDatabase.Refresh();
                Selection.activeGameObject = root;
                Debug.Log(string.Format(
                    "[Citadel] Built {0} houses, {1} towers, {2} ruins, {3} chunks, {4:n0} triangles. Seed {5}.",
                    result.houses, result.towers, result.ruins, result.chunks.Count, tris, seed));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return root;
        }
    }
}
