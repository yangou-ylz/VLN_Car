using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace VLN.ROS2
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VlnMesaTopgearObstacleImpactProbe : MonoBehaviour
    {
        [SerializeField] Vector3 m_TargetPoint;
        [SerializeField] string m_TargetName = "missing";
        [SerializeField] float m_InitialDistanceMeters;
        string m_ResultPath;
        int m_CollisionEnterCount;
        int m_CollisionStayCount;
        int m_WheelObstacleContactSteps;
        float m_MaxRelativeVelocity;
        float m_MinDistanceToTarget = float.PositiveInfinity;
        readonly HashSet<string> m_ObstacleNames = new();
        Rigidbody m_Body;
        WheelCollider[] m_Wheels = Array.Empty<WheelCollider>();
        bool m_FinalWritten;

        public void Configure(Collider targetCollider, Vector3 targetPoint, string targetName, float initialDistanceMeters)
        {
            m_TargetPoint = targetPoint;
            m_TargetName = string.IsNullOrWhiteSpace(targetName) ? "missing" : targetName;
            m_InitialDistanceMeters = initialDistanceMeters;
        }

        void Start()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Wheels = GetComponentsInChildren<WheelCollider>(true);
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_mesa_topgear_vehicle_obstacle_impact_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                "started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "stage=mesa_topgear_vehicle_obstacle_impact_probe\n" +
                "target_name=" + m_TargetName + "\n" +
                "target_point=" + FormatVector(m_TargetPoint) + "\n" +
                "initial_distance_m=" + m_InitialDistanceMeters.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "motion_source=external_ros2_cmd_vel\n" +
                "collision_policy=real_scene_collider_no_fake_probe_wall\n");
            Debug.Log("VLN_MESA_TOPGEAR_OBSTACLE_IMPACT_PROBE_READY target=" + m_TargetName + " distance=" + m_InitialDistanceMeters.ToString("F2", CultureInfo.InvariantCulture));
        }

        void Update()
        {
            float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(m_TargetPoint.x, 0f, m_TargetPoint.z));
            m_MinDistanceToTarget = Mathf.Min(m_MinDistanceToTarget, distance);
        }

        void FixedUpdate()
        {
            bool obstacleWheelHit = false;
            foreach (var wheel in m_Wheels)
            {
                if (wheel == null || !wheel.GetGroundHit(out var hit) || hit.collider == null)
                {
                    continue;
                }
                if (hit.collider is TerrainCollider || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                obstacleWheelHit = true;
                m_ObstacleNames.Add(HierarchyPath(hit.collider.gameObject));
            }
            if (obstacleWheelHit)
            {
                m_WheelObstacleContactSteps++;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            RecordCollision(collision, enter: true);
        }

        void OnCollisionStay(Collision collision)
        {
            RecordCollision(collision, enter: false);
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
            if (collision == null || collision.collider == null || collision.collider is TerrainCollider || collision.collider.isTrigger)
            {
                return;
            }
            if (collision.collider.transform.IsChildOf(transform))
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
            m_MaxRelativeVelocity = Mathf.Max(m_MaxRelativeVelocity, collision.relativeVelocity.magnitude);
            m_ObstacleNames.Add(HierarchyPath(collision.collider.gameObject));
        }

        void WriteFinalSnapshot()
        {
            if (m_FinalWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }
            m_FinalWritten = true;
            int totalCollisions = m_CollisionEnterCount + m_CollisionStayCount + m_WheelObstacleContactSteps;
            float finalDistance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(m_TargetPoint.x, 0f, m_TargetPoint.z));
            bool pass = totalCollisions > 0 && m_ObstacleNames.Count > 0 && m_MinDistanceToTarget < m_InitialDistanceMeters - 0.35f;
            File.AppendAllText(m_ResultPath,
                "finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "collision_enter_count=" + m_CollisionEnterCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "collision_stay_count=" + m_CollisionStayCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "wheel_obstacle_contact_steps=" + m_WheelObstacleContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "unique_obstacle_collision_count=" + m_ObstacleNames.Count.ToString(CultureInfo.InvariantCulture) + "\n" +
                "obstacle_names=" + string.Join(" | ", m_ObstacleNames) + "\n" +
                "max_relative_velocity_mps=" + m_MaxRelativeVelocity.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "min_distance_to_target_m=" + (float.IsInfinity(m_MinDistanceToTarget) ? "missing" : m_MinDistanceToTarget.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                "final_distance_to_target_m=" + finalDistance.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "final_speed_mps=" + (m_Body != null ? m_Body.velocity.magnitude.ToString("F3", CultureInfo.InvariantCulture) : "missing") + "\n" +
                "success=" + (pass ? 1 : 0).ToString(CultureInfo.InvariantCulture) + "\n");
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
