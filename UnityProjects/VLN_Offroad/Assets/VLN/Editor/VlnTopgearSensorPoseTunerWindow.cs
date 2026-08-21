using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VLN.Editor
{
    public sealed class VlnTopgearSensorPoseTunerWindow : EditorWindow
    {
        const string SensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        static readonly string[] SensorNames =
        {
            "Topgear_VLP16_RaycastLiDAR_UnitySensorsROS",
            "Topgear_Front_RGBCamera_UnitySensorsROS",
            "Topgear_Rear_RGBCamera_UnitySensorsROS",
            "Topgear_Left_RGBCamera_UnitySensorsROS",
            "Topgear_Right_RGBCamera_UnitySensorsROS",
        };

        [MenuItem("VLN/Topgear 传感器手动微调")]
        public static void ShowWindow()
        {
            var window = GetWindow<VlnTopgearSensorPoseTunerWindow>("Topgear 传感器微调");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Topgear 传感器手动微调", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("先打开主场景并让传感器存在。点击下面按钮选中对象后，用 Unity Scene 视图里的移动/旋转手柄拖到肉眼满意的位置，再点保存。保存结果会在下次重建场景时自动应用。", MessageType.Info);
            EditorGUILayout.SelectableLabel(VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverridePath, EditorStyles.textField, GUILayout.Height(18f));

            Transform root = FindSensorRoot();
            if (root == null)
            {
                EditorGUILayout.HelpBox($"当前场景没有找到 {SensorRootName}。请先打开或重建 VLNOffroadScoutWheelGroundCandidate 场景。", MessageType.Warning);
                if (GUILayout.Button("重建并打开主场景"))
                {
                    VlnOffroadScoutWheelGroundCandidateProjectSetup.BuildScoutWheelGroundCandidateScene();
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath);
                }
                return;
            }

            EditorGUILayout.Space(8f);
            foreach (string sensorName in SensorNames)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Transform sensor = FindDirectChild(root, sensorName);
                    EditorGUILayout.LabelField(sensorName, GUILayout.MinWidth(260f));
                    GUI.enabled = sensor != null;
                    if (GUILayout.Button("选中", GUILayout.Width(64f)))
                    {
                        Selection.activeTransform = sensor;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                    if (GUILayout.Button("复制位姿", GUILayout.Width(82f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = FormatPose(sensor);
                    }
                    GUI.enabled = true;
                }
            }

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("保存当前五个传感器位姿到 JSON", GUILayout.Height(30f)))
            {
                SaveCurrentPoses(root);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从 JSON 应用到当前场景"))
                {
                    ApplySavedPoses(root);
                }
                if (GUILayout.Button("清除 JSON 覆盖"))
                {
                    ClearSavedPoses();
                }
            }

            Transform selected = Selection.activeTransform;
            if (selected != null && selected.parent == root)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("当前选中对象", EditorStyles.boldLabel);
                EditorGUILayout.Vector3Field("Local Position", selected.localPosition);
                EditorGUILayout.Vector3Field("Local Euler", selected.localEulerAngles);
                EditorGUILayout.Vector3Field("Local Scale", selected.localScale);
            }
        }

        static Transform FindSensorRoot()
        {
            GameObject root = GameObject.Find(SensorRootName);
            return root != null ? root.transform : null;
        }

        static Transform FindDirectChild(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        static string FormatPose(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Vector3 p = transform.localPosition;
            Vector3 r = transform.localEulerAngles;
            Vector3 s = transform.localScale;
            return $"{transform.name}: pos=({p.x:F4},{p.y:F4},{p.z:F4}) euler=({r.x:F2},{r.y:F2},{r.z:F2}) scale=({s.x:F4},{s.y:F4},{s.z:F4})";
        }

        static void SaveCurrentPoses(Transform root)
        {
            var poses = new List<VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverride>();
            foreach (string sensorName in SensorNames)
            {
                Transform sensor = FindDirectChild(root, sensorName);
                if (sensor == null)
                {
                    continue;
                }

                poses.Add(new VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverride
                {
                    name = sensor.name,
                    localPosition = sensor.localPosition,
                    localEulerAngles = sensor.localEulerAngles,
                    localScale = sensor.localScale,
                });
            }

            var data = new VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverrideSet
            {
                sensors = poses.ToArray(),
            };

            string path = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverridePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("已保存", $"已保存 {poses.Count} 个传感器位姿：\n{path}", "确定");
        }

        static void ApplySavedPoses(Transform root)
        {
            string path = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverridePath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("没有 JSON", "还没有保存过传感器位姿。", "确定");
                return;
            }

            var data = JsonUtility.FromJson<VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverrideSet>(File.ReadAllText(path));
            int count = 0;
            if (data?.sensors != null)
            {
                foreach (var pose in data.sensors)
                {
                    Transform sensor = FindDirectChild(root, pose.name);
                    if (sensor == null)
                    {
                        continue;
                    }

                    sensor.localPosition = pose.localPosition;
                    sensor.localRotation = Quaternion.Euler(pose.localEulerAngles);
                    if (pose.localScale.x > 0f && pose.localScale.y > 0f && pose.localScale.z > 0f)
                    {
                        sensor.localScale = pose.localScale;
                    }
                    count++;
                }
            }

            EditorUtility.SetDirty(root.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorUtility.DisplayDialog("已应用", $"已应用 {count} 个传感器位姿。", "确定");
        }

        static void ClearSavedPoses()
        {
            string path = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverridePath;
            if (File.Exists(path))
            {
                File.Delete(path);
                AssetDatabase.Refresh();
            }
            EditorUtility.DisplayDialog("已清除", "已清除传感器位姿 JSON 覆盖。", "确定");
        }
    }
}
