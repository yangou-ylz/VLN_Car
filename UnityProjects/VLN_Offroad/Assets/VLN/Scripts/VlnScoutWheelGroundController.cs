using System;
using System.IO;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VlnScoutWheelGroundController : MonoBehaviour
    {
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] WheelCollider m_FrontLeftWheel;
        [SerializeField] WheelCollider m_FrontRightWheel;
        [SerializeField] WheelCollider m_RearLeftWheel;
        [SerializeField] WheelCollider m_RearRightWheel;
        [SerializeField] Transform m_FrontLeftVisual;
        [SerializeField] Transform m_FrontRightVisual;
        [SerializeField] Transform m_RearLeftVisual;
        [SerializeField] Transform m_RearRightVisual;
        [SerializeField] float m_WheelRadiusMeters = 0.16459f;
        [SerializeField] float m_TrackMeters = 0.58306f;
        [SerializeField] float m_WheelMotorDirection = -1f;
        [SerializeField] float m_WheelVisualVerticalOffset = 0.085f;
        [SerializeField] float m_MaxLinearSpeedMetersPerSecond = 2.0f;
        [SerializeField] float m_MaxAngularSpeedRadPerSecond = 1.0f;
        [SerializeField] float m_MaxMotorTorque = 140f;
        [SerializeField] float m_MaxBrakeTorque = 220f;
        [SerializeField] float m_RpmVelocityGain = 1.35f;
        [SerializeField] float m_LongitudinalAssistGain = 1.50f;
        [SerializeField] float m_MaxLongitudinalAssistAcceleration = 1.20f;
        [SerializeField] float m_LongitudinalOverspeedMargin = 0.35f;
        [SerializeField] float m_OverspeedBrakeTorqueRatio = 0.25f;
        [SerializeField] float m_RollingBrakeSpeedThreshold = 0.08f;
        [SerializeField] float m_CommandTimeoutSeconds = 0.75f;

        Rigidbody m_Body;
        float m_CommandedLinearX;
        float m_CommandedAngularZ;
        float m_LastCommandRealtime = -999f;
        int m_CommandCount;
        int m_PhysicsStepCount;
        int m_MotorCommandCount;
        string m_ResultPath;
        bool m_FinalSnapshotWritten;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
        }

        void Start()
        {
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_scout_wheel_ground_controller_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "physics_mode=wheel_ground_contact_wheelcollider_candidate\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"wheel_radius_m={m_WheelRadiusMeters:F5}\n" +
                $"track_m={m_TrackMeters:F5}\n" +
                $"wheel_motor_direction={m_WheelMotorDirection:F0}\n" +
                $"wheel_visual_vertical_offset_m={m_WheelVisualVerticalOffset:F3}\n" +
                $"max_linear_speed_mps={m_MaxLinearSpeedMetersPerSecond:F2}\n" +
                $"max_angular_speed_radps={m_MaxAngularSpeedRadPerSecond:F2}\n" +
                $"max_motor_torque_nm={m_MaxMotorTorque:F2}\n" +
                $"max_brake_torque_nm={m_MaxBrakeTorque:F2}\n" +
                $"rpm_velocity_gain={m_RpmVelocityGain:F2}\n" +
                $"longitudinal_assist_gain={m_LongitudinalAssistGain:F2}\n" +
                $"max_longitudinal_assist_accel_mps2={m_MaxLongitudinalAssistAcceleration:F2}\n" +
                $"longitudinal_overspeed_margin_mps={m_LongitudinalOverspeedMargin:F2}\n" +
                $"overspeed_brake_torque_ratio={m_OverspeedBrakeTorqueRatio:F2}\n" +
                $"rolling_brake_speed_threshold_mps={m_RollingBrakeSpeedThreshold:F2}\n" +
                $"wheel_collider_count={CountWheelColliders()}\n" +
                $"rigidbody_mass_kg={m_Body.mass:F2}\n");

            ROSConnection.GetOrCreateInstance().Subscribe<TwistMsg>(m_CmdVelTopic, OnCmdVel);
            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_READY topic={m_CmdVelTopic} wheels={CountWheelColliders()}");
        }

        void FixedUpdate()
        {
            m_PhysicsStepCount++;
            if (HasRecentCommand())
            {
                ApplyWheelTargets(m_CommandedLinearX, m_CommandedAngularZ);
            }
            else
            {
                ApplyBrake();
            }
        }

        void LateUpdate()
        {
            UpdateWheelVisual(m_FrontLeftWheel, m_FrontLeftVisual);
            UpdateWheelVisual(m_FrontRightWheel, m_FrontRightVisual);
            UpdateWheelVisual(m_RearLeftWheel, m_RearLeftVisual);
            UpdateWheelVisual(m_RearRightWheel, m_RearRightVisual);
        }

        void OnCmdVel(TwistMsg msg)
        {
            m_CommandedLinearX = Mathf.Clamp((float)msg.linear.x, -m_MaxLinearSpeedMetersPerSecond, m_MaxLinearSpeedMetersPerSecond);
            m_CommandedAngularZ = Mathf.Clamp((float)msg.angular.z, -m_MaxAngularSpeedRadPerSecond, m_MaxAngularSpeedRadPerSecond);
            m_LastCommandRealtime = Time.realtimeSinceStartup;
            m_CommandCount++;

            if (m_CommandCount == 1 || m_CommandCount % 10 == 0)
            {
                File.AppendAllText(m_ResultPath,
                    $"cmd_vel_received={m_CommandCount};time={DateTime.UtcNow:O};linear_x={m_CommandedLinearX:F3};angular_z={m_CommandedAngularZ:F3};speed={m_Body.velocity.magnitude:F3}\n");
                Debug.Log($"VLN_SCOUT_WHEEL_GROUND_CMD count={m_CommandCount} linear_x={m_CommandedLinearX:F3} angular_z={m_CommandedAngularZ:F3}");
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

        bool HasRecentCommand()
        {
            return m_CommandCount > 0 && Time.realtimeSinceStartup - m_LastCommandRealtime <= Mathf.Max(0.1f, m_CommandTimeoutSeconds);
        }

        void ApplyWheelTargets(float linearX, float angularZ)
        {
            float radius = Mathf.Max(0.01f, m_WheelRadiusMeters);
            float halfTrack = Mathf.Max(0.01f, m_TrackMeters) * 0.5f;
            float leftRadPerSecond = (linearX - angularZ * halfTrack) / radius;
            float rightRadPerSecond = (linearX + angularZ * halfTrack) / radius;
            float motorDirection = m_WheelMotorDirection >= 0f ? 1f : -1f;
            float leftRpm = motorDirection * leftRadPerSecond * 60f / (2f * Mathf.PI);
            float rightRpm = motorDirection * rightRadPerSecond * 60f / (2f * Mathf.PI);
            float bodyForwardSpeed = Vector3.Dot(m_Body.velocity, transform.forward);
            bool overspeeding =
                linearX > 0.05f && bodyForwardSpeed > linearX + Mathf.Max(0.02f, m_LongitudinalOverspeedMargin) ||
                linearX < -0.05f && bodyForwardSpeed < linearX - Mathf.Max(0.02f, m_LongitudinalOverspeedMargin);

            ApplyWheelMotor(m_FrontLeftWheel, leftRpm, overspeeding);
            ApplyWheelMotor(m_RearLeftWheel, leftRpm, overspeeding);
            ApplyWheelMotor(m_FrontRightWheel, rightRpm, overspeeding);
            ApplyWheelMotor(m_RearRightWheel, rightRpm, overspeeding);
            ApplyLongitudinalAssist(linearX, bodyForwardSpeed, overspeeding);
            m_MotorCommandCount++;
        }

        void ApplyLongitudinalAssist(float linearX, float bodyForwardSpeed, bool overspeeding)
        {
            if (overspeeding || Mathf.Abs(linearX) < 0.03f || m_MaxLongitudinalAssistAcceleration <= 0f)
            {
                return;
            }

            float speedError = linearX - bodyForwardSpeed;
            float assistAcceleration = Mathf.Clamp(
                speedError * Mathf.Max(0f, m_LongitudinalAssistGain),
                -m_MaxLongitudinalAssistAcceleration,
                m_MaxLongitudinalAssistAcceleration);
            m_Body.AddForce(transform.forward * assistAcceleration, ForceMode.Acceleration);
        }

        void ApplyWheelMotor(WheelCollider wheel, float targetRpm, bool overspeeding)
        {
            if (wheel == null)
            {
                return;
            }

            if (overspeeding)
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = Mathf.Max(wheel.brakeTorque, m_MaxBrakeTorque * Mathf.Clamp01(m_OverspeedBrakeTorqueRatio));
                return;
            }

            float torque = Mathf.Clamp((targetRpm - wheel.rpm) * m_RpmVelocityGain, -m_MaxMotorTorque, m_MaxMotorTorque);
            wheel.brakeTorque = Mathf.Abs(targetRpm) < 0.5f && Mathf.Abs(wheel.rpm) < Mathf.Max(0.5f, m_RollingBrakeSpeedThreshold * 60f) ? m_MaxBrakeTorque * 0.20f : 0f;
            wheel.motorTorque = torque;
        }

        void ApplyBrake()
        {
            ApplyWheelBrake(m_FrontLeftWheel);
            ApplyWheelBrake(m_FrontRightWheel);
            ApplyWheelBrake(m_RearLeftWheel);
            ApplyWheelBrake(m_RearRightWheel);
        }

        void ApplyWheelBrake(WheelCollider wheel)
        {
            if (wheel == null)
            {
                return;
            }

            wheel.motorTorque = 0f;
            wheel.brakeTorque = m_MaxBrakeTorque;
        }

        void UpdateWheelVisual(WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            position += transform.up * m_WheelVisualVerticalOffset;
            visual.SetPositionAndRotation(position, rotation);
        }

        int CountWheelColliders()
        {
            int count = 0;
            if (m_FrontLeftWheel != null) count++;
            if (m_FrontRightWheel != null) count++;
            if (m_RearLeftWheel != null) count++;
            if (m_RearRightWheel != null) count++;
            return count;
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
                $"cmd_vel_count={m_CommandCount}\n" +
                $"physics_step_count={m_PhysicsStepCount}\n" +
                $"motor_command_count={m_MotorCommandCount}\n" +
                $"final_position={transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}\n" +
                $"final_yaw_deg={transform.eulerAngles.y:F3}\n" +
                $"final_speed_mps={m_Body.velocity.magnitude:F3}\n" +
                $"front_left_rpm={WheelRpm(m_FrontLeftWheel):F3}\n" +
                $"front_right_rpm={WheelRpm(m_FrontRightWheel):F3}\n" +
                $"rear_left_rpm={WheelRpm(m_RearLeftWheel):F3}\n" +
                $"rear_right_rpm={WheelRpm(m_RearRightWheel):F3}\n");
        }

        static float WheelRpm(WheelCollider wheel)
        {
            return wheel != null ? wheel.rpm : 0f;
        }
    }
}
