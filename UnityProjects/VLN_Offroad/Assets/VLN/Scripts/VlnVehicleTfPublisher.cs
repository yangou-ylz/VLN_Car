using System;
using System.Collections.Generic;
using System.IO;
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
        [Serializable]
        sealed class AdditionalSensorFrame
        {
            [SerializeField] string m_FrameId;
            [SerializeField] Transform m_Transform;

            public string FrameId => m_FrameId;
            public Transform Transform => m_Transform;
        }

        [SerializeField] string m_TfTopic = "/tf";
        [SerializeField] string m_MapFrame = "map";
        [SerializeField] string m_BaseFrame = "base_link";
        [SerializeField] string m_CameraFrame = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrame = "lidar_link";
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] Transform m_CameraTransform;
        [SerializeField] Transform m_LidarTransform;
        [SerializeField] AdditionalSensorFrame[] m_AdditionalSensorFrames = Array.Empty<AdditionalSensorFrame>();
        [SerializeField] float m_TfFrequencyHz = 10f;
        [SerializeField] float m_VehicleSpeedMetersPerSecond = 1.4f;
        [SerializeField] float m_PathStartZ = -24f;
        [SerializeField] float m_PathEndZ = 24f;
        [SerializeField] bool m_EnableKinematicMotion = true;
        [SerializeField] bool m_AutopilotUntilFirstCommand = false;
        [SerializeField] float m_CommandTimeoutSeconds = 0.75f;
        [SerializeField] float m_MaxLinearSpeedMetersPerSecond = 2.0f;
        [SerializeField] float m_MaxAngularSpeedRadPerSecond = 1.2f;
        [SerializeField] bool m_EnableObstacleCollisionStop = true;
        [SerializeField] Vector3 m_CollisionHalfExtents = new(0.62f, 0.42f, 0.95f);
        [SerializeField] float m_CollisionCenterHeight = 0.52f;
        [SerializeField] float m_CollisionSkinMeters = 0.05f;
        [SerializeField] LayerMask m_ObstacleLayerMask = ~0;

        const float TerrainSize = 80f;
        const float TerrainHeight = 7f;

        ROSConnection m_Ros;
        float m_StartRealtime;
        float m_NextPublishTime;
        Vector3 m_LastPosition;
        float m_CommandedLinearX;
        float m_CommandedAngularZ;
        float m_LastCommandRealtime = -999f;
        int m_CommandCount;
        string m_ControlResultPath;
        bool m_FinalControlSnapshotWritten;
        bool m_Registered;
        int m_CollisionBlockCount;
        bool m_CollisionCurrentlyBlocked;

        void Start()
        {
            m_StartRealtime = Time.realtimeSinceStartup;
            m_LastPosition = transform.position;
            m_ControlResultPath = Path.Combine(Application.dataPath, "../Logs/vln_vehicle_control_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ControlResultPath));
            File.WriteAllText(m_ControlResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                "cmd_vel_type=geometry_msgs/msg/Twist\n" +
                $"autopilot_until_first_command={m_AutopilotUntilFirstCommand}\n" +
                $"command_timeout_seconds={m_CommandTimeoutSeconds:F2}\n" +
                $"max_linear_speed_mps={m_MaxLinearSpeedMetersPerSecond:F2}\n" +
                $"max_angular_speed_radps={m_MaxAngularSpeedRadPerSecond:F2}\n" +
                $"enable_kinematic_motion={m_EnableKinematicMotion}\n" +
                $"obstacle_collision_stop={m_EnableObstacleCollisionStop}\n" +
                $"collision_half_extents={m_CollisionHalfExtents.x:F2},{m_CollisionHalfExtents.y:F2},{m_CollisionHalfExtents.z:F2}\n");

            m_Ros = ROSConnection.GetOrCreateInstance();
            m_Ros.RegisterPublisher<TFMessageMsg>(m_TfTopic, queue_size: 10);
            m_Ros.Subscribe<TwistMsg>(m_CmdVelTopic, OnCmdVel);
            m_Registered = true;
            Debug.Log($"VLN_VEHICLE_TF_READY topic={m_TfTopic} map={m_MapFrame} base={m_BaseFrame} camera={m_CameraFrame} lidar={m_LidarFrame} additional_sensor_frames={m_AdditionalSensorFrames.Length} cmd_vel={m_CmdVelTopic}");
        }

        void Update()
        {
            if (m_EnableKinematicMotion)
            {
                UpdateVehiclePose();
            }

            if (!m_Registered || Time.time < m_NextPublishTime)
            {
                return;
            }

            m_NextPublishTime = Time.time + 1f / Mathf.Max(0.1f, m_TfFrequencyHz);
            PublishTf();
        }

        void UpdateVehiclePose()
        {
            if (HasRecentCommand())
            {
                UpdateVehiclePoseFromCommand();
                return;
            }

            if (m_CommandCount == 0 && m_AutopilotUntilFirstCommand)
            {
                UpdateVehiclePoseFromAutopilot();
                return;
            }

            SnapVehicleToTerrain();
        }

        void UpdateVehiclePoseFromAutopilot()
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

        void UpdateVehiclePoseFromCommand()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
            float linear = Mathf.Clamp(m_CommandedLinearX, -m_MaxLinearSpeedMetersPerSecond, m_MaxLinearSpeedMetersPerSecond);
            float angular = Mathf.Clamp(m_CommandedAngularZ, -m_MaxAngularSpeedRadPerSecond, m_MaxAngularSpeedRadPerSecond);

            Quaternion nextRotation = Quaternion.Euler(0f, transform.eulerAngles.y + angular * Mathf.Rad2Deg * dt, 0f);
            Vector3 nextPosition = transform.position + (nextRotation * Vector3.forward) * (linear * dt);
            float halfBound = TerrainSize * 0.46f;
            nextPosition.x = Mathf.Clamp(nextPosition.x, -halfBound, halfBound);
            nextPosition.z = Mathf.Clamp(nextPosition.z, -halfBound, halfBound);
            nextPosition.y = TerrainWorldY(nextPosition.x, nextPosition.z);

            if (Mathf.Abs(linear) > 0.001f && IsObstacleBlocking(nextPosition, nextRotation))
            {
                Vector3 currentPosition = transform.position;
                currentPosition.y = TerrainWorldY(currentPosition.x, currentPosition.z);
                transform.SetPositionAndRotation(currentPosition, nextRotation);
                m_LastPosition = currentPosition;

                if (!m_CollisionCurrentlyBlocked)
                {
                    m_CollisionCurrentlyBlocked = true;
                    m_CollisionBlockCount++;
                    string line = $"collision_blocked={m_CollisionBlockCount};time={DateTime.UtcNow:O};position={currentPosition.x:F3},{currentPosition.y:F3},{currentPosition.z:F3};yaw_deg={nextRotation.eulerAngles.y:F3}";
                    File.AppendAllText(m_ControlResultPath, line + "\n");
                    Debug.Log($"VLN_VEHICLE_COLLISION_BLOCKED count={m_CollisionBlockCount} position={currentPosition.x:F3},{currentPosition.y:F3},{currentPosition.z:F3}");
                }
                return;
            }

            m_CollisionCurrentlyBlocked = false;
            transform.SetPositionAndRotation(nextPosition, nextRotation);
            m_LastPosition = nextPosition;
        }

        bool IsObstacleBlocking(Vector3 nextPosition, Quaternion nextRotation)
        {
            if (!m_EnableObstacleCollisionStop)
            {
                return false;
            }

            Vector3 center = nextPosition + Vector3.up * m_CollisionCenterHeight;
            Vector3 halfExtents = new(
                Mathf.Max(0.05f, m_CollisionHalfExtents.x + m_CollisionSkinMeters),
                Mathf.Max(0.05f, m_CollisionHalfExtents.y),
                Mathf.Max(0.05f, m_CollisionHalfExtents.z + m_CollisionSkinMeters));

            var hits = Physics.OverlapBox(center, halfExtents, nextRotation, m_ObstacleLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit == null || hit.transform == null)
                {
                    continue;
                }

                if (hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                string objectName = hit.gameObject.name;
                if (objectName.StartsWith("OffroadTerrain_", StringComparison.Ordinal) || objectName.StartsWith("Offroad_DirtRoad_", StringComparison.Ordinal))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        bool HasRecentCommand()
        {
            return m_CommandCount > 0 && Time.realtimeSinceStartup - m_LastCommandRealtime <= Mathf.Max(0.1f, m_CommandTimeoutSeconds);
        }

        void SnapVehicleToTerrain()
        {
            Vector3 position = transform.position;
            position.y = TerrainWorldY(position.x, position.z);
            transform.position = position;
            m_LastPosition = position;
        }

        void OnCmdVel(TwistMsg msg)
        {
            m_CommandedLinearX = Mathf.Clamp((float)msg.linear.x, -m_MaxLinearSpeedMetersPerSecond, m_MaxLinearSpeedMetersPerSecond);
            m_CommandedAngularZ = Mathf.Clamp((float)msg.angular.z, -m_MaxAngularSpeedRadPerSecond, m_MaxAngularSpeedRadPerSecond);
            m_LastCommandRealtime = Time.realtimeSinceStartup;
            m_CommandCount++;

            string line = $"cmd_vel_received={m_CommandCount};time={DateTime.UtcNow:O};linear_x={m_CommandedLinearX:F3};angular_z={m_CommandedAngularZ:F3}";
            File.AppendAllText(m_ControlResultPath, line + "\n");
            if (m_CommandCount == 1 || m_CommandCount % 10 == 0)
            {
                Debug.Log($"VLN_CMD_VEL_RX count={m_CommandCount} linear_x={m_CommandedLinearX:F3} angular_z={m_CommandedAngularZ:F3}");
            }
        }

        void OnApplicationQuit()
        {
            WriteFinalControlSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalControlSnapshot();
        }

        void WriteFinalControlSnapshot()
        {
            if (m_FinalControlSnapshotWritten || string.IsNullOrEmpty(m_ControlResultPath))
            {
                return;
            }

            m_FinalControlSnapshotWritten = true;
            Vector3 euler = transform.eulerAngles;
            File.AppendAllText(m_ControlResultPath,
                $"finished={DateTime.UtcNow:O}\n" +
                $"cmd_vel_count={m_CommandCount}\n" +
                $"collision_block_count={m_CollisionBlockCount}\n" +
                $"final_position={transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}\n" +
                $"final_yaw_deg={euler.y:F3}\n");
        }

        void OnDrawGizmosSelected()
        {
            if (!m_EnableObstacleCollisionStop)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.85f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * m_CollisionCenterHeight, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, m_CollisionHalfExtents * 2f);
            Gizmos.matrix = previous;
        }

        void PublishTf()
        {
            TimeMsg stamp = MakeStamp(Time.time);
            TransformStampedMsg mapToBase = MakeTransform(m_MapFrame, m_BaseFrame, transform.position, transform.rotation, stamp);
            var transforms = new List<TransformStampedMsg>
            {
                mapToBase,
                MakeSensorTransform(m_CameraFrame, m_CameraTransform, stamp),
                MakeSensorTransform(m_LidarFrame, m_LidarTransform, stamp),
            };

            foreach (var frame in m_AdditionalSensorFrames)
            {
                if (frame == null || string.IsNullOrWhiteSpace(frame.FrameId))
                {
                    continue;
                }

                transforms.Add(MakeSensorTransform(frame.FrameId, frame.Transform, stamp));
            }

            m_Ros.Publish(m_TfTopic, new TFMessageMsg(transforms.ToArray()));
        }

        TransformStampedMsg MakeSensorTransform(string childFrame, Transform sensorTransform, TimeMsg stamp)
        {
            Vector3 localPosition = Vector3.zero;
            Quaternion localRotation = Quaternion.identity;
            if (sensorTransform != null)
            {
                localPosition = transform.InverseTransformPoint(sensorTransform.position);
                localRotation = Quaternion.Inverse(transform.rotation) * sensorTransform.rotation;
            }

            return MakeTransform(m_BaseFrame, childFrame, localPosition, localRotation, stamp);
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
