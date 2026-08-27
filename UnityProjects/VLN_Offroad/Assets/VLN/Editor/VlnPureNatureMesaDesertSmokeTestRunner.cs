using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnPureNatureMesaDesertSmokeTestRunner
    {
        const string DemoScenePath = "Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity";
        const string PrefabsScenePath = "Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Prefabs.unity";
        const string AssetRoot = "Assets/BK/PureNature_MesaDesert";

        public static void OpenForManualReview()
        {
            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Debug.Log("VLN_PURE_NATURE_MESA_DESERT_OPENED_FOR_MANUAL_REVIEW " + DemoScenePath);
        }

        public static void Run()
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, "vln_pure_nature_mesa_desert_result.txt");

            if (!File.Exists(Path.Combine(Application.dataPath, "BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity")))
            {
                File.WriteAllText(resultPath, "success=0\nmissing_scene=1\n");
                Debug.LogError("VLN_PURE_NATURE_MESA_DESERT_MISSING_SCENE");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);

            var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            var colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
            var bounds = CalculateSceneBounds(terrains, renderers);
            int missingMaterialSlots = CountMissingMaterialSlots(renderers);
            int internalErrorMaterials = CountInternalErrorMaterials(renderers);

            SaveExistingCameraView(logRoot, cameras, "scene_camera");
            SaveView(logRoot, "overview", bounds.center + new Vector3(bounds.extents.x * 0.45f, Mathf.Max(bounds.extents.y * 2.0f, 160f), -bounds.extents.z * 0.70f), bounds.center, 45f, bounds.size.magnitude * 3f);
            SaveView(logRoot, "route_like", bounds.center + new Vector3(-bounds.extents.x * 0.35f, Mathf.Max(bounds.extents.y * 0.55f, 18f), -bounds.extents.z * 0.45f), bounds.center + new Vector3(bounds.extents.x * 0.20f, 0f, bounds.extents.z * 0.20f), 40f, bounds.size.magnitude * 2.5f);
            SaveView(logRoot, "top_layout", bounds.center + new Vector3(0f, Mathf.Max(bounds.size.x, bounds.size.z) * 1.15f, 0f), bounds.center, 55f, bounds.size.magnitude * 3f);

            int sceneCount = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/BK/PureNature_MesaDesert/Scenes" }).Length;
            int prefabCount = AssetDatabase.FindAssets("t:Prefab", new[] { AssetRoot }).Length;
            int modelCount = AssetDatabase.FindAssets("t:Model", new[] { AssetRoot }).Length;
            int materialCount = AssetDatabase.FindAssets("t:Material", new[] { AssetRoot }).Length;
            int textureCount = AssetDatabase.FindAssets("t:Texture", new[] { AssetRoot }).Length;
            int terrainLayerCount = AssetDatabase.FindAssets("t:TerrainLayer", new[] { AssetRoot }).Length;

            bool pass = terrains.Length > 0 && renderers.Length > 25 && cameras.Length > 0 && missingMaterialSlots == 0 && internalErrorMaterials == 0;

            File.WriteAllText(resultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "stage=pure_nature_mesa_desert_visual_import\n" +
                $"demo_scene_path={DemoScenePath}\n" +
                $"prefabs_scene_path={PrefabsScenePath}\n" +
                $"scene_count={sceneCount}\n" +
                $"terrain_count={terrains.Length}\n" +
                $"camera_count={cameras.Length}\n" +
                $"light_count={lights.Length}\n" +
                $"renderer_count={renderers.Length}\n" +
                $"collider_count={colliders.Length}\n" +
                $"prefab_count={prefabCount}\n" +
                $"model_count={modelCount}\n" +
                $"material_count={materialCount}\n" +
                $"texture_count={textureCount}\n" +
                $"terrain_layer_count={terrainLayerCount}\n" +
                $"missing_material_slots={missingMaterialSlots}\n" +
                $"internal_error_materials={internalErrorMaterials}\n" +
                $"scene_bounds_center={bounds.center.x:F2},{bounds.center.y:F2},{bounds.center.z:F2}\n" +
                $"scene_bounds_size={bounds.size.x:F2},{bounds.size.y:F2},{bounds.size.z:F2}\n" +
                $"scene_camera_screenshot={Path.Combine(logRoot, "vln_pure_nature_mesa_desert_scene_camera.png")}\n" +
                $"overview_screenshot={Path.Combine(logRoot, "vln_pure_nature_mesa_desert_overview.png")}\n" +
                $"route_like_screenshot={Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_like.png")}\n" +
                $"top_layout_screenshot={Path.Combine(logRoot, "vln_pure_nature_mesa_desert_top_layout.png")}\n" +
                $"finished={DateTime.UtcNow:O}\n" +
                $"success={(pass ? 1 : 0)}\n");

            Debug.Log($"VLN_PURE_NATURE_MESA_DESERT_RESULT {resultPath}");
            EditorApplication.Exit(pass ? 0 : 1);
        }

        static Bounds CalculateSceneBounds(Terrain[] terrains, Renderer[] renderers)
        {
            bool initialized = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            foreach (var terrain in terrains)
            {
                var size = terrain.terrainData != null ? terrain.terrainData.size : Vector3.one;
                var tb = new Bounds(terrain.transform.position + size * 0.5f, size);
                if (!initialized)
                {
                    bounds = tb;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(tb);
                }
            }

            foreach (var renderer in renderers)
            {
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized ? bounds : new Bounds(Vector3.zero, new Vector3(100f, 50f, 100f));
        }

        static int CountMissingMaterialSlots(Renderer[] renderers)
        {
            int count = 0;
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        static int CountInternalErrorMaterials(Renderer[] renderers)
        {
            int count = 0;
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null && material.shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        static void SaveExistingCameraView(string logRoot, Camera[] cameras, string viewName)
        {
            if (cameras.Length == 0)
            {
                return;
            }
            RenderCameraToPng(cameras[0], Path.Combine(logRoot, $"vln_pure_nature_mesa_desert_{viewName}.png"), 1280, 720);
        }

        static void SaveView(string logRoot, string viewName, Vector3 position, Vector3 target, float fov, float farClip)
        {
            string path = Path.Combine(logRoot, $"vln_pure_nature_mesa_desert_{viewName}.png");
            var cameraObject = new GameObject($"MesaDesert_{viewName}_ScreenshotCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = Mathf.Max(farClip, 1000f);
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
