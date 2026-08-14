using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnOffroadAssetCandidateSmokeTest : MonoBehaviour
    {
        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_ImageTopic = "/vln/front/image_raw";
        [SerializeField] string m_CameraInfoTopic = "/vln/front/camera_info";
        [SerializeField] string m_PointCloudTopic = "/vln/lidar/points";
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] string m_CameraFrameId = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrameId = "lidar_link";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 30f;

        float m_StartRealtime;
        string m_ResultPath;
        bool m_ScreenshotRequested;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_asset_candidate_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "scene=Assets/VLN/Scenes/VLNOffroadAssetCandidate.unity\n" +
                $"image_topic={m_ImageTopic}\n" +
                $"camera_info_topic={m_CameraInfoTopic}\n" +
                $"pointcloud_topic={m_PointCloudTopic}\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"camera_frame_id={m_CameraFrameId}\n" +
                $"lidar_frame_id={m_LidarFrameId}\n" +
                "image_type=sensor_msgs/msg/Image\n" +
                "pointcloud_type=sensor_msgs/msg/PointCloud2\n" +
                "terrain=procedural_mesh_plus_kenney_nature_kit\n" +
                "asset_source=Kenney Nature Kit 2.1 CC0\n" +
                "vehicle=procedural_cmd_vel_placeholder\n" +
                "tf_topic=/tf\n" +
                "tf_tree=map->base_link->front_camera_optical_frame,lidar_link\n" +
                "cmd_vel_type=geometry_msgs/msg/Twist\n" +
                "image_resolution=640x480\n" +
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

            Debug.Log($"VLN_OFFROAD_ASSET_CANDIDATE_READY image={m_ImageTopic} points={m_PointCloudTopic} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;
            if (!m_ScreenshotRequested && elapsed >= 4f)
            {
                m_ScreenshotRequested = true;
                string screenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_asset_candidate_screenshot.png");
                SaveViewerCameraScreenshot(screenshotPath);
                File.AppendAllText(m_ResultPath, $"screenshot={screenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_ASSET_CANDIDATE_SCREENSHOT {screenshotPath}");
            }

            if (!Application.isBatchMode || elapsed < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            File.AppendAllText(m_ResultPath, $"finished={DateTime.UtcNow:O}\n");
            Debug.Log("VLN_OFFROAD_ASSET_CANDIDATE_AUTO_EXIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        static void SaveViewerCameraScreenshot(string path)
        {
            var cameraObject = GameObject.Find("Offroad_ViewerCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                throw new InvalidOperationException("未找到可用于候选场景截图的 Camera。");
            }

            const int width = 1280;
            const int height = 720;
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
