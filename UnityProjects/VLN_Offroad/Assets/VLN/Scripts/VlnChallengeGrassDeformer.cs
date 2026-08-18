using System;
using System.IO;
using UnityEngine;

namespace VLN.ROS2
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class VlnChallengeGrassDeformer : MonoBehaviour
    {
        [SerializeField] string m_PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        [SerializeField] float m_WheelBendRadius = 0.56f;
        [SerializeField] float m_TireTrackHalfWidth = 0.25f;
        [SerializeField] float m_MaxSidePushMeters = 0.26f;
        [SerializeField] float m_MaxFlattenMeters = 0.32f;
        [SerializeField] float m_BendRisePerSecond = 7.0f;
        [SerializeField] float m_BendRecoveryPerSecond = 0.012f;
        [SerializeField] float m_DeformedThreshold = 0.10f;

        MeshFilter m_MeshFilter;
        Mesh m_RuntimeMesh;
        Vector3[] m_OriginalVertices;
        Vector3[] m_WorkingVertices;
        float[] m_BendAmounts;
        Vector3[] m_BendDirections;
        WheelCollider[] m_Wheels = Array.Empty<WheelCollider>();
        Transform m_PhysicsRoot;
        string m_ResultPath;
        bool m_FinalSnapshotWritten;

        public int BladeCount => m_BendAmounts != null ? m_BendAmounts.Length : 0;
        public int CurrentDeformedBladeCount { get; private set; }
        public int MaxDeformedBladeCount { get; private set; }
        public int CurrentFreshAffectedBladeCount { get; private set; }
        public int MaxFreshAffectedBladeCount { get; private set; }
        public float MaxDeformedFraction => BladeCount > 0 ? MaxDeformedBladeCount / (float)BladeCount : 0f;

        public void Configure(string physicsRootName, float wheelBendRadius, float tireTrackHalfWidth, float sidePushMeters, float flattenMeters, float recoveryPerSecond)
        {
            m_PhysicsRootName = physicsRootName;
            m_WheelBendRadius = wheelBendRadius;
            m_TireTrackHalfWidth = tireTrackHalfWidth;
            m_MaxSidePushMeters = sidePushMeters;
            m_MaxFlattenMeters = flattenMeters;
            m_BendRecoveryPerSecond = recoveryPerSecond;
        }

        void Awake()
        {
            m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter == null || m_MeshFilter.sharedMesh == null)
            {
                return;
            }

            m_RuntimeMesh = Instantiate(m_MeshFilter.sharedMesh);
            m_RuntimeMesh.name = m_MeshFilter.sharedMesh.name + "_RuntimeBent";
            m_MeshFilter.sharedMesh = m_RuntimeMesh;
            m_OriginalVertices = m_RuntimeMesh.vertices;
            m_WorkingVertices = new Vector3[m_OriginalVertices.Length];
            Array.Copy(m_OriginalVertices, m_WorkingVertices, m_OriginalVertices.Length);

            int bladeCount = m_OriginalVertices.Length / 6;
            m_BendAmounts = new float[bladeCount];
            m_BendDirections = new Vector3[bladeCount];
            for (int i = 0; i < m_BendDirections.Length; i++)
            {
                m_BendDirections[i] = Vector3.forward;
            }
        }

        void Start()
        {
            ResolveVehicleReferences();
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_challenge_grass_deformer_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.AppendAllText(
                m_ResultPath,
                $"started={DateTime.UtcNow:O};field={gameObject.name};blade_count={BladeCount};wheel_bend_radius_m={m_WheelBendRadius:F3};side_push_m={m_MaxSidePushMeters:F3};flatten_m={m_MaxFlattenMeters:F3};recovery_per_second={m_BendRecoveryPerSecond:F3}\n");
        }

        void LateUpdate()
        {
            if (m_RuntimeMesh == null || m_OriginalVertices == null || m_BendAmounts == null)
            {
                return;
            }

            if (m_PhysicsRoot == null || m_Wheels == null || m_Wheels.Length == 0)
            {
                ResolveVehicleReferences();
            }

            float dt = Mathf.Max(0.001f, Time.deltaTime);
            CurrentDeformedBladeCount = 0;
            CurrentFreshAffectedBladeCount = 0;
            Array.Copy(m_OriginalVertices, m_WorkingVertices, m_OriginalVertices.Length);

            for (int bladeIndex = 0; bladeIndex < m_BendAmounts.Length; bladeIndex++)
            {
                int v = bladeIndex * 6;
                Vector3 baseLocal = (m_OriginalVertices[v] + m_OriginalVertices[v + 1]) * 0.5f;
                Vector3 baseWorld = transform.TransformPoint(baseLocal);
                float targetBend = FindWheelInfluence(baseWorld, out Vector3 pushDirectionWorld);

                if (targetBend > 0.001f)
                {
                    CurrentFreshAffectedBladeCount++;
                    m_BendDirections[bladeIndex] = transform.InverseTransformDirection(pushDirectionWorld).normalized;
                    m_BendAmounts[bladeIndex] = Mathf.MoveTowards(m_BendAmounts[bladeIndex], Mathf.Max(m_BendAmounts[bladeIndex], targetBend), m_BendRisePerSecond * dt);
                }
                else
                {
                    m_BendAmounts[bladeIndex] = Mathf.MoveTowards(m_BendAmounts[bladeIndex], 0f, m_BendRecoveryPerSecond * dt);
                }

                float bend = Mathf.Clamp01(m_BendAmounts[bladeIndex]);
                if (bend > m_DeformedThreshold)
                {
                    CurrentDeformedBladeCount++;
                }

                ApplyBladeBend(v, bend, m_BendDirections[bladeIndex]);
            }

            MaxDeformedBladeCount = Mathf.Max(MaxDeformedBladeCount, CurrentDeformedBladeCount);
            MaxFreshAffectedBladeCount = Mathf.Max(MaxFreshAffectedBladeCount, CurrentFreshAffectedBladeCount);
            m_RuntimeMesh.vertices = m_WorkingVertices;
            m_RuntimeMesh.RecalculateNormals();
            m_RuntimeMesh.RecalculateBounds();
        }

        float FindWheelInfluence(Vector3 bladeWorld, out Vector3 pushDirectionWorld)
        {
            pushDirectionWorld = Vector3.forward;
            if (m_Wheels == null || m_Wheels.Length == 0)
            {
                return 0f;
            }

            float bestInfluence = 0f;
            float radius = Mathf.Max(0.05f, m_WheelBendRadius);
            foreach (var wheel in m_Wheels)
            {
                if (wheel == null)
                {
                    continue;
                }

                wheel.GetWorldPose(out Vector3 wheelWorld, out _);
                Vector3 delta = Vector3.ProjectOnPlane(bladeWorld - wheelWorld, Vector3.up);
                float distance = delta.magnitude;
                if (distance > radius)
                {
                    continue;
                }

                float radialInfluence = 1f - distance / radius;
                float trackInfluence = Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(delta, wheel.transform.right)) / Mathf.Max(0.02f, m_TireTrackHalfWidth));
                float influence = Mathf.Clamp01(radialInfluence * 0.45f + trackInfluence * radialInfluence * 0.75f);
                if (influence <= bestInfluence)
                {
                    continue;
                }

                bestInfluence = influence;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    pushDirectionWorld = delta.normalized;
                }
                else
                {
                    pushDirectionWorld = Vector3.ProjectOnPlane(wheel.transform.right, Vector3.up).normalized;
                }
            }

            return bestInfluence;
        }

        void ApplyBladeBend(int vertexStart, float bend, Vector3 pushDirectionLocal)
        {
            if (bend <= 0.001f)
            {
                return;
            }

            Vector3 sidePush = pushDirectionLocal * (m_MaxSidePushMeters * bend);
            Vector3 flatten = Vector3.down * (m_MaxFlattenMeters * bend);
            Vector3 tipOffset = sidePush + flatten;
            Vector3 shoulderOffset = sidePush * 0.28f + flatten * 0.20f;

            m_WorkingVertices[vertexStart + 0] = m_OriginalVertices[vertexStart + 0] + shoulderOffset;
            m_WorkingVertices[vertexStart + 1] = m_OriginalVertices[vertexStart + 1] + shoulderOffset;
            m_WorkingVertices[vertexStart + 2] = m_OriginalVertices[vertexStart + 2] + tipOffset;
            m_WorkingVertices[vertexStart + 3] = m_OriginalVertices[vertexStart + 3] + shoulderOffset;
            m_WorkingVertices[vertexStart + 4] = m_OriginalVertices[vertexStart + 4] + shoulderOffset;
            m_WorkingVertices[vertexStart + 5] = m_OriginalVertices[vertexStart + 5] + tipOffset;
        }

        void ResolveVehicleReferences()
        {
            var rootObject = GameObject.Find(m_PhysicsRootName);
            m_PhysicsRoot = rootObject != null ? rootObject.transform : null;
            m_Wheels = rootObject != null ? rootObject.GetComponentsInChildren<WheelCollider>(true) : Array.Empty<WheelCollider>();
        }

        void OnApplicationQuit()
        {
            WriteFinalSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalSnapshot();
        }

        void WriteFinalSnapshot()
        {
            if (m_FinalSnapshotWritten || string.IsNullOrEmpty(m_ResultPath))
            {
                return;
            }

            m_FinalSnapshotWritten = true;
            File.AppendAllText(
                m_ResultPath,
                $"finished={DateTime.UtcNow:O};field={gameObject.name};blade_count={BladeCount};current_deformed_blades={CurrentDeformedBladeCount};max_deformed_blades={MaxDeformedBladeCount};max_fresh_affected_blades={MaxFreshAffectedBladeCount};max_deformed_fraction={MaxDeformedFraction:F3}\n");
        }
    }
}
