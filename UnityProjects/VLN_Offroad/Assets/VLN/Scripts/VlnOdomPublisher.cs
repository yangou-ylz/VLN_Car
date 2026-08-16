using System;
using System.IO;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnOdomPublisher : MonoBehaviour
    {
        [SerializeField] string m_OdomTopic = "/vln/odom";
        [SerializeField] string m_OdomFrame = "map";
        [SerializeField] string m_BaseFrame = "base_link";
        [SerializeField] float m_PublishFrequencyHz = 10f;

        ROSConnection m_Ros;
        Vector3 m_LastPosition;
        float m_LastYawRad;
        float m_LastSampleTime;
        float m_NextPublishTime;
        string m_ResultPath;
        int m_PublishCount;
        bool m_FinalSnapshotWritten;

        static readonly double[] PoseCovariance = MakeCovariance(0.01, 0.01, 0.04, 0.04, 0.04, 0.02);
        static readonly double[] TwistCovariance = MakeCovariance(0.02, 0.02, 0.04, 0.08, 0.08, 0.04);

        void Start()
        {
            m_LastPosition = transform.position;
            m_LastYawRad = UnityYawRad(transform.rotation);
            m_LastSampleTime = Time.time;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_odom_publisher_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                $"odom_topic={m_OdomTopic}\n" +
                "odom_type=nav_msgs/msg/Odometry\n" +
                $"odom_frame={m_OdomFrame}\n" +
                $"child_frame={m_BaseFrame}\n" +
                $"publish_frequency_hz={m_PublishFrequencyHz:F2}\n" +
                "source=unity_rig_transform_delta\n");

            m_Ros = ROSConnection.GetOrCreateInstance();
            m_Ros.RegisterPublisher<OdometryMsg>(m_OdomTopic, queue_size: 10);
            Debug.Log($"VLN_ODOM_PUBLISHER_READY topic={m_OdomTopic} frame={m_OdomFrame} child={m_BaseFrame}");
        }

        void Update()
        {
            if (m_Ros == null || Time.time < m_NextPublishTime)
            {
                return;
            }

            float now = Time.time;
            float dt = Mathf.Max(1e-4f, now - m_LastSampleTime);
            Vector3 currentPosition = transform.position;
            float currentYawRad = UnityYawRad(transform.rotation);

            Vector3 worldVelocity = (currentPosition - m_LastPosition) / dt;
            Vector3 localVelocity = Quaternion.Inverse(transform.rotation) * worldVelocity;
            float yawRateRadPerSecond = Mathf.DeltaAngle(m_LastYawRad * Mathf.Rad2Deg, currentYawRad * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt;

            PublishOdom(currentPosition, transform.rotation, localVelocity, yawRateRadPerSecond, now);

            m_LastPosition = currentPosition;
            m_LastYawRad = currentYawRad;
            m_LastSampleTime = now;
            m_NextPublishTime = now + 1f / Mathf.Max(0.1f, m_PublishFrequencyHz);
        }

        void OnApplicationQuit()
        {
            WriteFinalSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalSnapshot();
        }

        void PublishOdom(Vector3 position, Quaternion rotation, Vector3 localVelocityUnity, float yawRateRadPerSecond, float stampSeconds)
        {
            TimeMsg stamp = MakeStamp(stampSeconds);
            Vector3Msg linearVelocity = new(
                localVelocityUnity.z,
                -localVelocityUnity.x,
                localVelocityUnity.y);
            Vector3Msg angularVelocity = new(0.0, 0.0, yawRateRadPerSecond);

            var msg = new OdometryMsg(
                new HeaderMsg(stamp, m_OdomFrame),
                m_BaseFrame,
                new PoseWithCovarianceMsg(
                    new PoseMsg(position.To<FLU>(), rotation.To<FLU>()),
                    PoseCovariance),
                new TwistWithCovarianceMsg(
                    new TwistMsg(linearVelocity, angularVelocity),
                    TwistCovariance));

            m_Ros.Publish(m_OdomTopic, msg);
            m_PublishCount++;

            if (m_PublishCount == 1 || m_PublishCount % 20 == 0)
            {
                File.AppendAllText(m_ResultPath,
                    $"odom_published={m_PublishCount};time={DateTime.UtcNow:O};" +
                    $"pose={position.x:F3},{position.y:F3},{position.z:F3};" +
                    $"linear_base_flu={linearVelocity.x:F3},{linearVelocity.y:F3},{linearVelocity.z:F3};" +
                    $"yaw_rate_radps={yawRateRadPerSecond:F3}\n");
                Debug.Log($"VLN_ODOM_PUBLISHED count={m_PublishCount} linear_x={linearVelocity.x:F3} yaw_rate={yawRateRadPerSecond:F3}");
            }
        }

        void WriteFinalSnapshot()
        {
            if (m_FinalSnapshotWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }

            m_FinalSnapshotWritten = true;
            File.AppendAllText(m_ResultPath,
                $"finished={DateTime.UtcNow:O}\n" +
                $"odom_publish_count={m_PublishCount}\n" +
                $"final_position={transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}\n" +
                $"final_yaw_deg={transform.eulerAngles.y:F3}\n");
        }

        static TimeMsg MakeStamp(float seconds)
        {
            int sec = Mathf.FloorToInt(seconds);
            uint nanosec = (uint)Mathf.Clamp(Mathf.RoundToInt((seconds - sec) * 1_000_000_000f), 0, 999_999_999);
            return new TimeMsg(sec, nanosec);
        }

        static float UnityYawRad(Quaternion rotation)
        {
            return rotation.eulerAngles.y * Mathf.Deg2Rad;
        }

        static double[] MakeCovariance(double x, double y, double z, double roll, double pitch, double yaw)
        {
            var covariance = new double[36];
            covariance[0] = x;
            covariance[7] = y;
            covariance[14] = z;
            covariance[21] = roll;
            covariance[28] = pitch;
            covariance[35] = yaw;
            return covariance;
        }
    }
}
