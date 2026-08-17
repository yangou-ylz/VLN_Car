using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnOffroadScoutWheelGroundCandidateSmokeTest : MonoBehaviour
    {
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string VisualRootName = "ScoutWheelGround_VisualUrdf";

        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_ImageTopic = "/vln/front/image_raw";
        [SerializeField] string m_CameraInfoTopic = "/vln/front/camera_info";
        [SerializeField] string m_PointCloudTopic = "/vln/lidar/points";
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] string m_OdomTopic = "/vln/odom";
        [SerializeField] string m_CameraFrameId = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrameId = "lidar_link";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 38f;

        float m_StartRealtime;
        string m_ResultPath;
        Vector3 m_InitialPosition;
        bool m_ScreenshotRequested;
        bool m_BridgeScreenshotRequested;
        bool m_ShortRampScreenshotRequested;
        bool m_FinalSnapshotWritten;

        void Start()
        {
            Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 12);
            Physics.defaultSolverVelocityIterations = Mathf.Max(Physics.defaultSolverVelocityIterations, 6);
            Time.fixedDeltaTime = 0.01f;

            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_candidate_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));

            var physicsRoot = GameObject.Find(PhysicsRootName);
            if (physicsRoot == null)
            {
                throw new InvalidOperationException("Missing Scout wheel-ground physics root in candidate scene.");
            }

            m_InitialPosition = physicsRoot.transform.position;
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "scene=Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity\n" +
                "stage=wheel_ground_real_dynamics_candidate\n" +
                "vehicle=agilex_scout_v2_urdf_visual_plus_unity_wheelcollider_physics\n" +
                "motion_source=wheel_ground_contact_not_kinematic_rig\n" +
                "physics_backend=Unity WheelCollider + Rigidbody\n" +
                "urdf_articulation_candidate_preserved_in_separate_scene=VLNOffroadScoutUrdfCandidate.unity\n" +
                $"image_topic={m_ImageTopic}\n" +
                $"camera_info_topic={m_CameraInfoTopic}\n" +
                $"pointcloud_topic={m_PointCloudTopic}\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"odom_topic={m_OdomTopic}\n" +
                $"camera_frame_id={m_CameraFrameId}\n" +
                $"lidar_frame_id={m_LidarFrameId}\n" +
                "tf_topic=/tf\n" +
                "tf_tree=map->base_link->front_camera_optical_frame,lidar_link\n" +
                "odom_type=nav_msgs/msg/Odometry\n" +
                "cmd_vel_type=geometry_msgs/msg/Twist\n" +
                "image_resolution=640x480\n" +
                "lidar_scan_pattern=VLP-16\n" +
                "lidar_points_per_scan=7200\n" +
                "terrain_geometry_policy=visible_local_physics_no_flattening_no_hidden_bypass\n" +
                BuildPhysicsSummary(physicsRoot));

            var ros = ROSConnection.GetOrCreateInstance();
            ros.RosIPAddress = m_RosIp;
            ros.RosPort = m_RosPort;
            ros.ConnectOnStart = true;
            ros.ShowHud = false;
            ros.listenForTFMessages = false;

            if (!ros.HasConnectionThread)
            {
                ros.Connect(m_RosIp, m_RosPort);
            }

            Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_READY image={m_ImageTopic} points={m_PointCloudTopic} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;
            if (!m_ScreenshotRequested && elapsed >= 5f)
            {
                m_ScreenshotRequested = true;
                string screenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_candidate_screenshot.png");
                SaveViewerCameraScreenshot(screenshotPath);
                File.AppendAllText(m_ResultPath, $"screenshot={screenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_SCREENSHOT {screenshotPath}");
            }

            var physicsRoot = GameObject.Find(PhysicsRootName);
            if (!m_BridgeScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= -9.5f && physicsRoot.transform.position.z <= -4.2f)
            {
                m_BridgeScreenshotRequested = true;
                string bridgeScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_bridge_screenshot.png");
                SaveViewerCameraScreenshot(bridgeScreenshotPath);
                File.AppendAllText(m_ResultPath, $"bridge_screenshot={bridgeScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_BRIDGE_SCREENSHOT {bridgeScreenshotPath}");
            }

            if (!m_ShortRampScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= 1.2f && physicsRoot.transform.position.z <= 5.8f)
            {
                m_ShortRampScreenshotRequested = true;
                string shortRampScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png");
                SaveViewerCameraScreenshot(shortRampScreenshotPath);
                File.AppendAllText(m_ResultPath, $"short_ramp_screenshot={shortRampScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_SHORT_RAMP_SCREENSHOT {shortRampScreenshotPath}");
            }

            if (!Application.isBatchMode || elapsed < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            WriteFinalSnapshot();
            Debug.Log("VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_AUTO_EXIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        void OnApplicationQuit()
        {
            WriteFinalSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalSnapshot();
        }

        void WriteFinalSnapshot()
        {
            if (m_FinalSnapshotWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }

            m_FinalSnapshotWritten = true;
            var physicsRoot = GameObject.Find(PhysicsRootName);
            float baseDelta = 0f;
            if (physicsRoot != null)
            {
                baseDelta = Vector3.Distance(m_InitialPosition, physicsRoot.transform.position);
            }

            File.AppendAllText(m_ResultPath,
                $"finished={DateTime.UtcNow:O}\n" +
                $"physics_root_delta_m={baseDelta:F4}\n" +
                $"final_position={(physicsRoot != null ? FormatVector(physicsRoot.transform.position) : "missing")}\n" +
                $"final_yaw_deg={(physicsRoot != null ? physicsRoot.transform.eulerAngles.y.ToString("F3") : "missing")}\n");
        }

        static string BuildPhysicsSummary(GameObject physicsRoot)
        {
            var visualRoot = GameObject.Find(VisualRootName);
            var body = physicsRoot.GetComponent<Rigidbody>();
            var colliders = physicsRoot.GetComponentsInChildren<WheelCollider>(true);
            return
                $"rigidbody_count={physicsRoot.GetComponentsInChildren<Rigidbody>(true).Length}\n" +
                $"wheel_collider_count={colliders.Length}\n" +
                $"box_collider_count={physicsRoot.GetComponentsInChildren<BoxCollider>(true).Length}\n" +
                $"broad_physical_trail_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalTrailSurface_")}\n" +
                $"road_physical_slab_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalRoadSlab_")}\n" +
                $"road_seam_transition_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalRoadSeam_")}\n" +
                $"bridge_physics_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalBridge")}\n" +
                $"short_ramp_physics_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalShortRamp")}\n" +
                $"bridge_visual_detail_count={CountGameObjectsByPrefix("ScoutWheelGround_VisibleBridgeDetail_")}\n" +
                $"bridge_rail_collider_count={CountBridgeRailColliders()}\n" +
                $"short_ramp_visual_detail_count={CountGameObjectsByPrefix("ScoutWheelGround_VisibleShortRampDetail_")}\n" +
                $"road_physical_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_PhysicalRoad"):F3}\n" +
                $"bridge_physical_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_PhysicalBridge"):F3}\n" +
                $"bridge_physical_height_span_m={MaxBoundsHeightByPrefix("ScoutWheelGround_PhysicalBridge"):F3}\n" +
                $"short_ramp_physical_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_PhysicalShortRamp"):F3}\n" +
                $"short_ramp_physical_height_span_m={MaxBoundsHeightByPrefix("ScoutWheelGround_PhysicalShortRamp"):F3}\n" +
                $"decorative_trail_collider_count={CountDecorativeTrailColliders()}\n" +
                $"decorative_bridge_renderer_count={CountDecorativeBridgeRenderers()}\n" +
                $"bridge_deck_has_renderer={HasComponentOnObject<Renderer>("ScoutWheelGround_PhysicalBridgeDeck")}\n" +
                $"bridge_deck_has_collider={HasComponentOnObject<Collider>("ScoutWheelGround_PhysicalBridgeDeck")}\n" +
                $"bridge_deck_renderer_collider_top_delta_m={BridgeDeckRendererColliderTopDelta():F4}\n" +
                $"visual_renderer_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true).Length : 0)}\n" +
                $"visual_collider_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<Collider>(true).Length : 0)}\n" +
                $"visual_articulation_body_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<ArticulationBody>(true).Length : 0)}\n" +
                $"rigidbody_mass_kg={(body != null ? body.mass.ToString("F2") : "missing")}\n";
        }

        static int CountGameObjectsByPrefix(string prefix)
        {
            int count = 0;
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        static float MaxBoundsWidthByPrefix(string prefix)
        {
            float maxWidth = 0f;
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (!gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    maxWidth = Mathf.Max(maxWidth, renderer.bounds.size.x);
                    continue;
                }

                var collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    maxWidth = Mathf.Max(maxWidth, collider.bounds.size.x);
                }
            }

            return maxWidth;
        }

        static float MaxBoundsHeightByPrefix(string prefix)
        {
            float maxHeight = 0f;
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (!gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    maxHeight = Mathf.Max(maxHeight, renderer.bounds.size.y);
                    continue;
                }

                var collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    maxHeight = Mathf.Max(maxHeight, collider.bounds.size.y);
                }
            }

            return maxHeight;
        }

        static int CountDecorativeTrailColliders()
        {
            int count = 0;
            foreach (var collider in FindObjectsOfType<Collider>())
            {
                string name = collider.gameObject.name;
                if (name.StartsWith("Offroad_DirtRoad_", StringComparison.Ordinal) || name == "Offroad_ShortRamp")
                {
                    count++;
                }
            }
            return count;
        }

        static int CountBridgeRailColliders()
        {
            int count = 0;
            foreach (var collider in FindObjectsOfType<BoxCollider>())
            {
                string name = collider.gameObject.name;
                if (!name.StartsWith("ScoutWheelGround_VisibleBridgeDetail_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (name.Contains("Rail", StringComparison.Ordinal) || name.Contains("Post", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        static int CountDecorativeBridgeRenderers()
        {
            int count = 0;
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                if (IsDecorativeBridgeTransform(renderer.transform))
                {
                    count++;
                }
            }
            return count;
        }

        static bool IsDecorativeBridgeTransform(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                string name = current.name;
                if (name.Contains("WoodBridge", StringComparison.Ordinal) ||
                    name.Contains("Kenney_bridge", StringComparison.Ordinal) ||
                    name.Contains("bridge_wood", StringComparison.Ordinal) ||
                    name.Contains("bridge_center_wood", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        static int HasComponentOnObject<T>(string objectName) where T : Component
        {
            var gameObject = GameObject.Find(objectName);
            return gameObject != null && gameObject.GetComponent<T>() != null ? 1 : 0;
        }

        static float BridgeDeckRendererColliderTopDelta()
        {
            var gameObject = GameObject.Find("ScoutWheelGround_PhysicalBridgeDeck");
            if (gameObject == null)
            {
                return 999f;
            }

            var renderer = gameObject.GetComponent<Renderer>();
            var collider = gameObject.GetComponent<Collider>();
            if (renderer == null || collider == null)
            {
                return 999f;
            }

            return Mathf.Abs(renderer.bounds.max.y - collider.bounds.max.y);
        }

        static void SaveViewerCameraScreenshot(string path)
        {
            var cameraObject = GameObject.Find("ScoutWheelGroundCandidate_GameCamera") ?? GameObject.Find("Offroad_ViewerCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                throw new InvalidOperationException("No camera available for Scout wheel-ground candidate screenshot.");
            }

            RenderCameraToPng(camera, path, 1280, 720);
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
                Destroy(renderTexture);
                Destroy(texture);
            }
        }

        static string FormatVector(Vector3 value)
        {
            return $"{value.x:F3},{value.y:F3},{value.z:F3}";
        }
    }
}
