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
                $"visual_renderer_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true).Length : 0)}\n" +
                $"visual_collider_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<Collider>(true).Length : 0)}\n" +
                $"visual_articulation_body_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<ArticulationBody>(true).Length : 0)}\n" +
                $"rigidbody_mass_kg={(body != null ? body.mass.ToString("F2") : "missing")}\n";
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
