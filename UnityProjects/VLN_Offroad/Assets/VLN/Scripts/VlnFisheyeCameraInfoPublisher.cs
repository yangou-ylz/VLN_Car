using System;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnFisheyeCameraInfoPublisher : MonoBehaviour
    {
        [SerializeField] string m_TopicName = "/vln/front/camera_info";
        [SerializeField] string m_FrameId = "front_camera_optical_frame";
        [SerializeField] float m_PublishFrequencyHz = 20f;
        [SerializeField] int m_Width = 640;
        [SerializeField] int m_Height = 640;
        [SerializeField] float m_ViewAngleDeg = 190f;
        [SerializeField] string m_DistortionModel = "equidistant";

        ROSConnection m_Ros;
        float m_NextPublishTime;

        public string topicName
        {
            get => m_TopicName;
            set => m_TopicName = value;
        }

        public string frameId
        {
            get => m_FrameId;
            set => m_FrameId = value;
        }

        public float publishFrequencyHz
        {
            get => m_PublishFrequencyHz;
            set => m_PublishFrequencyHz = Mathf.Max(0.1f, value);
        }

        public int width
        {
            get => m_Width;
            set => m_Width = Mathf.Max(1, value);
        }

        public int height
        {
            get => m_Height;
            set => m_Height = Mathf.Max(1, value);
        }

        public float viewAngleDeg
        {
            get => m_ViewAngleDeg;
            set => m_ViewAngleDeg = Mathf.Clamp(value, 90f, 360f);
        }

        void Start()
        {
            m_Ros = ROSConnection.GetOrCreateInstance();
            m_Ros.RegisterPublisher<CameraInfoMsg>(m_TopicName, queue_size: 10);
            Debug.Log($"VLN_FISHEYE_CAMERA_INFO_READY topic={m_TopicName} frame={m_FrameId} model={m_DistortionModel} view_angle_deg={m_ViewAngleDeg:F1}");
        }

        void Update()
        {
            if (m_Ros == null || Time.time < m_NextPublishTime)
            {
                return;
            }

            m_Ros.Publish(m_TopicName, MakeMessage(Time.time));
            m_NextPublishTime = Time.time + 1f / Mathf.Max(0.1f, m_PublishFrequencyHz);
        }

        CameraInfoMsg MakeMessage(float stampSeconds)
        {
            double halfAngleRad = Mathf.Deg2Rad * m_ViewAngleDeg * 0.5;
            double radiusPixels = Math.Min(m_Width, m_Height) * 0.5;
            double focalPixels = radiusPixels / Math.Max(halfAngleRad, 1e-6);
            double cx = m_Width * 0.5;
            double cy = m_Height * 0.5;

            return new CameraInfoMsg(
                new HeaderMsg(MakeStamp(stampSeconds), m_FrameId),
                (uint)m_Height,
                (uint)m_Width,
                m_DistortionModel,
                new[] { 0.0, 0.0, 0.0, 0.0 },
                new[]
                {
                    focalPixels, 0.0, cx,
                    0.0, focalPixels, cy,
                    0.0, 0.0, 1.0
                },
                new[]
                {
                    1.0, 0.0, 0.0,
                    0.0, 1.0, 0.0,
                    0.0, 0.0, 1.0
                },
                new[]
                {
                    focalPixels, 0.0, cx, 0.0,
                    0.0, focalPixels, cy, 0.0,
                    0.0, 0.0, 1.0, 0.0
                },
                0,
                0,
                new RegionOfInterestMsg());
        }

        static TimeMsg MakeStamp(float seconds)
        {
            int sec = Mathf.FloorToInt(seconds);
            uint nanosec = (uint)Mathf.Clamp(Mathf.RoundToInt((seconds - sec) * 1_000_000_000f), 0, 999_999_999);
            return new TimeMsg(sec, nanosec);
        }
    }
}
