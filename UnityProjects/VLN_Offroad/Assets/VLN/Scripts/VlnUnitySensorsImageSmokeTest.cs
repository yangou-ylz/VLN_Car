using System;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnUnitySensorsImageSmokeTest : MonoBehaviour
    {
        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_ImageTopic = "/vln/front/image_raw";
        [SerializeField] string m_FrameId = "front_camera_optical_frame";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 18f;

        float m_StartRealtime;
        string m_ResultPath;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_unitysensors_image_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                $"topic={m_ImageTopic}\n" +
                $"frame_id={m_FrameId}\n" +
                "message_type=sensor_msgs/msg/Image\n" +
                "resolution=640x480\n" +
                "encoding=rgb8\n" +
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

            Debug.Log($"VLN_UNITYSENSORS_IMAGE_READY topic={m_ImageTopic} frame_id={m_FrameId} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            if (!Application.isBatchMode || Time.realtimeSinceStartup - m_StartRealtime < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            File.AppendAllText(m_ResultPath, $"finished={DateTime.UtcNow:O}\n");
            Debug.Log("VLN_UNITYSENSORS_IMAGE_AUTO_EXIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }
    }
}
