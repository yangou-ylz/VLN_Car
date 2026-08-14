using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnOffroadAssetCandidateProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity";
        const string SourceScenePath = VlnOffroadTerrainProjectSetup.ScenePath;
        const string ModelRoot = "Assets/VLN/ExternalAssets/KenneyNatureKit/Models";
        const string AssetRootName = "KenneyNatureKit_ImportedOffroadProps";
        const float TerrainSize = 80f;
        const float TerrainHeight = 7f;

        static readonly string[] TreeModels =
        {
            "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD",
            "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundA", "tree_pineRoundC",
            "tree_oak", "tree_oak_dark", "tree_default", "tree_detailed", "tree_cone", "tree_fat", "tree_small", "tree_thin"
        };

        static readonly string[] RockModels =
        {
            "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_largeE", "rock_largeF",
            "rock_tallA", "rock_tallB", "rock_tallC", "rock_tallD",
            "stone_largeA", "stone_largeB", "stone_largeC", "stone_largeD", "stone_tallA", "stone_tallB", "stone_tallC"
        };

        static readonly string[] BushModels =
        {
            "plant_bush", "plant_bushDetailed", "plant_bushLarge", "plant_bushSmall",
            "grass", "grass_large", "grass_leafs", "grass_leafsLarge", "stump_round", "stump_square", "stump_old"
        };

        static readonly string[] FenceModels =
        {
            "fence_simple", "fence_simpleCenter", "fence_corner", "fence_gate", "fence_planks", "fence_planksDouble"
        };

        [MenuItem("VLN/Build Offroad Asset Candidate Scene")]
        public static void BuildAssetCandidateScene()
        {
            VlnRos2ProjectSetup.ConfigureRos2();
            AssetDatabase.Refresh();

            if (!File.Exists(SourceScenePath))
            {
                VlnOffroadTerrainProjectSetup.BuildOffroadTerrainScene();
            }

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            RemoveIfExists(AssetRootName);
            RemoveIfExists("VLN_OffroadTerrain_SmokeTestController");
            RemoveIfExists("VLN_OffroadAssetCandidate_SmokeTestController");

            var root = new GameObject(AssetRootName);
            root.isStatic = true;

            AddImportedForest(root.transform);
            AddImportedRockFields(root.transform);
            AddImportedRoadsideDetails(root.transform);

            var controller = new GameObject("VLN_OffroadAssetCandidate_SmokeTestController");
            controller.AddComponent<VlnOffroadAssetCandidateSmokeTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_OFFROAD_ASSET_CANDIDATE_SETUP saved scene at {ScenePath}");
        }

        static void AddImportedForest(Transform root)
        {
            var random = new System.Random(20260814);
            int index = 0;

            for (int i = 0; i < 44; i++)
            {
                float z = -36f + i * 1.7f;
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * Mathf.Lerp(8f, 24f, (float)random.NextDouble());
                float scale = Mathf.Lerp(1.55f, 2.55f, (float)random.NextDouble());
                float yaw = Mathf.Lerp(0f, 360f, (float)random.NextDouble());
                string model = TreeModels[index++ % TreeModels.Length];
                InstantiateModel(root, model, $"Forest_{i:00}", GroundPosition(x, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale, true);
            }

            for (int i = 0; i < 16; i++)
            {
                float z = -34f + i * 4.4f;
                float x = i % 2 == 0 ? -28f + (i % 3) * 2.2f : 28f - (i % 3) * 2.1f;
                float scale = 2.0f + (i % 4) * 0.25f;
                string model = TreeModels[(i * 3) % TreeModels.Length];
                InstantiateModel(root, model, $"BackgroundTree_{i:00}", GroundPosition(x, z), Quaternion.Euler(0f, i * 37f, 0f), Vector3.one * scale, true);
            }
        }

        static void AddImportedRockFields(Transform root)
        {
            var rockPositions = new[]
            {
                new Vector2(-5.2f, -19f), new Vector2(6.4f, -15f), new Vector2(-7.8f, -8f), new Vector2(8.4f, -5f),
                new Vector2(-9.2f, 2f), new Vector2(7.6f, 5.5f), new Vector2(-6.5f, 14f), new Vector2(8.8f, 18f),
                new Vector2(-12.5f, 24f), new Vector2(11.0f, 29f), new Vector2(-18.0f, -27f), new Vector2(18.5f, -24f),
                new Vector2(-21.0f, 9f), new Vector2(22.0f, 12f), new Vector2(-16.0f, 33f), new Vector2(16.0f, 35f)
            };

            for (int i = 0; i < rockPositions.Length; i++)
            {
                Vector2 p = rockPositions[i];
                float scale = 1.25f + (i % 5) * 0.23f;
                string model = RockModels[(i * 2) % RockModels.Length];
                InstantiateModel(root, model, $"RockObstacle_{i:00}", GroundPosition(p.x, p.y), Quaternion.Euler(0f, i * 29f, 0f), Vector3.one * scale, true);
            }

            InstantiateModel(root, "log_large", "FallenLog_LeftTrail", GroundPosition(-4.8f, 22.5f), Quaternion.Euler(0f, 67f, 0f), Vector3.one * 1.9f, true);
            InstantiateModel(root, "log_stack", "LogStack_RightCamp", GroundPosition(9.0f, -21f), Quaternion.Euler(0f, -18f, 0f), Vector3.one * 1.4f, true);
            InstantiateModel(root, "campfire_stones", "Campfire_Stones", GroundPosition(11.8f, -20.0f), Quaternion.identity, Vector3.one * 1.2f, true);
            InstantiateModel(root, "campfire_logs", "Campfire_Logs", GroundPosition(11.8f, -20.0f), Quaternion.Euler(0f, 25f, 0f), Vector3.one * 1.2f, true);
        }

        static void AddImportedRoadsideDetails(Transform root)
        {
            for (int i = 0; i < 28; i++)
            {
                float z = -34f + i * 2.55f;
                float x = (i % 2 == 0 ? -1f : 1f) * (4.8f + (i % 4) * 0.8f);
                string model = BushModels[(i * 5) % BushModels.Length];
                float scale = 1.0f + (i % 5) * 0.16f;
                InstantiateModel(root, model, $"RoadsideDetail_{i:00}", GroundPosition(x, z), Quaternion.Euler(0f, i * 41f, 0f), Vector3.one * scale, false);
            }

            for (int i = 0; i < 12; i++)
            {
                float z = -30f + i * 5.2f;
                float x = i % 2 == 0 ? -6.9f : 6.9f;
                string model = FenceModels[i % FenceModels.Length];
                float yaw = x < 0f ? 0f : 180f;
                InstantiateModel(root, model, $"TrailFence_{i:00}", GroundPosition(x, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one * 1.25f, true);
            }

            InstantiateModel(root, "bridge_woodNarrow", "WoodBridge_OverTrailA", GroundPosition(0.0f, -7.5f), Quaternion.Euler(0f, 0f, 0f), new Vector3(1.55f, 1.2f, 1.8f), true);
            InstantiateModel(root, "bridge_center_wood", "WoodBridge_CenterVisual", GroundPosition(0.0f, -6.0f), Quaternion.Euler(0f, 0f, 0f), new Vector3(1.4f, 1.15f, 1.4f), true);
            InstantiateModel(root, "sign", "TrailSign_NearStart", GroundPosition(4.9f, -27.5f), Quaternion.Euler(0f, -25f, 0f), Vector3.one * 1.35f, true);
            InstantiateModel(root, "tent_smallOpen", "CampTent_RightSide", GroundPosition(14.0f, -18.5f), Quaternion.Euler(0f, -38f, 0f), Vector3.one * 1.35f, true);
        }

        static GameObject InstantiateModel(Transform root, string modelName, string instanceName, Vector3 position, Quaternion rotation, Vector3 scale, bool addCollider)
        {
            string path = $"{ModelRoot}/{modelName}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"VLN_OFFROAD_ASSET_CANDIDATE missing model {path}");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = "Kenney_" + modelName + "_" + instanceName;
            instance.transform.SetParent(root, true);
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = scale;
            instance.isStatic = true;
            SetLayerRecursively(instance, 0);

            if (addCollider)
            {
                AddMeshCollidersRecursively(instance);
            }

            return instance;
        }

        static void AddMeshCollidersRecursively(GameObject root)
        {
            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                var collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        static Vector3 GroundPosition(float x, float z)
        {
            return new Vector3(x, TerrainWorldY(x, z), z);
        }

        static float TerrainWorldY(float x, float z)
        {
            return NormalizedTerrainHeight(x, z) * TerrainHeight;
        }

        static float NormalizedTerrainHeight(float x, float z)
        {
            float ridge = 0.034f * Mathf.Sin(0.24f * x + 0.31f * Mathf.Sin(0.12f * z));
            float roll = 0.025f * Mathf.Cos(0.19f * z - 0.11f * x);
            float longSlope = 0.032f * Mathf.InverseLerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z);
            float baseHeight = 0.17f + ridge + roll + longSlope;

            float roadBlend = Mathf.Clamp01(1f - Mathf.Abs(x) / 4.4f);
            roadBlend = roadBlend * roadBlend * (3f - 2f * roadBlend);
            float roadHeight = 0.175f + longSlope * 0.75f + 0.006f * Mathf.Sin(0.18f * z);
            return Mathf.Clamp01(Mathf.Lerp(baseHeight, roadHeight, roadBlend * 0.9f));
        }

        static void RemoveIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }
    }
}
