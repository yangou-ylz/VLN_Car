using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnOffroadVehicleCandidateSmokeTest : MonoBehaviour
    {
        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_ImageTopic = "/vln/front/image_raw";
        [SerializeField] string m_CameraInfoTopic = "/vln/front/camera_info";
        [SerializeField] string m_PointCloudTopic = "/vln/lidar/points";
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] string m_CameraFrameId = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrameId = "lidar_link";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 32f;

        float m_StartRealtime;
        string m_ResultPath;
        bool m_ScreenshotRequested;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_vehicle_candidate_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "scene=Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity\n" +
                $"image_topic={m_ImageTopic}\n" +
                $"camera_info_topic={m_CameraInfoTopic}\n" +
                $"pointcloud_topic={m_PointCloudTopic}\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"camera_frame_id={m_CameraFrameId}\n" +
                $"lidar_frame_id={m_LidarFrameId}\n" +
                "image_type=sensor_msgs/msg/Image\n" +
                "pointcloud_type=sensor_msgs/msg/PointCloud2\n" +
                "terrain=procedural_mesh_plus_kenney_nature_kit\n" +
                "environment_asset_source=Kenney Nature Kit 2.1 CC0\n" +
                "vehicle=clearpath_husky_visual_candidate\n" +
                "vehicle_source=https://github.com/husky/husky humble-devel\n" +
                "vehicle_import_strategy=visual_mesh_only_keep_existing_cmd_vel_tf_sensor_rig\n" +
                "tf_topic=/tf\n" +
                "tf_tree=map->base_link->front_camera_optical_frame,lidar_link\n" +
                "cmd_vel_type=geometry_msgs/msg/Twist\n" +
                "image_resolution=1280x720\n" +
                "lidar_scan_pattern=VLP-16\n" +
                "lidar_points_per_scan=7200\n" +
                "frequency_hz=5\n");

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

            Debug.Log($"VLN_OFFROAD_VEHICLE_CANDIDATE_READY image={m_ImageTopic} points={m_PointCloudTopic} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;
            if (!m_ScreenshotRequested && elapsed >= 5f)
            {
                m_ScreenshotRequested = true;
                string screenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_vehicle_candidate_screenshot.png");
                SaveViewerCameraScreenshot(screenshotPath);
                File.AppendAllText(m_ResultPath, $"screenshot={screenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_VEHICLE_CANDIDATE_SCREENSHOT {screenshotPath}");

                string detailScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_vehicle_candidate_detail_screenshot.png");
                SaveVehicleDetailScreenshot(detailScreenshotPath);
                File.AppendAllText(m_ResultPath, $"detail_screenshot={detailScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_VEHICLE_CANDIDATE_DETAIL_SCREENSHOT {detailScreenshotPath}");
            }

            if (!Application.isBatchMode || elapsed < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            File.AppendAllText(m_ResultPath, $"finished={DateTime.UtcNow:O}\n");
            Debug.Log("VLN_OFFROAD_VEHICLE_CANDIDATE_AUTO_EXIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        static void SaveViewerCameraScreenshot(string path)
        {
            var cameraObject = GameObject.Find("VehicleCandidate_GameCamera") ?? GameObject.Find("Offroad_ViewerCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                throw new InvalidOperationException("未找到可用于车体候选场景截图的 Camera。");
            }

            const int width = 1280;
            const int height = 720;
            RenderCameraToPng(camera, path, width, height);
        }

        static void SaveVehicleDetailScreenshot(string path)
        {
            var vehicleRoot = GameObject.Find("HuskyVisual_Root");
            if (vehicleRoot == null)
            {
                throw new InvalidOperationException("未找到 HuskyVisual_Root，无法生成车体细节截图。");
            }

            Bounds bounds = CalculateRendererBounds(vehicleRoot);
            var cameraObject = new GameObject("VLN_TemporaryVehicleDetailCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.fieldOfView = 38f;

            Vector3 lookAt = bounds.center + Vector3.up * 0.15f;
            camera.transform.position = bounds.center + new Vector3(2.15f, 1.15f, -2.75f);
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
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("HuskyVisual_Root 下没有 Renderer，车体模型可能没有导入成功。");
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
