using System;
using System.Collections.Generic;
using System.IO;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnScoutWheelJointCommandProbe : MonoBehaviour
    {
        static readonly string[] WheelLinkNames =
        {
            "front_left_wheel_link",
            "front_right_wheel_link",
            "rear_left_wheel_link",
            "rear_right_wheel_link",
        };

        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] float m_WheelRadiusMeters = 0.16459f;
        [SerializeField] float m_TrackMeters = 0.58306f;
        [SerializeField] float m_DriveDamping = 20f;
        [SerializeField] float m_MinForceLimit = 1000f;

        readonly Dictionary<string, ArticulationBody> m_Wheels = new();
        readonly Dictionary<string, float> m_LastTargetsDegPerSecond = new();
        readonly HashSet<string> m_EverNonzeroTargets = new();
        string m_ResultPath;
        int m_CommandCount;
        bool m_FinalSnapshotWritten;

        void Start()
        {
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_scout_wheel_joint_command_probe_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));

            BindWheelArticulations();
            ConfigureWheelDrives();
            WriteHeader();

            ROSConnection.GetOrCreateInstance().Subscribe<TwistMsg>(m_CmdVelTopic, OnCmdVel);
            Debug.Log($"VLN_SCOUT_WHEEL_JOINT_PROBE_READY topic={m_CmdVelTopic} wheels={m_Wheels.Count}");
        }

        void OnCmdVel(TwistMsg msg)
        {
            float linear = (float)msg.linear.x;
            float angular = (float)msg.angular.z;
            float radius = Mathf.Max(0.01f, m_WheelRadiusMeters);
            float halfTrack = Mathf.Max(0.01f, m_TrackMeters) * 0.5f;

            float leftRadPerSecond = (linear - angular * halfTrack) / radius;
            float rightRadPerSecond = (linear + angular * halfTrack) / radius;

            ApplyTarget("front_left_wheel_link", leftRadPerSecond);
            ApplyTarget("rear_left_wheel_link", leftRadPerSecond);
            ApplyTarget("front_right_wheel_link", rightRadPerSecond);
            ApplyTarget("rear_right_wheel_link", rightRadPerSecond);

            m_CommandCount++;
            File.AppendAllText(m_ResultPath,
                $"wheel_cmd_received={m_CommandCount};time={DateTime.UtcNow:O};linear_x={linear:F3};angular_z={angular:F3};" +
                $"front_left_dps={GetTarget("front_left_wheel_link"):F3};front_right_dps={GetTarget("front_right_wheel_link"):F3};" +
                $"rear_left_dps={GetTarget("rear_left_wheel_link"):F3};rear_right_dps={GetTarget("rear_right_wheel_link"):F3}\n");

            if (m_CommandCount == 1 || m_CommandCount % 10 == 0)
            {
                Debug.Log($"VLN_SCOUT_WHEEL_JOINT_CMD count={m_CommandCount} left_dps={GetTarget("front_left_wheel_link"):F3} right_dps={GetTarget("front_right_wheel_link"):F3}");
            }
        }

        void OnApplicationQuit()
        {
            WriteFinalSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalSnapshot();
        }

        void BindWheelArticulations()
        {
            m_Wheels.Clear();
            m_LastTargetsDegPerSecond.Clear();
            m_EverNonzeroTargets.Clear();

            foreach (string wheelName in WheelLinkNames)
            {
                Transform wheel = FindDeepChild(transform, wheelName);
                if (wheel == null || !wheel.TryGetComponent<ArticulationBody>(out var body))
                {
                    continue;
                }

                m_Wheels[wheelName] = body;
                m_LastTargetsDegPerSecond[wheelName] = 0f;
            }
        }

        void ConfigureWheelDrives()
        {
            foreach (var body in m_Wheels.Values)
            {
                ArticulationDrive drive = body.xDrive;
                drive.stiffness = 0f;
                drive.damping = Mathf.Max(drive.damping, m_DriveDamping);
                if (!float.IsInfinity(drive.forceLimit) && drive.forceLimit < m_MinForceLimit)
                {
                    drive.forceLimit = m_MinForceLimit;
                }
                drive.targetVelocity = 0f;
                body.xDrive = drive;
            }
        }

        void ApplyTarget(string wheelName, float radPerSecond)
        {
            if (!m_Wheels.TryGetValue(wheelName, out var body))
            {
                return;
            }

            float degPerSecond = radPerSecond * Mathf.Rad2Deg;
            ArticulationDrive drive = body.xDrive;
            drive.targetVelocity = degPerSecond;
            body.xDrive = drive;
            m_LastTargetsDegPerSecond[wheelName] = degPerSecond;
            if (Mathf.Abs(degPerSecond) > 0.001f)
            {
                m_EverNonzeroTargets.Add(wheelName);
            }
        }

        void WriteHeader()
        {
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "probe_mode=wheel_joint_drive_signal_only_existing_rig_still_moves_vehicle\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"wheel_radius_m={m_WheelRadiusMeters:F5}\n" +
                $"track_m={m_TrackMeters:F5}\n" +
                $"wheel_found_count={m_Wheels.Count}\n" +
                $"wheel_names={string.Join(",", m_Wheels.Keys)}\n");
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
                $"wheel_command_count={m_CommandCount}\n" +
                $"nonzero_target_count={m_EverNonzeroTargets.Count}\n" +
                $"front_left_final_dps={GetTarget("front_left_wheel_link"):F3}\n" +
                $"front_right_final_dps={GetTarget("front_right_wheel_link"):F3}\n" +
                $"rear_left_final_dps={GetTarget("rear_left_wheel_link"):F3}\n" +
                $"rear_right_final_dps={GetTarget("rear_right_wheel_link"):F3}\n");
        }

        float GetTarget(string wheelName)
        {
            return m_LastTargetsDegPerSecond.TryGetValue(wheelName, out float target) ? target : 0f;
        }

        static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform result = FindDeepChild(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
