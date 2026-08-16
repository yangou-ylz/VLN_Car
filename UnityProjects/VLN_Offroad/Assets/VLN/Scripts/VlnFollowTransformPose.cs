using System;
using System.IO;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnFollowTransformPose : MonoBehaviour
    {
        [SerializeField] Transform m_Target;
        [SerializeField] Vector3 m_PositionOffset;
        [SerializeField] bool m_CopyRotation = true;
        [SerializeField] bool m_WriteResultLog = true;

        string m_ResultPath;
        int m_UpdateCount;
        bool m_FinalSnapshotWritten;

        public void Configure(Transform target, Vector3 positionOffset, bool copyRotation)
        {
            m_Target = target;
            m_PositionOffset = positionOffset;
            m_CopyRotation = copyRotation;
        }

        void Start()
        {
            if (!m_WriteResultLog)
            {
                return;
            }

            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_follow_transform_pose_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                $"target={(m_Target != null ? m_Target.name : "missing")}\n" +
                $"copy_rotation={m_CopyRotation}\n" +
                $"position_offset={m_PositionOffset.x:F3},{m_PositionOffset.y:F3},{m_PositionOffset.z:F3}\n");
        }

        void LateUpdate()
        {
            if (m_Target == null)
            {
                return;
            }

            transform.position = m_Target.position + m_Target.rotation * m_PositionOffset;
            if (m_CopyRotation)
            {
                transform.rotation = m_Target.rotation;
            }
            m_UpdateCount++;
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
            if (m_FinalSnapshotWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }

            m_FinalSnapshotWritten = true;
            File.AppendAllText(m_ResultPath,
                $"finished={DateTime.UtcNow:O}\n" +
                $"follow_update_count={m_UpdateCount}\n" +
                $"final_position={transform.position.x:F3},{transform.position.y:F3},{transform.position.z:F3}\n" +
                $"final_yaw_deg={transform.eulerAngles.y:F3}\n");
        }
    }
}
