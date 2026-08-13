using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnUnitySensorsLidarSmokeTest : MonoBehaviour
    {
        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_PointCloudTopic = "/vln/lidar/points";
        [SerializeField] string m_FrameId = "lidar_link";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 20f;

        float m_StartRealtime;
        string m_ResultPath;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_unitysensors_lidar_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                $"topic={m_PointCloudTopic}\n" +
                $"frame_id={m_FrameId}\n" +
                "message_type=sensor_msgs/msg/PointCloud2\n" +
                "scan_pattern=VLP-16\n" +
                "points_per_scan=7200\n" +
                "frequency_hz=5\n" +
                "point_step=16\n");

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

            Debug.Log($"VLN_UNITYSENSORS_LIDAR_READY topic={m_PointCloudTopic} frame_id={m_FrameId} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            if (!Application.isBatchMode || Time.realtimeSinceStartup - m_StartRealtime < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            File.AppendAllText(m_ResultPath, $"finished={DateTime.UtcNow:O}\n");
            Debug.Log("VLN_UNITYSENSORS_LIDAR_AUTO_EXIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }
    }
}
