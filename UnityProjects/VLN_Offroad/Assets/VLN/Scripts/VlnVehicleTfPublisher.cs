using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.Tf2;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnVehicleTfPublisher : MonoBehaviour
    {
        [SerializeField] string m_TfTopic = "/tf";
        [SerializeField] string m_MapFrame = "map";
        [SerializeField] string m_BaseFrame = "base_link";
        [SerializeField] string m_CameraFrame = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrame = "lidar_link";
        [SerializeField] Transform m_CameraTransform;
        [SerializeField] Transform m_LidarTransform;
        [SerializeField] float m_TfFrequencyHz = 10f;
        [SerializeField] float m_VehicleSpeedMetersPerSecond = 1.4f;
        [SerializeField] float m_PathStartZ = -24f;
        [SerializeField] float m_PathEndZ = 24f;

        const float TerrainSize = 80f;
        const float TerrainHeight = 7f;

        ROSConnection m_Ros;
        float m_StartRealtime;
        float m_NextPublishTime;
        Vector3 m_LastPosition;
        bool m_Registered;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_LastPosition = transform.position;
            m_Ros = ROSConnection.GetOrCreateInstance();
            m_Ros.RegisterPublisher<TFMessageMsg>(m_TfTopic, queue_size: 10);
            m_Registered = true;
            Debug.Log($"VLN_VEHICLE_TF_READY topic={m_TfTopic} map={m_MapFrame} base={m_BaseFrame} camera={m_CameraFrame} lidar={m_LidarFrame}");
        }

        void Update()
        {
            UpdateVehiclePose();

            if (!m_Registered || Time.time < m_NextPublishTime)
            {
                return;
            }

            m_NextPublishTime = Time.time + 1f / Mathf.Max(0.1f, m_TfFrequencyHz);
            PublishTf();
        }

        void UpdateVehiclePose()
        {
            float pathLength = Mathf.Max(0.1f, m_PathEndZ - m_PathStartZ);
            float distance = Mathf.PingPong((Time.realtimeSinceStartup - m_StartRealtime) * m_VehicleSpeedMetersPerSecond, pathLength);
            float z = m_PathStartZ + distance;
            float x = 0.7f * Mathf.Sin(0.18f * z);
            float y = TerrainWorldY(x, z);

            var nextPosition = new Vector3(x, y, z);
            Vector3 delta = nextPosition - m_LastPosition;
            if (delta.sqrMagnitude > 1e-5f)
            {
                float yawDegrees = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            }

            transform.position = nextPosition;
            m_LastPosition = nextPosition;
        }

        void PublishTf()
        {
            TimeMsg stamp = MakeStamp(Time.time);
            TransformStampedMsg mapToBase = MakeTransform(m_MapFrame, m_BaseFrame, transform.position, transform.rotation, stamp);

            TransformStampedMsg baseToCamera = MakeTransform(
                m_BaseFrame,
                m_CameraFrame,
                m_CameraTransform != null ? m_CameraTransform.localPosition : Vector3.zero,
                m_CameraTransform != null ? m_CameraTransform.localRotation : Quaternion.identity,
                stamp);

            TransformStampedMsg baseToLidar = MakeTransform(
                m_BaseFrame,
                m_LidarFrame,
                m_LidarTransform != null ? m_LidarTransform.localPosition : Vector3.zero,
                m_LidarTransform != null ? m_LidarTransform.localRotation : Quaternion.identity,
                stamp);

            m_Ros.Publish(m_TfTopic, new TFMessageMsg(new[] { mapToBase, baseToCamera, baseToLidar }));
        }

        static TransformStampedMsg MakeTransform(string parent, string child, Vector3 position, Quaternion rotation, TimeMsg stamp)
        {
            return new TransformStampedMsg(
                new HeaderMsg(stamp, parent),
                child,
                new TransformMsg(position.To<FLU>(), rotation.To<FLU>()));
        }

        static TimeMsg MakeStamp(float seconds)
        {
            int sec = Mathf.FloorToInt(seconds);
            uint nanosec = (uint)Mathf.Clamp(Mathf.RoundToInt((seconds - sec) * 1_000_000_000f), 0, 999_999_999);
            return new TimeMsg(sec, nanosec);
        }

        static float TerrainWorldY(float x, float z)
        {
            return NormalizedTerrainHeight(x, z) * TerrainHeight;
        }

        static float NormalizedTerrainHeight(float x, float z)
        {
            float ridge = 0.034f * Mathf.Sin(0.24f * x + 0.31f * Mathf.Sin(0.12f * z));
            float roll = 0.025f * Mathf.Cos(0.19f * z - 0.11f * x);
            float longSlope = 0.032f * Mathf.InverseLerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z);
            float baseHeight = 0.17f + ridge + roll + longSlope;

            float roadBlend = Mathf.Clamp01(1f - Mathf.Abs(x) / 4.4f);
            roadBlend = roadBlend * roadBlend * (3f - 2f * roadBlend);
            float roadHeight = 0.175f + longSlope * 0.75f + 0.006f * Mathf.Sin(0.18f * z);
            return Mathf.Clamp01(Mathf.Lerp(baseHeight, roadHeight, roadBlend * 0.9f));
        }
    }
}
