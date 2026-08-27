using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace VLN.ROS2
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VlnMesaTopgearCliffDropProbe : MonoBehaviour
    {
        [SerializeField] Vector3 m_TargetPoint;
        [SerializeField] float m_ExpectedDropMeters;
        [SerializeField] float m_MaxEdgeSlopeDegrees;
        [SerializeField] float m_SampleDistanceMeters;

        string m_ResultPath;
        Rigidbody m_Body;
        WheelCollider[] m_Wheels = Array.Empty<WheelCollider>();
        Vector3 m_StartPosition;
        float m_MinBodyY = float.PositiveInfinity;
        float m_MaxBodyY = float.NegativeInfinity;
        float m_MaxRollAbsDegrees;
        float m_MaxPitchAbsDegrees;
        float m_MaxAngularSpeed;
        int m_TotalSteps;
        int m_AnyWheelContactSteps;
        int m_NoWheelContactSteps;
        int m_SteepWheelContactSteps;
        int m_SlowSteepContactSteps;
        int m_CollisionEnterCount;
        int m_CollisionStayCount;
        readonly HashSet<string> m_CollisionNames = new();
        bool m_FinalWritten;

        public void Configure(Vector3 targetPoint, float expectedDropMeters, float maxEdgeSlopeDegrees, float sampleDistanceMeters)
        {
            m_TargetPoint = targetPoint;
            m_ExpectedDropMeters = expectedDropMeters;
            m_MaxEdgeSlopeDegrees = maxEdgeSlopeDegrees;
            m_SampleDistanceMeters = sampleDistanceMeters;
        }

        void Start()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Wheels = GetComponentsInChildren<WheelCollider>(true);
            m_StartPosition = transform.position;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_mesa_topgear_vehicle_cliff_drop_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                "started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "stage=mesa_topgear_vehicle_cliff_drop_probe\n" +
                "motion_source=external_ros2_cmd_vel\n" +
                "collision_policy=real_terrain_and_scene_colliders_no_hidden_floor\n" +
                "start_position=" + FormatVector(m_StartPosition) + "\n" +
                "target_point=" + FormatVector(m_TargetPoint) + "\n" +
                "expected_drop_m=" + m_ExpectedDropMeters.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "edge_slope_deg=" + m_MaxEdgeSlopeDegrees.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "sample_distance_m=" + m_SampleDistanceMeters.ToString("F3", CultureInfo.InvariantCulture) + "\n");
            Debug.Log("VLN_MESA_TOPGEAR_CLIFF_DROP_PROBE_READY expected_drop=" + m_ExpectedDropMeters.ToString("F2", CultureInfo.InvariantCulture));
        }

        void FixedUpdate()
        {
            m_TotalSteps++;
            m_MinBodyY = Mathf.Min(m_MinBodyY, transform.position.y);
            m_MaxBodyY = Mathf.Max(m_MaxBodyY, transform.position.y);
            if (m_Body != null)
            {
                m_MaxAngularSpeed = Mathf.Max(m_MaxAngularSpeed, m_Body.angularVelocity.magnitude);
            }

            Vector3 euler = transform.eulerAngles;
            m_MaxRollAbsDegrees = Mathf.Max(m_MaxRollAbsDegrees, Mathf.Abs(Mathf.DeltaAngle(0f, euler.z)));
            m_MaxPitchAbsDegrees = Mathf.Max(m_MaxPitchAbsDegrees, Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)));

            bool anyWheelContact = false;
            bool steepContact = false;
            foreach (var wheel in m_Wheels)
            {
                if (wheel == null || !wheel.GetGroundHit(out var hit))
                {
                    continue;
                }

                anyWheelContact = true;
                if (Vector3.Angle(hit.normal, Vector3.up) >= 28f)
                {
                    steepContact = true;
                }
            }

            if (anyWheelContact)
            {
                m_AnyWheelContactSteps++;
            }
            else
            {
                m_NoWheelContactSteps++;
            }

            if (steepContact)
            {
                m_SteepWheelContactSteps++;
                if (m_Body != null && m_Body.velocity.magnitude < 0.18f)
                {
                    m_SlowSteepContactSteps++;
                }
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            RecordCollision(collision, true);
        }

        void OnCollisionStay(Collision collision)
        {
            RecordCollision(collision, false);
        }

        void OnApplicationQuit()
        {
            WriteFinalSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalSnapshot();
        }

        void RecordCollision(Collision collision, bool enter)
        {
            if (collision == null || collision.collider == null || collision.collider.isTrigger || collision.collider.transform.IsChildOf(transform))
            {
                return;
            }

            if (enter)
            {
                m_CollisionEnterCount++;
            }
            else
            {
                m_CollisionStayCount++;
            }
            m_CollisionNames.Add(HierarchyPath(collision.collider.gameObject));
        }

        void WriteFinalSnapshot()
        {
            if (m_FinalWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }
            m_FinalWritten = true;

            float heightDrop = m_StartPosition.y - m_MinBodyY;
            float horizontalDelta = Vector2.Distance(new Vector2(m_StartPosition.x, m_StartPosition.z), new Vector2(transform.position.x, transform.position.z));
            float distanceToTarget = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(m_TargetPoint.x, m_TargetPoint.z));
            float bodyHeightSpan = SafeSpan(m_MinBodyY, m_MaxBodyY);
            int stickyLimit = Mathf.Max(25, m_TotalSteps / 5);
            bool movedDown = heightDrop >= Mathf.Min(3.0f, Mathf.Max(1.6f, m_ExpectedDropMeters * 0.35f));
            bool movedForward = horizontalDelta >= Mathf.Min(5.0f, Mathf.Max(1.6f, m_SampleDistanceMeters * 0.28f));
            bool hadBodyMotion = Mathf.Max(m_MaxRollAbsDegrees, m_MaxPitchAbsDegrees) >= 6f || m_MaxAngularSpeed >= 0.35f;
            bool notStuckOnWall = m_SlowSteepContactSteps <= stickyLimit;
            bool pass = movedDown && movedForward && hadBodyMotion && notStuckOnWall && transform.position.y > -80f;

            File.AppendAllText(m_ResultPath,
                "finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "physics_step_count=" + m_TotalSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "any_wheel_contact_steps=" + m_AnyWheelContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "no_wheel_contact_steps=" + m_NoWheelContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "steep_wheel_contact_steps=" + m_SteepWheelContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "slow_steep_contact_steps=" + m_SlowSteepContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "collision_enter_count=" + m_CollisionEnterCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "collision_stay_count=" + m_CollisionStayCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "unique_collision_count=" + m_CollisionNames.Count.ToString(CultureInfo.InvariantCulture) + "\n" +
                "collision_names=" + string.Join(" | ", m_CollisionNames) + "\n" +
                "height_drop_m=" + heightDrop.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "horizontal_delta_m=" + horizontalDelta.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "distance_to_target_m=" + distanceToTarget.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "body_height_span_m=" + bodyHeightSpan.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_roll_abs_deg=" + m_MaxRollAbsDegrees.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_pitch_abs_deg=" + m_MaxPitchAbsDegrees.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_angular_speed_radps=" + m_MaxAngularSpeed.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "final_position=" + FormatVector(transform.position) + "\n" +
                "success=" + (pass ? 1 : 0).ToString(CultureInfo.InvariantCulture) + "\n");
        }

        static float SafeSpan(float minValue, float maxValue)
        {
            if (float.IsInfinity(minValue) || float.IsInfinity(maxValue))
            {
                return 0f;
            }
            return Mathf.Max(0f, maxValue - minValue);
        }

        static string HierarchyPath(GameObject go)
        {
            var names = new List<string>();
            Transform current = go.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F3", CultureInfo.InvariantCulture) + "," + value.y.ToString("F3", CultureInfo.InvariantCulture) + "," + value.z.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
