using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnOffroadScoutWheelGroundTopgearVisualSmokeTestRunner
    {
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string TopgearVisualRootName = "ScoutWheelGround_TopgearV2Visual";

        public static void Run()
        {
            VlnOffroadScoutWheelGroundCandidateProjectSetup.BuildScoutWheelGroundCandidateScene();
            EditorSceneManager.OpenScene(VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath);

            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, "vln_offroad_scout_wheel_ground_topgear_visual_result.txt");

            var physicsRoot = GameObject.Find(PhysicsRootName);
            var visualRoot = GameObject.Find(VisualRootName);
            var topgear = GameObject.Find(TopgearVisualRootName);
            if (physicsRoot == null || topgear == null)
            {
                File.WriteAllText(resultPath,
                    $"started={DateTime.UtcNow:O}\n" +
                    $"physics_root_present={(physicsRoot != null ? 1 : 0)}\n" +
                    $"topgear_visual_present={(topgear != null ? 1 : 0)}\n" +
                    "success=0\n");
                EditorApplication.Exit(1);
                return;
            }

            Bounds worldBounds = CalculateRendererBounds(topgear);
            Bounds vehicleLocalBounds = CalculateRendererBoundsInLocalFrame(topgear, physicsRoot.transform);

            File.WriteAllText(resultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "stage=topgear_v2_visual_alignment_only\n" +
                $"physics_root_present=1\n" +
                $"topgear_visual_present=1\n" +
                $"topgear_visual_renderer_count={topgear.GetComponentsInChildren<Renderer>(true).Length}\n" +
                $"topgear_visual_collider_count={topgear.GetComponentsInChildren<Collider>(true).Length}\n" +
                $"topgear_visual_rigidbody_count={topgear.GetComponentsInChildren<Rigidbody>(true).Length}\n" +
                $"world_bounds_center_m={FormatVector(worldBounds.center)}\n" +
                $"world_bounds_size_m={FormatVector(worldBounds.size)}\n" +
                $"vehicle_local_bounds_min_m={FormatVector(vehicleLocalBounds.min)}\n" +
                $"vehicle_local_bounds_center_m={FormatVector(vehicleLocalBounds.center)}\n" +
                $"vehicle_local_bounds_max_m={FormatVector(vehicleLocalBounds.max)}\n" +
                $"vehicle_local_bounds_size_m={FormatVector(vehicleLocalBounds.size)}\n" +
                BuildDeckContactSummary(visualRoot, topgear, physicsRoot.transform, vehicleLocalBounds) +
                BuildSubmeshMaterialBounds(topgear, physicsRoot.transform));

            SaveView(logRoot, "front", physicsRoot.transform, worldBounds, new Vector3(0f, 0.68f, 1.55f));
            SaveView(logRoot, "rear", physicsRoot.transform, worldBounds, new Vector3(0f, 0.68f, -1.55f));
            SaveView(logRoot, "left", physicsRoot.transform, worldBounds, new Vector3(-1.55f, 0.68f, 0f));
            SaveView(logRoot, "right", physicsRoot.transform, worldBounds, new Vector3(1.55f, 0.68f, 0f));
            SaveView(logRoot, "top", physicsRoot.transform, worldBounds, new Vector3(0.15f, 1.85f, 0.15f));

            File.AppendAllText(resultPath,
                $"front_screenshot={Path.Combine(logRoot, "vln_offroad_scout_wheel_ground_topgear_visual_front.png")}\n" +
                $"rear_screenshot={Path.Combine(logRoot, "vln_offroad_scout_wheel_ground_topgear_visual_rear.png")}\n" +
                $"left_screenshot={Path.Combine(logRoot, "vln_offroad_scout_wheel_ground_topgear_visual_left.png")}\n" +
                $"right_screenshot={Path.Combine(logRoot, "vln_offroad_scout_wheel_ground_topgear_visual_right.png")}\n" +
                $"top_screenshot={Path.Combine(logRoot, "vln_offroad_scout_wheel_ground_topgear_visual_top.png")}\n" +
                $"finished={DateTime.UtcNow:O}\n" +
                "success=1\n");

            Debug.Log($"VLN_TOPGEAR_VISUAL_ALIGNMENT_RESULT {resultPath}");
            EditorApplication.Exit(0);
        }

        static string BuildSubmeshMaterialBounds(GameObject root, Transform localFrame)
        {
            var meshFilter = root.GetComponentInChildren<MeshFilter>(true);
            var renderer = root.GetComponentInChildren<MeshRenderer>(true);
            if (meshFilter == null || renderer == null || meshFilter.sharedMesh == null)
            {
                return "material_bounds_available=0\n";
            }

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            var materials = renderer.sharedMaterials;
            string output = "material_bounds_available=1\n";

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] indices = mesh.GetTriangles(submesh);
                if (indices.Length == 0)
                {
                    continue;
                }

                bool initialized = false;
                Bounds bounds = default;
                foreach (int index in indices)
                {
                    Vector3 world = meshFilter.transform.TransformPoint(vertices[index]);
                    Vector3 local = localFrame.InverseTransformPoint(world);
                    if (!initialized)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }

                string materialName = submesh < materials.Length && materials[submesh] != null ? materials[submesh].name : $"submesh_{submesh}";
                string key = SanitizeKey(materialName);
                output += $"material_{submesh}_{key}_bounds_min_m={FormatVector(bounds.min)}\n";
                output += $"material_{submesh}_{key}_bounds_center_m={FormatVector(bounds.center)}\n";
                output += $"material_{submesh}_{key}_bounds_max_m={FormatVector(bounds.max)}\n";
                output += $"material_{submesh}_{key}_bounds_size_m={FormatVector(bounds.size)}\n";
            }

            return output;
        }

        static string BuildDeckContactSummary(GameObject visualRoot, GameObject topgear, Transform vehicleFrame, Bounds topgearLocalBounds)
        {
            if (visualRoot == null)
            {
                return "scout_visual_present=0\n" +
                    "scout_visual_top_under_topgear_m=missing\n" +
                    "topgear_visual_bottom_to_scout_visual_top_gap_m=missing\n";
            }

            float minX = topgearLocalBounds.min.x - 0.04f;
            float maxX = topgearLocalBounds.max.x + 0.04f;
            float minZ = topgearLocalBounds.min.z - 0.04f;
            float maxZ = topgearLocalBounds.max.z + 0.04f;
            float topY = float.NegativeInfinity;
            int sampleCount = 0;

            foreach (var meshFilter in visualRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.transform.IsChildOf(topgear.transform) || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
                {
                    Vector3 local = vehicleFrame.InverseTransformPoint(meshFilter.transform.TransformPoint(vertex));
                    if (local.x < minX || local.x > maxX || local.z < minZ || local.z > maxZ)
                    {
                        continue;
                    }

                    topY = Mathf.Max(topY, local.y);
                    sampleCount++;
                }
            }

            if (sampleCount == 0 || float.IsNegativeInfinity(topY))
            {
                return "scout_visual_present=1\n" +
                    "scout_visual_top_under_topgear_sample_count=0\n" +
                    "scout_visual_top_under_topgear_m=missing\n" +
                    "topgear_visual_bottom_to_scout_visual_top_gap_m=missing\n";
            }

            float gap = topgearLocalBounds.min.y - topY;
            return "scout_visual_present=1\n" +
                $"scout_visual_top_under_topgear_sample_count={sampleCount}\n" +
                $"scout_visual_top_under_topgear_m={topY:F3}\n" +
                $"topgear_visual_bottom_to_scout_visual_top_gap_m={gap:F3}\n";
        }

        static void SaveView(string logRoot, string viewName, Transform vehicleFrame, Bounds focusBounds, Vector3 localOffset)
        {
            string path = Path.Combine(logRoot, $"vln_offroad_scout_wheel_ground_topgear_visual_{viewName}.png");
            var cameraObject = new GameObject($"TopgearVisual_{viewName}_Camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = 20f;
                camera.fieldOfView = viewName == "top" ? 24f : 34f;

                Vector3 focus = focusBounds.center;
                cameraObject.transform.position = focus + vehicleFrame.TransformDirection(localOffset);
                cameraObject.transform.LookAt(focus);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        static Bounds CalculateRendererBoundsInLocalFrame(GameObject root, Transform localFrame)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                Vector3 min = renderer.bounds.min;
                Vector3 max = renderer.bounds.max;
                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z),
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 local = localFrame.InverseTransformPoint(corner);
                    if (!initialized)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return bounds;
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

        static string FormatVector(Vector3 value)
        {
            return $"{value.x:F3},{value.y:F3},{value.z:F3}";
        }

        static string SanitizeKey(string value)
        {
            return value.Replace(" ", "_").Replace("-", "_").Replace("/", "_").Replace(".", "_");
        }
    }
}
