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
        [SerializeField] float m_WheelMotorDirection = 1f;
        [SerializeField] float m_WheelYawDirection = 1f;
        [SerializeField] float m_WheelLinearMotorScale = 0f;
        [SerializeField] float m_WheelAngularMotorScale = 0f;
        [SerializeField] float m_WheelVisualVerticalOffset = 0.085f;
        [SerializeField] float m_WheelVisualForwardRollDirection = 1f;
        [SerializeField] float m_WheelVisualAngularSmoothing = 14f;
        [SerializeField] float m_MaxLinearSpeedMetersPerSecond = 2.0f;
        [SerializeField] float m_MaxAngularSpeedRadPerSecond = 1.0f;
        [SerializeField] float m_MaxMotorTorque = 160f;
        [SerializeField] float m_MaxBrakeTorque = 220f;
        [SerializeField] float m_RpmVelocityGain = 0.90f;
        [SerializeField] float m_LongitudinalAssistGain = 3.00f;
        [SerializeField] float m_MaxLongitudinalAssistAcceleration = 4.00f;
        [SerializeField] float m_LongitudinalVelocityKp = 3.20f;
        [SerializeField] float m_LongitudinalVelocityKi = 0.12f;
        [SerializeField] float m_LongitudinalVelocityKd = 0.45f;
        [SerializeField] float m_LongitudinalIntegralLimit = 1.20f;
        [SerializeField] float m_YawAssistGain = 3.0f;
        [SerializeField] float m_MaxYawAssistAngularAcceleration = 5.0f;
        [SerializeField] float m_YawRateKp = 8.50f;
        [SerializeField] float m_YawRateKi = 0.08f;
        [SerializeField] float m_YawRateKd = 0.55f;
        [SerializeField] float m_YawRateIntegralLimit = 0.70f;
        [SerializeField] bool m_EnableStraightHeadingHold = true;
        [SerializeField] float m_StraightHeadingHoldKp = 4.2f;
        [SerializeField] float m_StraightHeadingHoldKd = 1.8f;
        [SerializeField] float m_MaxStraightHeadingHoldAngularAcceleration = 3.5f;
        [SerializeField] float m_LateralDampingGain = 9.0f;
        [SerializeField] float m_MaxLateralDampingAcceleration = 8.0f;
        [SerializeField] float m_StopVelocityDampingGain = 7.0f;
        [SerializeField] float m_MaxStopDampingAcceleration = 6.0f;
        [SerializeField] float m_StopYawDampingGain = 36.0f;
        [SerializeField] float m_DirectStopVelocityDampingGain = 10.0f;
        [SerializeField] float m_DirectStopYawDampingGain = 60.0f;
        [SerializeField] float m_PureTurnTranslationDampingGain = 14.0f;
        [SerializeField] float m_MaxPureTurnDampingAcceleration = 9.0f;
        [SerializeField] float m_LongitudinalOverspeedMargin = 0.35f;
        [SerializeField] float m_OverspeedBrakeTorqueRatio = 0.25f;
        [SerializeField] float m_RollingBrakeSpeedThreshold = 0.08f;
        [SerializeField] float m_CommandTimeoutSeconds = 0.18f;
        [SerializeField] float m_GrassRollingResistanceAcceleration = 0.12f;
        [SerializeField] float m_StoneRollingResistanceAcceleration = 0.03f;
        [SerializeField] float m_SandRollingResistanceAcceleration = 0.32f;
        [SerializeField] float m_GrassTractionAssistReduction = 0.05f;
        [SerializeField] float m_SandTractionAssistReduction = 0.16f;
        [SerializeField] float m_SandLateralDampingReduction = 0.20f;

        Rigidbody m_Body;
        float m_CommandedLinearX;
        float m_CommandedAngularZ;
        float m_LastCommandRealtime = -999f;
        int m_CommandCount;
        int m_PhysicsStepCount;
        int m_MotorCommandCount;
        int m_RoadContactSteps;
        int m_BridgeContactSteps;
        int m_ShortRampContactSteps;
        int m_ChallengeSurfaceContactSteps;
        int m_ChallengeObstacleContactSteps;
        int m_ChallengePhysicsProxyContactSteps;
        int m_ChallengeMaterialResistanceSteps;
        int m_GrassContactSteps;
        int m_StoneContactSteps;
        int m_SandContactSteps;
        int m_GrassSurfaceContactSteps;
        int m_StoneSurfaceContactSteps;
        int m_SandSurfaceContactSteps;
        int m_GrassPhysicsProxyContactSteps;
        int m_StonePhysicsProxyContactSteps;
        int m_SandPhysicsProxyContactSteps;
        int m_GrassWheelHitCount;
        int m_StoneWheelHitCount;
        int m_SandWheelHitCount;
        int m_TerrainContactSteps;
        int m_OtherContactSteps;
        int m_NoWheelContactSteps;
        int m_WheelVisualDirectionReversalCount;
        float m_WheelVisualTotalAbsRollDegrees;
        float m_MinBodyHeight = float.PositiveInfinity;
        float m_MaxBodyHeight = float.NegativeInfinity;
        float m_MinWheelGroundHeight = float.PositiveInfinity;
        float m_MaxWheelGroundHeight = float.NegativeInfinity;
        float m_MinGrassWheelGroundHeight = float.PositiveInfinity;
        float m_MaxGrassWheelGroundHeight = float.NegativeInfinity;
        float m_MinStoneWheelGroundHeight = float.PositiveInfinity;
        float m_MaxStoneWheelGroundHeight = float.NegativeInfinity;
        float m_MinSandWheelGroundHeight = float.PositiveInfinity;
        float m_MaxSandWheelGroundHeight = float.NegativeInfinity;
        float m_MinGrassBodyHeight = float.PositiveInfinity;
        float m_MaxGrassBodyHeight = float.NegativeInfinity;
        float m_MinStoneBodyHeight = float.PositiveInfinity;
        float m_MaxStoneBodyHeight = float.NegativeInfinity;
        float m_MinSandBodyHeight = float.PositiveInfinity;
        float m_MaxSandBodyHeight = float.NegativeInfinity;
        float m_GrassSpeedSum;
        float m_StoneSpeedSum;
        float m_SandSpeedSum;
        float m_CurrentGrassContactFraction;
        float m_CurrentStoneContactFraction;
        float m_CurrentSandContactFraction;
        Transform[] m_VisualWheelTransforms;
        Quaternion[] m_VisualRestRootRotations;
        float[] m_VisualRollDegrees;
        float[] m_VisualAngularSpeeds;
        float m_LongitudinalSpeedIntegral;
        float m_PreviousLongitudinalSpeedError;
        float m_YawRateIntegral;
        float m_PreviousYawRateError;
        float m_YawServoRate;
        bool m_StraightHeadingHoldActive;
        float m_StraightHeadingHoldYawDegrees;
        string m_ResultPath;
        bool m_FinalSnapshotWritten;

        struct ContactStepState
        {
            public bool Any;
            public bool Road;
            public bool Bridge;
            public bool ShortRamp;
            public bool ChallengeSurface;
            public bool ChallengeObstacle;
            public bool ChallengePhysicsProxy;
            public bool Terrain;
            public bool Other;
            public bool Grass;
            public bool Stone;
            public bool Sand;
            public bool GrassSurface;
            public bool StoneSurface;
            public bool SandSurface;
            public bool GrassPhysicsProxy;
            public bool StonePhysicsProxy;
            public bool SandPhysicsProxy;
            public int GrassWheelHits;
            public int StoneWheelHits;
            public int SandWheelHits;
        }

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
        }

        void Start()
        {
            InitializeWheelVisualState();
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_scout_wheel_ground_controller_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "physics_mode=wheel_ground_contact_wheelcollider_candidate\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"wheel_radius_m={m_WheelRadiusMeters:F5}\n" +
                $"track_m={m_TrackMeters:F5}\n" +
                $"wheel_motor_direction={m_WheelMotorDirection:F0}\n" +
                $"wheel_yaw_direction={m_WheelYawDirection:F0}\n" +
                $"wheel_linear_motor_scale={m_WheelLinearMotorScale:F2}\n" +
                $"wheel_angular_motor_scale={m_WheelAngularMotorScale:F2}\n" +
                $"wheel_visual_vertical_offset_m={m_WheelVisualVerticalOffset:F3}\n" +
                "wheel_visual_rotation_mode=accumulated_roll_root_x\n" +
                $"wheel_visual_forward_roll_direction={m_WheelVisualForwardRollDirection:F0}\n" +
                $"wheel_visual_angular_smoothing={m_WheelVisualAngularSmoothing:F2}\n" +
                $"max_linear_speed_mps={m_MaxLinearSpeedMetersPerSecond:F2}\n" +
                $"max_angular_speed_radps={m_MaxAngularSpeedRadPerSecond:F2}\n" +
                $"max_motor_torque_nm={m_MaxMotorTorque:F2}\n" +
                $"max_brake_torque_nm={m_MaxBrakeTorque:F2}\n" +
                $"rpm_velocity_gain={m_RpmVelocityGain:F2}\n" +
                $"longitudinal_assist_gain={m_LongitudinalAssistGain:F2}\n" +
                $"max_longitudinal_assist_accel_mps2={m_MaxLongitudinalAssistAcceleration:F2}\n" +
                $"longitudinal_velocity_pid={m_LongitudinalVelocityKp:F2},{m_LongitudinalVelocityKi:F2},{m_LongitudinalVelocityKd:F2}\n" +
                $"yaw_assist_gain={m_YawAssistGain:F2}\n" +
                $"max_yaw_assist_angular_accel={m_MaxYawAssistAngularAcceleration:F2}\n" +
                $"yaw_rate_pid={m_YawRateKp:F2},{m_YawRateKi:F2},{m_YawRateKd:F2}\n" +
                $"straight_heading_hold_enabled={(m_EnableStraightHeadingHold ? 1 : 0)}\n" +
                $"straight_heading_hold_pd={m_StraightHeadingHoldKp:F2},{m_StraightHeadingHoldKd:F2}\n" +
                $"max_straight_heading_hold_angular_accel={m_MaxStraightHeadingHoldAngularAcceleration:F2}\n" +
                $"lateral_damping_gain={m_LateralDampingGain:F2}\n" +
                $"max_lateral_damping_accel_mps2={m_MaxLateralDampingAcceleration:F2}\n" +
                $"stop_velocity_damping_gain={m_StopVelocityDampingGain:F2}\n" +
                $"max_stop_damping_accel_mps2={m_MaxStopDampingAcceleration:F2}\n" +
                $"direct_stop_velocity_damping_gain={m_DirectStopVelocityDampingGain:F2}\n" +
                $"direct_stop_yaw_damping_gain={m_DirectStopYawDampingGain:F2}\n" +
                $"pure_turn_translation_damping_gain={m_PureTurnTranslationDampingGain:F2}\n" +
                $"longitudinal_overspeed_margin_mps={m_LongitudinalOverspeedMargin:F2}\n" +
                $"overspeed_brake_torque_ratio={m_OverspeedBrakeTorqueRatio:F2}\n" +
                $"rolling_brake_speed_threshold_mps={m_RollingBrakeSpeedThreshold:F2}\n" +
                $"command_timeout_seconds={m_CommandTimeoutSeconds:F2}\n" +
                $"grass_rolling_resistance_accel_mps2={m_GrassRollingResistanceAcceleration:F2}\n" +
                $"stone_rolling_resistance_accel_mps2={m_StoneRollingResistanceAcceleration:F2}\n" +
                $"sand_rolling_resistance_accel_mps2={m_SandRollingResistanceAcceleration:F2}\n" +
                $"grass_traction_assist_reduction={m_GrassTractionAssistReduction:F2}\n" +
                $"sand_traction_assist_reduction={m_SandTractionAssistReduction:F2}\n" +
                $"sand_lateral_damping_reduction={m_SandLateralDampingReduction:F2}\n" +
                $"wheel_collider_count={CountWheelColliders()}\n" +
                $"rigidbody_mass_kg={m_Body.mass:F2}\n");

            ROSConnection.GetOrCreateInstance().Subscribe<TwistMsg>(m_CmdVelTopic, OnCmdVel);
            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_READY topic={m_CmdVelTopic} wheels={CountWheelColliders()}");
        }

        void FixedUpdate()
        {
            m_PhysicsStepCount++;
            SampleWheelContacts();
            if (HasRecentCommand())
            {
                ApplyWheelTargets(m_CommandedLinearX, m_CommandedAngularZ);
            }
            else
            {
                ApplyBrake();
                ApplyStopDamping();
                ResetPidState();
            }

            ApplyChallengeMaterialForces();
        }

        void LateUpdate()
        {
            UpdateWheelVisual(0, m_FrontLeftWheel, m_FrontLeftVisual);
            UpdateWheelVisual(1, m_FrontRightWheel, m_FrontRightVisual);
            UpdateWheelVisual(2, m_RearLeftWheel, m_RearLeftVisual);
            UpdateWheelVisual(3, m_RearRightWheel, m_RearRightVisual);
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
            return m_CommandCount > 0 && Time.realtimeSinceStartup - m_LastCommandRealtime <= Mathf.Max(0.05f, m_CommandTimeoutSeconds);
        }

        void ApplyWheelTargets(float linearX, float angularZ)
        {
            if (Mathf.Abs(linearX) < 0.02f && Mathf.Abs(angularZ) < 0.02f)
            {
                ApplyBrake();
                ApplyStopDamping();
                ResetPidState();
                m_MotorCommandCount++;
                return;
            }

            float radius = Mathf.Max(0.01f, m_WheelRadiusMeters);
            float halfTrack = Mathf.Max(0.01f, m_TrackMeters) * 0.5f;
            float motorLinearX = linearX * Mathf.Max(0f, m_WheelLinearMotorScale);
            float wheelAngularZ = angularZ * Mathf.Max(0f, m_WheelAngularMotorScale) * (m_WheelYawDirection >= 0f ? 1f : -1f);
            float leftRadPerSecond = (motorLinearX - wheelAngularZ * halfTrack) / radius;
            float rightRadPerSecond = (motorLinearX + wheelAngularZ * halfTrack) / radius;
            float motorDirection = m_WheelMotorDirection >= 0f ? 1f : -1f;
            float leftRpm = motorDirection * leftRadPerSecond * 60f / (2f * Mathf.PI);
            float rightRpm = motorDirection * rightRadPerSecond * 60f / (2f * Mathf.PI);
            Vector3 driveForward = PlanarForward();
            float bodyForwardSpeed = Vector3.Dot(Vector3.ProjectOnPlane(m_Body.velocity, Vector3.up), driveForward);
            bool overspeeding =
                linearX > 0.05f && bodyForwardSpeed > linearX + Mathf.Max(0.02f, m_LongitudinalOverspeedMargin) ||
                linearX < -0.05f && bodyForwardSpeed < linearX - Mathf.Max(0.02f, m_LongitudinalOverspeedMargin);

            ApplyWheelMotor(m_FrontLeftWheel, leftRpm, overspeeding);
            ApplyWheelMotor(m_RearLeftWheel, leftRpm, overspeeding);
            ApplyWheelMotor(m_FrontRightWheel, rightRpm, overspeeding);
            ApplyWheelMotor(m_RearRightWheel, rightRpm, overspeeding);
            ApplyLongitudinalVelocityPid(linearX, bodyForwardSpeed, overspeeding, driveForward);
            ApplyYawRatePid(angularZ);
            if (Mathf.Abs(linearX) > 0.03f && Mathf.Abs(angularZ) < 0.01f)
            {
                ApplyStraightHeadingHold();
            }
            else
            {
                ResetStraightHeadingHold();
            }
            ApplyLateralDamping();
            if (Mathf.Abs(linearX) < 0.03f && Mathf.Abs(angularZ) > 0.01f)
            {
                ApplyPureTurnTranslationDamping();
            }
            m_MotorCommandCount++;
        }

        void ApplyLongitudinalVelocityPid(float linearX, float bodyForwardSpeed, bool overspeeding, Vector3 driveForward)
        {
            if (overspeeding || Mathf.Abs(linearX) < 0.03f || m_MaxLongitudinalAssistAcceleration <= 0f)
            {
                m_LongitudinalSpeedIntegral = 0f;
                m_PreviousLongitudinalSpeedError = 0f;
                return;
            }

            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            float speedError = linearX - bodyForwardSpeed;
            m_LongitudinalSpeedIntegral = Mathf.Clamp(
                m_LongitudinalSpeedIntegral + speedError * dt,
                -Mathf.Abs(m_LongitudinalIntegralLimit),
                Mathf.Abs(m_LongitudinalIntegralLimit));
            float derivative = (speedError - m_PreviousLongitudinalSpeedError) / dt;
            m_PreviousLongitudinalSpeedError = speedError;
            float assistLimit = m_MaxLongitudinalAssistAcceleration * ChallengeTractionAssistScale();
            float assistAcceleration = Mathf.Clamp(
                speedError * Mathf.Max(0f, m_LongitudinalVelocityKp) +
                m_LongitudinalSpeedIntegral * Mathf.Max(0f, m_LongitudinalVelocityKi) +
                derivative * Mathf.Max(0f, m_LongitudinalVelocityKd),
                -assistLimit,
                assistLimit);
            m_Body.AddForce(driveForward * assistAcceleration, ForceMode.Acceleration);
        }

        float ChallengeTractionAssistScale()
        {
            float reduction =
                m_CurrentGrassContactFraction * Mathf.Clamp01(m_GrassTractionAssistReduction) +
                m_CurrentSandContactFraction * Mathf.Clamp01(m_SandTractionAssistReduction);
            return Mathf.Clamp(1f - reduction, 0.58f, 1f);
        }

        void ApplyYawRatePid(float angularZ)
        {
            if (m_MaxYawAssistAngularAcceleration <= 0f)
            {
                m_YawRateIntegral = 0f;
                m_PreviousYawRateError = 0f;
                m_YawServoRate = 0f;
                return;
            }

            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            float desiredUnityYawRate = -angularZ;
            float currentYawRate = Vector3.Dot(m_Body.angularVelocity, Vector3.up);
            if (Mathf.Abs(angularZ) < 0.01f && Mathf.Abs(currentYawRate) < 0.01f && Mathf.Abs(m_YawServoRate) < 0.01f)
            {
                m_YawRateIntegral = 0f;
                m_PreviousYawRateError = 0f;
                m_YawServoRate = 0f;
                return;
            }

            float yawRateError = desiredUnityYawRate - currentYawRate;
            m_YawRateIntegral = Mathf.Clamp(
                m_YawRateIntegral + yawRateError * dt,
                -Mathf.Abs(m_YawRateIntegralLimit),
                Mathf.Abs(m_YawRateIntegralLimit));
            float derivative = (yawRateError - m_PreviousYawRateError) / dt;
            m_PreviousYawRateError = yawRateError;
            float yawAcceleration = Mathf.Clamp(
                yawRateError * Mathf.Max(0f, m_YawRateKp) +
                m_YawRateIntegral * Mathf.Max(0f, m_YawRateKi) +
                derivative * Mathf.Max(0f, m_YawRateKd),
                -m_MaxYawAssistAngularAcceleration,
                m_MaxYawAssistAngularAcceleration);

            m_YawServoRate = Mathf.Clamp(
                Mathf.MoveTowards(m_YawServoRate, desiredUnityYawRate, Mathf.Abs(yawAcceleration) * dt),
                -m_MaxAngularSpeedRadPerSecond,
                m_MaxAngularSpeedRadPerSecond);
            Vector3 nonYawAngularVelocity = m_Body.angularVelocity - Vector3.up * currentYawRate;
            m_Body.angularVelocity = nonYawAngularVelocity + Vector3.up * m_YawServoRate;
            m_Body.MoveRotation(Quaternion.AngleAxis(m_YawServoRate * Mathf.Rad2Deg * dt, Vector3.up) * m_Body.rotation);
            m_Body.AddTorque(Vector3.up * yawAcceleration, ForceMode.Acceleration);
        }

        void ApplyStraightHeadingHold()
        {
            if (!m_EnableStraightHeadingHold || m_MaxStraightHeadingHoldAngularAcceleration <= 0f)
            {
                ResetStraightHeadingHold();
                return;
            }

            if (!m_StraightHeadingHoldActive)
            {
                m_StraightHeadingHoldYawDegrees = transform.eulerAngles.y;
                m_StraightHeadingHoldActive = true;
            }

            float yawErrorDegrees = Mathf.DeltaAngle(transform.eulerAngles.y, m_StraightHeadingHoldYawDegrees);
            float yawErrorRadians = yawErrorDegrees * Mathf.Deg2Rad;
            float currentYawRate = Vector3.Dot(m_Body.angularVelocity, Vector3.up);
            float yawAcceleration = Mathf.Clamp(
                yawErrorRadians * Mathf.Max(0f, m_StraightHeadingHoldKp) -
                currentYawRate * Mathf.Max(0f, m_StraightHeadingHoldKd),
                -m_MaxStraightHeadingHoldAngularAcceleration,
                m_MaxStraightHeadingHoldAngularAcceleration);
            m_Body.AddTorque(Vector3.up * yawAcceleration, ForceMode.Acceleration);
        }

        void ApplyLateralDamping()
        {
            if (m_MaxLateralDampingAcceleration <= 0f)
            {
                return;
            }

            Vector3 driveRight = PlanarRight();
            float lateralSpeed = Vector3.Dot(Vector3.ProjectOnPlane(m_Body.velocity, Vector3.up), driveRight);
            float materialScale = Mathf.Clamp(1f - m_CurrentSandContactFraction * Mathf.Clamp01(m_SandLateralDampingReduction), 0.65f, 1f);
            float lateralAcceleration = Mathf.Clamp(
                -lateralSpeed * Mathf.Max(0f, m_LateralDampingGain) * materialScale,
                -m_MaxLateralDampingAcceleration,
                m_MaxLateralDampingAcceleration);
            m_Body.AddForce(driveRight * lateralAcceleration, ForceMode.Acceleration);
        }

        void ApplyChallengeMaterialForces()
        {
            float resistanceAcceleration =
                m_CurrentGrassContactFraction * Mathf.Max(0f, m_GrassRollingResistanceAcceleration) +
                m_CurrentStoneContactFraction * Mathf.Max(0f, m_StoneRollingResistanceAcceleration) +
                m_CurrentSandContactFraction * Mathf.Max(0f, m_SandRollingResistanceAcceleration);
            if (resistanceAcceleration <= 0.001f)
            {
                return;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(m_Body.velocity, Vector3.up);
            if (planarVelocity.sqrMagnitude <= 0.0009f)
            {
                return;
            }

            m_Body.AddForce(-planarVelocity.normalized * resistanceAcceleration, ForceMode.Acceleration);
            m_ChallengeMaterialResistanceSteps++;
        }

        void ApplyPureTurnTranslationDamping()
        {
            if (m_MaxPureTurnDampingAcceleration <= 0f)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(m_Body.velocity);
            Vector3 localDamping = new Vector3(-localVelocity.x, 0f, -localVelocity.z) * Mathf.Max(0f, m_PureTurnTranslationDampingGain);
            localDamping = Vector3.ClampMagnitude(localDamping, m_MaxPureTurnDampingAcceleration);
            m_Body.AddForce(transform.TransformDirection(localDamping), ForceMode.Acceleration);
        }

        void ApplyStopDamping()
        {
            if (m_MaxStopDampingAcceleration > 0f)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(m_Body.velocity);
                Vector3 localDamping = new Vector3(-localVelocity.x, 0f, -localVelocity.z) * Mathf.Max(0f, m_StopVelocityDampingGain);
                localDamping = Vector3.ClampMagnitude(localDamping, m_MaxStopDampingAcceleration);
                m_Body.AddForce(transform.TransformDirection(localDamping), ForceMode.Acceleration);
            }

            if (m_StopYawDampingGain > 0f)
            {
                float currentYawRate = Vector3.Dot(m_Body.angularVelocity, Vector3.up);
                m_Body.AddTorque(Vector3.up * (-currentYawRate * m_StopYawDampingGain), ForceMode.Acceleration);
            }

            ApplyDirectStopDamping();
        }

        void ApplyDirectStopDamping()
        {
            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            if (m_DirectStopVelocityDampingGain > 0f)
            {
                Vector3 verticalVelocity = Vector3.Project(m_Body.velocity, Vector3.up);
                float velocityBlend = 1f - Mathf.Exp(-m_DirectStopVelocityDampingGain * dt);
                m_Body.velocity = Vector3.Lerp(m_Body.velocity, verticalVelocity, velocityBlend);
            }

            if (m_DirectStopYawDampingGain > 0f)
            {
                Vector3 yawAngularVelocity = Vector3.up * Vector3.Dot(m_Body.angularVelocity, Vector3.up);
                float yawBlend = 1f - Mathf.Exp(-m_DirectStopYawDampingGain * dt);
                m_Body.angularVelocity -= yawAngularVelocity * yawBlend;
            }
        }

        void ResetPidState()
        {
            m_LongitudinalSpeedIntegral = 0f;
            m_PreviousLongitudinalSpeedError = 0f;
            m_YawRateIntegral = 0f;
            m_PreviousYawRateError = 0f;
            m_YawServoRate = 0f;
            ResetStraightHeadingHold();
        }

        void ResetStraightHeadingHold()
        {
            m_StraightHeadingHoldActive = false;
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

            if (Mathf.Abs(targetRpm) < 0.5f)
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = 0f;
                return;
            }

            float torque = Mathf.Clamp((targetRpm - wheel.rpm) * m_RpmVelocityGain, -m_MaxMotorTorque, m_MaxMotorTorque);
            wheel.brakeTorque = 0f;
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

        void SampleWheelContacts()
        {
            m_MinBodyHeight = Mathf.Min(m_MinBodyHeight, transform.position.y);
            m_MaxBodyHeight = Mathf.Max(m_MaxBodyHeight, transform.position.y);

            var state = new ContactStepState();
            SampleWheelContact(m_FrontLeftWheel, ref state);
            SampleWheelContact(m_FrontRightWheel, ref state);
            SampleWheelContact(m_RearLeftWheel, ref state);
            SampleWheelContact(m_RearRightWheel, ref state);

            m_CurrentGrassContactFraction = Mathf.Clamp01(state.GrassWheelHits / 4f);
            m_CurrentStoneContactFraction = Mathf.Clamp01(state.StoneWheelHits / 4f);
            m_CurrentSandContactFraction = Mathf.Clamp01(state.SandWheelHits / 4f);

            if (!state.Any)
            {
                m_NoWheelContactSteps++;
            }
            if (state.Road)
            {
                m_RoadContactSteps++;
            }
            if (state.Bridge)
            {
                m_BridgeContactSteps++;
            }
            if (state.ShortRamp)
            {
                m_ShortRampContactSteps++;
            }
            if (state.ChallengeSurface)
            {
                m_ChallengeSurfaceContactSteps++;
            }
            if (state.ChallengeObstacle)
            {
                m_ChallengeObstacleContactSteps++;
            }
            if (state.ChallengePhysicsProxy)
            {
                m_ChallengePhysicsProxyContactSteps++;
            }
            if (state.Grass)
            {
                RecordMaterialStep(ref m_GrassContactSteps, ref m_GrassSpeedSum, ref m_MinGrassBodyHeight, ref m_MaxGrassBodyHeight);
            }
            if (state.Stone)
            {
                RecordMaterialStep(ref m_StoneContactSteps, ref m_StoneSpeedSum, ref m_MinStoneBodyHeight, ref m_MaxStoneBodyHeight);
            }
            if (state.Sand)
            {
                RecordMaterialStep(ref m_SandContactSteps, ref m_SandSpeedSum, ref m_MinSandBodyHeight, ref m_MaxSandBodyHeight);
            }
            if (state.GrassSurface)
            {
                m_GrassSurfaceContactSteps++;
            }
            if (state.StoneSurface)
            {
                m_StoneSurfaceContactSteps++;
            }
            if (state.SandSurface)
            {
                m_SandSurfaceContactSteps++;
            }
            if (state.GrassPhysicsProxy)
            {
                m_GrassPhysicsProxyContactSteps++;
            }
            if (state.StonePhysicsProxy)
            {
                m_StonePhysicsProxyContactSteps++;
            }
            if (state.SandPhysicsProxy)
            {
                m_SandPhysicsProxyContactSteps++;
            }

            m_GrassWheelHitCount += state.GrassWheelHits;
            m_StoneWheelHitCount += state.StoneWheelHits;
            m_SandWheelHitCount += state.SandWheelHits;

            if (state.Terrain)
            {
                m_TerrainContactSteps++;
            }
            if (state.Other)
            {
                m_OtherContactSteps++;
            }
        }

        void SampleWheelContact(WheelCollider wheel, ref ContactStepState state)
        {
            if (wheel == null || !wheel.GetGroundHit(out WheelHit hit))
            {
                return;
            }

            state.Any = true;
            m_MinWheelGroundHeight = Mathf.Min(m_MinWheelGroundHeight, hit.point.y);
            m_MaxWheelGroundHeight = Mathf.Max(m_MaxWheelGroundHeight, hit.point.y);

            string name = hit.collider != null ? hit.collider.gameObject.name : string.Empty;
            if (name.StartsWith("ScoutWheelGround_PhysicalBridge", StringComparison.Ordinal))
            {
                state.Bridge = true;
            }
            else if (name.StartsWith("ScoutWheelGround_PhysicalShortRamp", StringComparison.Ordinal))
            {
                state.ShortRamp = true;
            }
            else if (ClassifyChallengeContact(name, hit.point.y, ref state))
            {
            }
            else if (name.StartsWith("ScoutWheelGround_PhysicalRoad", StringComparison.Ordinal))
            {
                state.Road = true;
            }
            else if (name.StartsWith("OffroadTerrain_", StringComparison.Ordinal))
            {
                state.Terrain = true;
            }
            else
            {
                state.Other = true;
            }
        }

        bool ClassifyChallengeContact(string name, float hitY, ref ContactStepState state)
        {
            if (name.StartsWith("ScoutWheelGround_ChallengeSurface_Grass", StringComparison.Ordinal))
            {
                state.ChallengeSurface = true;
                state.GrassSurface = true;
                MarkGrassContact(hitY, ref state);
                return true;
            }
            if (name.StartsWith("ScoutWheelGround_ChallengeSurface_Stone", StringComparison.Ordinal))
            {
                state.ChallengeSurface = true;
                state.StoneSurface = true;
                MarkStoneContact(hitY, ref state);
                return true;
            }
            if (name.StartsWith("ScoutWheelGround_ChallengeSurface_Sand", StringComparison.Ordinal))
            {
                state.ChallengeSurface = true;
                state.SandSurface = true;
                MarkSandContact(hitY, ref state);
                return true;
            }
            if (name.StartsWith("ScoutWheelGround_ChallengePhysicsProxy_Grass", StringComparison.Ordinal) ||
                name.StartsWith("ScoutWheelGround_ChallengeObstacle_Grass", StringComparison.Ordinal))
            {
                state.ChallengeObstacle = true;
                state.ChallengePhysicsProxy = name.StartsWith("ScoutWheelGround_ChallengePhysicsProxy_", StringComparison.Ordinal) || state.ChallengePhysicsProxy;
                state.GrassPhysicsProxy = true;
                MarkGrassContact(hitY, ref state);
                return true;
            }
            if (name.StartsWith("ScoutWheelGround_ChallengePhysicsProxy_Stone", StringComparison.Ordinal) ||
                name.StartsWith("ScoutWheelGround_ChallengeObstacle_Stone", StringComparison.Ordinal))
            {
                state.ChallengeObstacle = true;
                state.ChallengePhysicsProxy = name.StartsWith("ScoutWheelGround_ChallengePhysicsProxy_", StringComparison.Ordinal) || state.ChallengePhysicsProxy;
                state.StonePhysicsProxy = true;
                MarkStoneContact(hitY, ref state);
                return true;
            }
            if (name.StartsWith("ScoutWheelGround_ChallengePhysicsProxy_Sand", StringComparison.Ordinal) ||
                name.StartsWith("ScoutWheelGround_ChallengeObstacle_Sand", StringComparison.Ordinal))
            {
                state.ChallengeObstacle = true;
                state.ChallengePhysicsProxy = name.StartsWith("ScoutWheelGround_ChallengePhysicsProxy_", StringComparison.Ordinal) || state.ChallengePhysicsProxy;
                state.SandPhysicsProxy = true;
                MarkSandContact(hitY, ref state);
                return true;
            }
            if (name.StartsWith("ScoutWheelGround_ChallengeObstacle_", StringComparison.Ordinal))
            {
                state.ChallengeObstacle = true;
                return true;
            }

            return false;
        }

        void MarkGrassContact(float hitY, ref ContactStepState state)
        {
            state.Grass = true;
            state.GrassWheelHits++;
            m_MinGrassWheelGroundHeight = Mathf.Min(m_MinGrassWheelGroundHeight, hitY);
            m_MaxGrassWheelGroundHeight = Mathf.Max(m_MaxGrassWheelGroundHeight, hitY);
        }

        void MarkStoneContact(float hitY, ref ContactStepState state)
        {
            state.Stone = true;
            state.StoneWheelHits++;
            m_MinStoneWheelGroundHeight = Mathf.Min(m_MinStoneWheelGroundHeight, hitY);
            m_MaxStoneWheelGroundHeight = Mathf.Max(m_MaxStoneWheelGroundHeight, hitY);
        }

        void MarkSandContact(float hitY, ref ContactStepState state)
        {
            state.Sand = true;
            state.SandWheelHits++;
            m_MinSandWheelGroundHeight = Mathf.Min(m_MinSandWheelGroundHeight, hitY);
            m_MaxSandWheelGroundHeight = Mathf.Max(m_MaxSandWheelGroundHeight, hitY);
        }

        void RecordMaterialStep(ref int stepCount, ref float speedSum, ref float minBodyHeight, ref float maxBodyHeight)
        {
            stepCount++;
            speedSum += Vector3.ProjectOnPlane(m_Body.velocity, Vector3.up).magnitude;
            minBodyHeight = Mathf.Min(minBodyHeight, transform.position.y);
            maxBodyHeight = Mathf.Max(maxBodyHeight, transform.position.y);
        }

        void InitializeWheelVisualState()
        {
            m_VisualWheelTransforms = new[] { m_FrontLeftVisual, m_FrontRightVisual, m_RearLeftVisual, m_RearRightVisual };
            m_VisualRestRootRotations = new Quaternion[m_VisualWheelTransforms.Length];
            m_VisualRollDegrees = new float[m_VisualWheelTransforms.Length];
            m_VisualAngularSpeeds = new float[m_VisualWheelTransforms.Length];

            for (int i = 0; i < m_VisualWheelTransforms.Length; i++)
            {
                Transform visual = m_VisualWheelTransforms[i];
                m_VisualRestRootRotations[i] = visual != null ? Quaternion.Inverse(transform.rotation) * visual.rotation : Quaternion.identity;
            }
        }

        void UpdateWheelVisual(int index, WheelCollider wheel, Transform visual)
        {
            if (wheel == null || visual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            position += transform.up * m_WheelVisualVerticalOffset;
            visual.position = position;

            if (m_VisualRestRootRotations == null || index < 0 || index >= m_VisualRestRootRotations.Length)
            {
                visual.rotation = rotation;
                return;
            }

            float targetAngularSpeed = EstimateVisualWheelAngularSpeed(wheel);
            float previousAngularSpeed = m_VisualAngularSpeeds[index];
            float smoothing = 1f - Mathf.Exp(-Mathf.Max(0.1f, m_WheelVisualAngularSmoothing) * Time.deltaTime);
            float angularSpeed = Mathf.Lerp(previousAngularSpeed, targetAngularSpeed, smoothing);
            if (Mathf.Abs(previousAngularSpeed) > 20f && Mathf.Abs(angularSpeed) > 20f && Mathf.Sign(previousAngularSpeed) != Mathf.Sign(angularSpeed))
            {
                m_WheelVisualDirectionReversalCount++;
            }

            float deltaDegrees = angularSpeed * Time.deltaTime;
            m_VisualAngularSpeeds[index] = angularSpeed;
            m_VisualRollDegrees[index] = Mathf.Repeat(m_VisualRollDegrees[index] + deltaDegrees, 360f);
            m_WheelVisualTotalAbsRollDegrees += Mathf.Abs(deltaDegrees);

            Quaternion rootRelativeRotation = Quaternion.AngleAxis(m_VisualRollDegrees[index], Vector3.right) * m_VisualRestRootRotations[index];
            visual.rotation = transform.rotation * rootRelativeRotation;
        }

        float EstimateVisualWheelAngularSpeed(WheelCollider wheel)
        {
            float commandedWheelSpeed = 0f;
            if (HasRecentCommand())
            {
                float wheelAngularZ = m_CommandedAngularZ * (m_WheelYawDirection >= 0f ? 1f : -1f);
                commandedWheelSpeed = m_CommandedLinearX + wheelAngularZ * wheel.transform.localPosition.x;
            }

            float observedWheelSpeed = Vector3.Dot(Vector3.ProjectOnPlane(m_Body.velocity, Vector3.up), PlanarForward()) -
                                       Vector3.Dot(m_Body.angularVelocity, Vector3.up) * wheel.transform.localPosition.x;
            float signSource = Mathf.Abs(commandedWheelSpeed) > 0.03f ? commandedWheelSpeed : observedWheelSpeed;
            float direction = Mathf.Abs(signSource) > 0.02f ? Mathf.Sign(signSource) : Mathf.Sign(wheel.rpm);
            if (Mathf.Abs(direction) < 0.5f)
            {
                direction = 0f;
            }

            float rpmAngularSpeed = Mathf.Abs(wheel.rpm) * 6f;
            float bodyAngularSpeed = Mathf.Abs(observedWheelSpeed) / Mathf.Max(0.01f, m_WheelRadiusMeters) * Mathf.Rad2Deg;
            float targetMagnitude = Mathf.Max(rpmAngularSpeed, bodyAngularSpeed);
            return direction * targetMagnitude * (m_WheelVisualForwardRollDirection >= 0f ? 1f : -1f);
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
                $"road_contact_steps={m_RoadContactSteps}\n" +
                $"bridge_contact_steps={m_BridgeContactSteps}\n" +
                $"short_ramp_contact_steps={m_ShortRampContactSteps}\n" +
                $"challenge_surface_contact_steps={m_ChallengeSurfaceContactSteps}\n" +
                $"challenge_obstacle_contact_steps={m_ChallengeObstacleContactSteps}\n" +
                $"challenge_physics_proxy_contact_steps={m_ChallengePhysicsProxyContactSteps}\n" +
                $"challenge_material_resistance_steps={m_ChallengeMaterialResistanceSteps}\n" +
                $"grass_contact_steps={m_GrassContactSteps}\n" +
                $"stone_contact_steps={m_StoneContactSteps}\n" +
                $"sand_contact_steps={m_SandContactSteps}\n" +
                $"grass_surface_contact_steps={m_GrassSurfaceContactSteps}\n" +
                $"stone_surface_contact_steps={m_StoneSurfaceContactSteps}\n" +
                $"sand_surface_contact_steps={m_SandSurfaceContactSteps}\n" +
                $"grass_physics_proxy_contact_steps={m_GrassPhysicsProxyContactSteps}\n" +
                $"stone_physics_proxy_contact_steps={m_StonePhysicsProxyContactSteps}\n" +
                $"sand_physics_proxy_contact_steps={m_SandPhysicsProxyContactSteps}\n" +
                $"grass_wheel_hit_count={m_GrassWheelHitCount}\n" +
                $"stone_wheel_hit_count={m_StoneWheelHitCount}\n" +
                $"sand_wheel_hit_count={m_SandWheelHitCount}\n" +
                $"grass_avg_speed_mps={AverageSpeed(m_GrassSpeedSum, m_GrassContactSteps):F3}\n" +
                $"stone_avg_speed_mps={AverageSpeed(m_StoneSpeedSum, m_StoneContactSteps):F3}\n" +
                $"sand_avg_speed_mps={AverageSpeed(m_SandSpeedSum, m_SandContactSteps):F3}\n" +
                $"grass_wheel_ground_height_span_m={SafeSpan(m_MinGrassWheelGroundHeight, m_MaxGrassWheelGroundHeight):F3}\n" +
                $"stone_wheel_ground_height_span_m={SafeSpan(m_MinStoneWheelGroundHeight, m_MaxStoneWheelGroundHeight):F3}\n" +
                $"sand_wheel_ground_height_span_m={SafeSpan(m_MinSandWheelGroundHeight, m_MaxSandWheelGroundHeight):F3}\n" +
                $"grass_body_height_span_m={SafeSpan(m_MinGrassBodyHeight, m_MaxGrassBodyHeight):F3}\n" +
                $"stone_body_height_span_m={SafeSpan(m_MinStoneBodyHeight, m_MaxStoneBodyHeight):F3}\n" +
                $"sand_body_height_span_m={SafeSpan(m_MinSandBodyHeight, m_MaxSandBodyHeight):F3}\n" +
                $"terrain_contact_steps={m_TerrainContactSteps}\n" +
                $"other_contact_steps={m_OtherContactSteps}\n" +
                $"no_wheel_contact_steps={m_NoWheelContactSteps}\n" +
                $"body_height_span_m={SafeSpan(m_MinBodyHeight, m_MaxBodyHeight):F3}\n" +
                $"wheel_ground_height_span_m={SafeSpan(m_MinWheelGroundHeight, m_MaxWheelGroundHeight):F3}\n" +
                $"wheel_visual_total_abs_roll_deg={m_WheelVisualTotalAbsRollDegrees:F1}\n" +
                $"wheel_visual_direction_reversal_count={m_WheelVisualDirectionReversalCount}\n" +
                $"straight_heading_hold_active={(m_StraightHeadingHoldActive ? 1 : 0)}\n" +
                $"front_left_rpm={WheelRpm(m_FrontLeftWheel):F3}\n" +
                $"front_right_rpm={WheelRpm(m_FrontRightWheel):F3}\n" +
                $"rear_left_rpm={WheelRpm(m_RearLeftWheel):F3}\n" +
                $"rear_right_rpm={WheelRpm(m_RearRightWheel):F3}\n");
        }

        static float WheelRpm(WheelCollider wheel)
        {
            return wheel != null ? wheel.rpm : 0f;
        }

        static float SafeSpan(float minValue, float maxValue)
        {
            if (float.IsInfinity(minValue) || float.IsInfinity(maxValue))
            {
                return 0f;
            }

            return Mathf.Max(0f, maxValue - minValue);
        }

        static float AverageSpeed(float speedSum, int stepCount)
        {
            return stepCount > 0 ? speedSum / stepCount : 0f;
        }

        Vector3 PlanarForward()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        Vector3 PlanarRight()
        {
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            return right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
        }
    }
}
