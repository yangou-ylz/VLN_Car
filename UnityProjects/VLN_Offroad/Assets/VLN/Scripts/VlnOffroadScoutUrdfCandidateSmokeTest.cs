using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.UrdfImporter;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnOffroadScoutUrdfCandidateSmokeTest : MonoBehaviour
    {
        const string ScoutRootName = "ScoutUrdf_Root";

        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_ImageTopic = "/vln/front/image_raw";
        [SerializeField] string m_CameraInfoTopic = "/vln/front/camera_info";
        [SerializeField] string m_PointCloudTopic = "/vln/lidar/points";
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] string m_OdomTopic = "/vln/odom";
        [SerializeField] string m_CameraFrameId = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrameId = "lidar_link";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 34f;

        float m_StartRealtime;
        string m_ResultPath;
        Vector3 m_InitialScoutPosition;
        bool m_InitialPoseCaptured;
        bool m_ScreenshotRequested;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_urdf_candidate_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));

            var scout = GameObject.Find(ScoutRootName);
            if (scout == null)
            {
                throw new InvalidOperationException("Missing Scout URDF root in candidate scene.");
            }

            m_InitialScoutPosition = scout.transform.position;
            m_InitialPoseCaptured = true;

            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "scene=Assets/VLN/Scenes/VLNOffroadScoutUrdfCandidate.unity\n" +
                $"image_topic={m_ImageTopic}\n" +
                $"camera_info_topic={m_CameraInfoTopic}\n" +
                $"pointcloud_topic={m_PointCloudTopic}\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"odom_topic={m_OdomTopic}\n" +
                $"camera_frame_id={m_CameraFrameId}\n" +
                $"lidar_frame_id={m_LidarFrameId}\n" +
                "image_type=sensor_msgs/msg/Image\n" +
                "pointcloud_type=sensor_msgs/msg/PointCloud2\n" +
                "odom_type=nav_msgs/msg/Odometry\n" +
                "terrain=procedural_mesh_plus_kenney_nature_kit\n" +
                "vehicle=agilex_scout_v2_urdf_candidate\n" +
                "vehicle_source=https://github.com/agilexrobotics/ugv_gazebo_sim scout/scout_description\n" +
                "vehicle_import_strategy=unity_urdf_importer_static_first_pass_keep_existing_ros2_sensor_tf_cmd_vel\n" +
                "tf_topic=/tf\n" +
                "tf_tree=map->base_link->front_camera_optical_frame,lidar_link\n" +
                "odom_frame=map\n" +
                "odom_child_frame=base_link\n" +
                "cmd_vel_type=geometry_msgs/msg/Twist\n" +
                "image_resolution=640x480\n" +
                "lidar_scan_pattern=VLP-16\n" +
                "lidar_points_per_scan=7200\n" +
                "frequency_hz=5\n" +
                BuildScoutComponentSummary(scout));

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

            Debug.Log($"VLN_OFFROAD_SCOUT_URDF_CANDIDATE_READY image={m_ImageTopic} points={m_PointCloudTopic} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;
            if (!m_ScreenshotRequested && elapsed >= 5f)
            {
                m_ScreenshotRequested = true;
                string screenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_urdf_candidate_screenshot.png");
                SaveViewerCameraScreenshot(screenshotPath);
                File.AppendAllText(m_ResultPath, $"screenshot={screenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_URDF_CANDIDATE_SCREENSHOT {screenshotPath}");

                string detailScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_urdf_candidate_detail_screenshot.png");
                SaveScoutDetailScreenshot(detailScreenshotPath);
                File.AppendAllText(m_ResultPath, $"detail_screenshot={detailScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_URDF_CANDIDATE_DETAIL_SCREENSHOT {detailScreenshotPath}");
            }

            if (!Application.isBatchMode || elapsed < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            WriteFinalSnapshot();
            Debug.Log("VLN_OFFROAD_SCOUT_URDF_CANDIDATE_AUTO_EXIT");
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
            if (string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }

            string marker = "finished=";
            string existing = File.ReadAllText(m_ResultPath);
            if (existing.Contains(marker, StringComparison.Ordinal))
            {
                return;
            }

            var scout = GameObject.Find(ScoutRootName);
            float staticDelta = 0f;
            if (scout != null && m_InitialPoseCaptured)
            {
                staticDelta = Vector3.Distance(m_InitialScoutPosition, scout.transform.position);
            }

            File.AppendAllText(m_ResultPath,
                $"finished={DateTime.UtcNow:O}\n" +
                $"static_pose_delta_m={staticDelta:F4}\n");
        }

        static string BuildScoutComponentSummary(GameObject scout)
        {
            Bounds bounds = CalculateRendererBounds(scout);
            return
                $"urdf_robot_count={scout.GetComponentsInChildren<UrdfRobot>(true).Length}\n" +
                $"urdf_link_count={scout.GetComponentsInChildren<UrdfLink>(true).Length}\n" +
                $"urdf_joint_count={scout.GetComponentsInChildren<UrdfJoint>(true).Length}\n" +
                $"urdf_continuous_joint_count={scout.GetComponentsInChildren<UrdfJointContinuous>(true).Length}\n" +
                $"urdf_inertial_count={scout.GetComponentsInChildren<UrdfInertial>(true).Length}\n" +
                $"urdf_collision_count={scout.GetComponentsInChildren<UrdfCollision>(true).Length}\n" +
                $"unity_collider_count={scout.GetComponentsInChildren<Collider>(true).Length}\n" +
                $"renderer_count={scout.GetComponentsInChildren<Renderer>(true).Length}\n" +
                $"articulation_body_count={scout.GetComponentsInChildren<ArticulationBody>(true).Length}\n" +
                $"rigidbody_count={scout.GetComponentsInChildren<Rigidbody>(true).Length}\n" +
                $"bounds_size={bounds.size.x:F3},{bounds.size.y:F3},{bounds.size.z:F3}\n";
        }

        static void SaveViewerCameraScreenshot(string path)
        {
            var cameraObject = GameObject.Find("ScoutUrdfCandidate_GameCamera") ?? GameObject.Find("Offroad_ViewerCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                throw new InvalidOperationException("No camera available for Scout URDF candidate screenshot.");
            }

            RenderCameraToPng(camera, path, 1280, 720);
        }

        static void SaveScoutDetailScreenshot(string path)
        {
            var scout = GameObject.Find(ScoutRootName);
            if (scout == null)
            {
                throw new InvalidOperationException("Missing Scout URDF root for detail screenshot.");
            }

            Bounds bounds = CalculateRendererBounds(scout);
            var cameraObject = new GameObject("VLN_TemporaryScoutDetailCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.fieldOfView = 38f;

            Vector3 lookAt = bounds.center + Vector3.up * 0.08f;
            camera.transform.position = bounds.center + new Vector3(1.75f, 1.05f, -2.20f);
            camera.transform.LookAt(lookAt);

            try
            {
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                Destroy(cameraObject);
            }
        }

        static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("Scout URDF candidate has no renderers.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
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
                Destroy(renderTexture);
                Destroy(texture);
            }
        }
    }
}
