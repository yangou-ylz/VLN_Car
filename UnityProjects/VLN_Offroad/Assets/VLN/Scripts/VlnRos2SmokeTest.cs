using System;
using System.IO;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnRos2SmokeTest : MonoBehaviour
    {
        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_UnityHeartbeatTopic = "/unity/heartbeat";
        [SerializeField] string m_Ros2CommandTopic = "/ros2/command";
        [SerializeField] float m_PublishPeriodSeconds = 0.5f;
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 14f;

        ROSConnection m_Ros;
        float m_NextPublishTime;
        float m_StartRealtime;
        int m_PublishCount;
        string m_ResultPath;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_ros2_smoke_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath, $"started={DateTime.UtcNow:O}\n");

            m_Ros = ROSConnection.GetOrCreateInstance();
            m_Ros.RosIPAddress = m_RosIp;
            m_Ros.RosPort = m_RosPort;
            m_Ros.ConnectOnStart = true;
            m_Ros.ShowHud = false;
            m_Ros.listenForTFMessages = false;

            m_Ros.RegisterPublisher<StringMsg>(m_UnityHeartbeatTopic);
            m_Ros.Subscribe<StringMsg>(m_Ros2CommandTopic, OnRos2Command);

            if (!m_Ros.HasConnectionThread)
            {
                m_Ros.Connect(m_RosIp, m_RosPort);
            }

            Debug.Log($"VLN_ROS2_SMOKE_READY ip={m_RosIp} port={m_RosPort} pub={m_UnityHeartbeatTopic} sub={m_Ros2CommandTopic}");
        }

        void Update()
        {
            if (Application.isBatchMode && Time.realtimeSinceStartup - m_StartRealtime >= m_BatchModeAutoExitAfterSeconds)
            {
                Debug.Log("VLN_ROS2_SMOKE_AUTO_EXIT");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(0);
#else
                Application.Quit(0);
#endif
                return;
            }

            if (Time.time < m_NextPublishTime)
            {
                return;
            }

            m_NextPublishTime = Time.time + m_PublishPeriodSeconds;
            m_PublishCount++;
            string payload = $"unity_heartbeat_{m_PublishCount}_{DateTime.UtcNow:O}";
            m_Ros.Publish(m_UnityHeartbeatTopic, new StringMsg(payload));
            Debug.Log($"VLN_ROS2_SMOKE_TX {payload}");
        }

        void OnRos2Command(StringMsg msg)
        {
            string line = $"received={DateTime.UtcNow:O};data={msg.data}";
            File.AppendAllText(m_ResultPath, line + "\n");
            Debug.Log($"VLN_ROS2_SMOKE_RX {msg.data}");
        }
    }
}
