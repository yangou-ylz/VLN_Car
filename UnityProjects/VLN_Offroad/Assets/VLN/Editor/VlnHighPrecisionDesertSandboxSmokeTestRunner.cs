using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnHighPrecisionDesertSandboxSmokeTestRunner
    {
        public static void Run()
        {
            VlnHighPrecisionDesertSandboxProjectSetup.BuildHighPrecisionDesertSandbox();
            EditorSceneManager.OpenScene(VlnHighPrecisionDesertSandboxProjectSetup.ScenePath, OpenSceneMode.Single);

            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, "vln_high_precision_desert_sandbox_result.txt");

            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            int boulderCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_Boulder01_");
            int rockRidgeCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_RockRidge_");
            int rockClusterCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_RockCluster_");
            int pebbleCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_Pebble_");
            int shrubCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_DryShrub_");
            int treeCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_QuiverTree_");
            int dryWashCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_DryWash_");
            int routeSurfaceDetailCount = GameObject.FindObjectsOfType<GameObject>().LengthByName("HighPrecisionDesert_RouteGravel_");
            int colliderCount = UnityEngine.Object.FindObjectsOfType<Collider>().Length;
            int textureCount = CountAssets("Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven", "t:Texture");
            int modelCount = CountAssets("Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven", "t:Model");

            SaveView(logRoot, "overview", GroundPoint(-210f, -470f, 96f), GroundPoint(20f, -140f, 8f), 45f);
            SaveView(logRoot, "route", GroundPoint(-82f, -390f, 8f), GroundPoint(35f, -245f, 1.6f), 40f);
            SaveView(logRoot, "cliff", GroundPoint(210f, 145f, 82f), GroundPoint(410f, 315f, 20f), 38f);
            SaveView(logRoot, "vegetation", GroundPoint(-128f, -330f, 10f), GroundPoint(-62f, -245f, 1.8f), 35f);
            SaveView(logRoot, "boulder", GroundPoint(72f, -255f, 10f), GroundPoint(96f, -210f, 2.0f), 34f);
            SaveView(logRoot, "top_layout", new Vector3(0f, 1180f, 0f), new Vector3(0f, 0f, 0f), 55f);

            File.WriteAllText(resultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "stage=high_precision_desert_sandbox_visual_only\n" +
                $"scene_path={VlnHighPrecisionDesertSandboxProjectSetup.ScenePath}\n" +
                $"terrain_present={(terrain != null ? 1 : 0)}\n" +
                $"terrain_size_m={VlnHighPrecisionDesertSandboxProjectSetup.TerrainSize:F1}\n" +
                $"terrain_area_m2={VlnHighPrecisionDesertSandboxProjectSetup.TerrainAreaSquareMeters:F0}\n" +
                $"polyhaven_texture_count={textureCount}\n" +
                $"polyhaven_model_count={modelCount}\n" +
                $"boulder_count={boulderCount}\n" +
                $"rock_ridge_count={rockRidgeCount}\n" +
                $"rock_cluster_count={rockClusterCount}\n" +
                $"pebble_count={pebbleCount}\n" +
                $"dry_shrub_count={shrubCount}\n" +
                $"quiver_tree_count={treeCount}\n" +
                $"dry_wash_count={dryWashCount}\n" +
                $"route_surface_detail_count={routeSurfaceDetailCount}\n" +
                $"collider_count={colliderCount}\n" +
                $"overview_screenshot={Path.Combine(logRoot, "vln_high_precision_desert_sandbox_overview.png")}\n" +
                $"route_screenshot={Path.Combine(logRoot, "vln_high_precision_desert_sandbox_route.png")}\n" +
                $"cliff_screenshot={Path.Combine(logRoot, "vln_high_precision_desert_sandbox_cliff.png")}\n" +
                $"vegetation_screenshot={Path.Combine(logRoot, "vln_high_precision_desert_sandbox_vegetation.png")}\n" +
                $"boulder_screenshot={Path.Combine(logRoot, "vln_high_precision_desert_sandbox_boulder.png")}\n" +
                $"top_layout_screenshot={Path.Combine(logRoot, "vln_high_precision_desert_sandbox_top_layout.png")}\n" +
                $"finished={DateTime.UtcNow:O}\n" +
                "success=1\n");

            Debug.Log($"VLN_HIGH_PRECISION_DESERT_SANDBOX_RESULT {resultPath}");
            EditorApplication.Exit(0);
        }

        static int CountAssets(string folder, string filter)
        {
            return AssetDatabase.FindAssets(filter, new[] { folder }).Length;
        }

        static int LengthByName(this GameObject[] objects, string prefix)
        {
            int count = 0;
            foreach (var obj in objects)
            {
                if (obj.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        static void SaveView(string logRoot, string viewName, Vector3 position, Vector3 target, float fov)
        {
            string path = Path.Combine(logRoot, $"vln_high_precision_desert_sandbox_{viewName}.png");
            var cameraObject = new GameObject($"HighPrecisionDesert_{viewName}_ScreenshotCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 2600f;
                camera.fieldOfView = fov;
                cameraObject.transform.position = position;
                cameraObject.transform.LookAt(target);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static Vector3 GroundPoint(float x, float z, float aboveGround)
        {
            return new Vector3(x, VlnHighPrecisionDesertSandboxProjectSetup.TerrainWorldY(x, z) + aboveGround, z);
        }

        static void RenderCameraToPng(Camera camera, string path, int width, int height)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
