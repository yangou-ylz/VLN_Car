using System;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using UnityEngine.Rendering;
using UnitySensors.Sensor.Camera;

namespace VLN.ROS2
{
    public sealed class VlnFisheyeImagePublisher : MonoBehaviour
    {
        [SerializeField] Camera m_SourceCamera;
        [SerializeField] FisheyeCameraSensor m_SourceSensor;
        [SerializeField] Material m_FisheyeMaterial;
        [SerializeField] string m_TopicName = "/vln/front/image_raw";
        [SerializeField] string m_FrameId = "front_camera_optical_frame";
        [SerializeField] float m_PublishFrequencyHz = 20f;
        [SerializeField] int m_Width = 640;
        [SerializeField] int m_Height = 640;
        [SerializeField] int m_CubemapResolution = 1024;
        [SerializeField] float m_ViewAngleDeg = 190f;

        ROSConnection m_Ros;
        RenderTexture m_Cubemap;
        RenderTexture m_FisheyeTexture;
        Material m_RuntimeMaterial;
        ImageMsg m_Message;
        byte[] m_Data;
        float m_NextPublishTime;
        float m_PendingStamp;
        bool m_RequestPending;

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

        void Start()
        {
            if (m_SourceCamera == null)
            {
                m_SourceCamera = GetComponent<Camera>();
            }
            if (m_SourceSensor == null)
            {
                m_SourceSensor = GetComponent<FisheyeCameraSensor>();
            }
            if (m_SourceCamera == null || m_SourceSensor == null)
            {
                Debug.LogError("VLN_FISHEYE_IMAGE_PUBLISHER_MISSING_SOURCE_CAMERA_OR_SENSOR");
                enabled = false;
                return;
            }
            if (m_FisheyeMaterial == null)
            {
                Debug.LogError("VLN_FISHEYE_IMAGE_PUBLISHER_MISSING_MATERIAL");
                enabled = false;
                return;
            }

            m_SourceCamera.enabled = false;
            m_SourceCamera.targetTexture = null;
            m_Cubemap = new RenderTexture(m_CubemapResolution, m_CubemapResolution, 0, RenderTextureFormat.ARGB32)
            {
                dimension = TextureDimension.Cube
            };
            m_FisheyeTexture = new RenderTexture(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
            m_RuntimeMaterial = new Material(m_FisheyeMaterial);
            m_Data = new byte[m_Width * m_Height * 3];
            m_Message = new ImageMsg
            {
                height = (uint)m_Height,
                width = (uint)m_Width,
                encoding = "rgb8",
                is_bigendian = 0,
                step = (uint)(m_Width * 3),
                data = m_Data
            };

            m_Ros = ROSConnection.GetOrCreateInstance();
            m_Ros.RegisterPublisher<ImageMsg>(m_TopicName, queue_size: 10);
            Debug.Log($"VLN_FISHEYE_IMAGE_PUBLISHER_READY topic={m_TopicName} frame={m_FrameId} {m_Width}x{m_Height} view_angle_deg={m_ViewAngleDeg:F1}");
        }

        void Update()
        {
            if (m_Ros == null || m_RequestPending || Time.time < m_NextPublishTime)
            {
                return;
            }

            m_PendingStamp = Time.time;
            ConfigureMaterial();
            m_SourceCamera.RenderToCubemap(m_Cubemap);
            Graphics.Blit(m_Cubemap, m_FisheyeTexture, m_RuntimeMaterial);
            m_RequestPending = true;
            AsyncGPUReadback.Request(m_FisheyeTexture, 0, TextureFormat.RGB24, OnReadbackComplete);
            m_NextPublishTime = Time.time + 1f / Mathf.Max(0.1f, m_PublishFrequencyHz);
        }

        void OnReadbackComplete(AsyncGPUReadbackRequest request)
        {
            m_RequestPending = false;
            if (!enabled || m_Ros == null || request.hasError)
            {
                if (request.hasError)
                {
                    Debug.LogError("VLN_FISHEYE_IMAGE_READBACK_FAILED topic=" + m_TopicName);
                }
                return;
            }

            var raw = request.GetData<byte>();
            if (raw.Length != m_Data.Length)
            {
                Debug.LogError($"VLN_FISHEYE_IMAGE_READBACK_SIZE_MISMATCH topic={m_TopicName} got={raw.Length} expected={m_Data.Length}");
                return;
            }

            raw.CopyTo(m_Data);
            m_Message.header = new HeaderMsg(MakeStamp(m_PendingStamp), m_FrameId);
            m_Message.data = m_Data;
            m_Ros.Publish(m_TopicName, m_Message);
        }

        void ConfigureMaterial()
        {
            m_RuntimeMaterial.SetFloat("_CameraModel", 5f);
            m_RuntimeMaterial.SetFloat("_Angle", m_ViewAngleDeg);
            m_RuntimeMaterial.SetVector("_kb4", Vector4.zero);
            float halfAngleRad = Mathf.Deg2Rad * m_ViewAngleDeg * 0.5f;
            float focal = (Mathf.Min(m_Width, m_Height) * 0.5f) / Mathf.Max(halfAngleRad, 1e-6f);
            m_RuntimeMaterial.SetFloat("_fx", focal / m_Width);
            m_RuntimeMaterial.SetFloat("_fy", focal / m_Height);
            m_RuntimeMaterial.SetFloat("_cx", 0.5f);
            m_RuntimeMaterial.SetFloat("_cy", 0.5f);
            m_RuntimeMaterial.SetFloat("_resolutionX", m_Width);
            m_RuntimeMaterial.SetFloat("_resolutionY", m_Height);
            m_RuntimeMaterial.SetMatrix("_WorldTransform", Matrix4x4.TRS(Vector3.zero, transform.rotation, Vector3.one));
        }

        void OnDestroy()
        {
            if (m_Cubemap != null)
            {
                m_Cubemap.Release();
            }
            if (m_FisheyeTexture != null)
            {
                m_FisheyeTexture.Release();
            }
            if (m_RuntimeMaterial != null)
            {
                Destroy(m_RuntimeMaterial);
            }
        }

        static TimeMsg MakeStamp(float seconds)
        {
            int sec = Mathf.FloorToInt(seconds);
            uint nanosec = (uint)Mathf.Clamp(Mathf.RoundToInt((seconds - sec) * 1_000_000_000f), 0, 999_999_999);
            return new TimeMsg(sec, nanosec);
        }
    }
}
