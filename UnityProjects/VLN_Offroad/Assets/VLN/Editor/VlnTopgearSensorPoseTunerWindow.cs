using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        public static void LockCurrentSceneSensorPosesBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath);
                Transform root = FindSensorRoot();
                if (root == null)
                {
                    throw new InvalidOperationException($"Missing {SensorRootName} in current scene. Refusing to write locked sensor poses.");
                }

                SaveCurrentPoses(root, showDialog: false);
                Debug.Log("VLN_TOPGEAR_SENSOR_POSE_BATCH_LOCK_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"VLN_TOPGEAR_SENSOR_POSE_BATCH_LOCK_FAILED {ex}");
                EditorApplication.Exit(1);
            }
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Topgear 传感器手动微调", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("先打开主场景并让传感器存在。选中对象后，用 Unity Scene 视图里的移动/旋转手柄拖到肉眼满意的位置，再点“保存并锁定”。保存会同时写锁定 JSON、普通 JSON、恢复备份，并保存当前 Unity 主场景。", MessageType.Info);
            EditorGUILayout.LabelField("锁定基线 JSON", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseUserLockedPath, EditorStyles.textField, GUILayout.Height(18f));
            EditorGUILayout.LabelField("兼容 JSON", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverridePath, EditorStyles.textField, GUILayout.Height(18f));

            Transform root = FindSensorRoot();
            if (root == null)
            {
                EditorGUILayout.HelpBox($"当前场景没有找到 {SensorRootName}。请先打开 VLNOffroadScoutWheelGroundCandidate 主场景。这里不会重建场景，避免覆盖手动位姿。", MessageType.Warning);
                if (GUILayout.Button("只打开主场景（不重建）"))
                {
                    EditorSceneManager.OpenScene(VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath);
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
            if (GUILayout.Button("保存当前五个传感器位姿并锁定为唯一基线", GUILayout.Height(34f)))
            {
                SaveCurrentPoses(root, showDialog: true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从锁定 JSON 应用到当前场景"))
                {
                    ApplySavedPoses(root);
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

        static void SaveCurrentPoses(Transform root, bool showDialog)
        {
            var scene = root.gameObject.scene;
            if (scene.path != VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath)
            {
                string message = $"当前场景不是主场景，拒绝写入锁定基线。当前：{scene.path}；需要：{VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath}";
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("未保存", message, "确定");
                }
                else
                {
                    throw new InvalidOperationException(message);
                }
                return;
            }

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

            if (poses.Count != SensorNames.Length)
            {
                string message = $"只找到 {poses.Count}/{SensorNames.Length} 个传感器。为避免锁定错误基线，已拒绝保存。";
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("未保存", message, "确定");
                }
                else
                {
                    throw new InvalidOperationException(message);
                }
                return;
            }

            var data = new VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverrideSet
            {
                sensors = poses.ToArray(),
            };
            var hierarchyData = new VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorHierarchyOverrideSet
            {
                transforms = BuildHierarchySnapshot(root).ToArray(),
            };

            string json = JsonUtility.ToJson(data, true);
            string hierarchyJson = JsonUtility.ToJson(hierarchyData, true);
            string overridePath = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseOverridePath;
            string lockedPath = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseUserLockedPath;
            string hierarchyPath = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorHierarchyUserLockedPath;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDirectory = Path.Combine(Path.GetDirectoryName(overridePath), "pose_backups");
            string lockedSceneDirectory = Path.Combine(Path.GetDirectoryName(overridePath), "topgear_sensor_scene_locked");
            string recoveryDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_ManualRecoveryLogs"));

            Directory.CreateDirectory(Path.GetDirectoryName(overridePath));
            Directory.CreateDirectory(backupDirectory);
            Directory.CreateDirectory(lockedSceneDirectory);
            Directory.CreateDirectory(recoveryDirectory);

            BackupFileIfExists(overridePath, Path.Combine(backupDirectory, $"topgear_sensor_pose_overrides_before_user_lock_{stamp}.json"));
            BackupFileIfExists(lockedPath, Path.Combine(backupDirectory, $"topgear_sensor_pose_user_locked_before_user_lock_{stamp}.json"));
            BackupFileIfExists(hierarchyPath, Path.Combine(backupDirectory, $"topgear_sensor_hierarchy_user_locked_before_user_lock_{stamp}.json"));

            File.WriteAllText(lockedPath, json);
            File.WriteAllText(overridePath, json);
            File.WriteAllText(hierarchyPath, hierarchyJson);
            File.WriteAllText(Path.Combine(backupDirectory, $"topgear_sensor_pose_user_locked_{stamp}.json"), json);
            File.WriteAllText(Path.Combine(backupDirectory, $"topgear_sensor_hierarchy_user_locked_{stamp}.json"), hierarchyJson);
            File.WriteAllText(Path.Combine(recoveryDirectory, $"topgear_sensor_pose_user_locked_{stamp}.json"), json);
            File.WriteAllText(Path.Combine(recoveryDirectory, $"topgear_sensor_hierarchy_user_locked_{stamp}.json"), hierarchyJson);

            EditorUtility.SetDirty(root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string sceneFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scene.path));
            BackupFileIfExists(sceneFullPath, Path.Combine(lockedSceneDirectory, "VLNOffroadScoutWheelGroundCandidate_user_locked.unity"));
            BackupFileIfExists(sceneFullPath + ".meta", Path.Combine(lockedSceneDirectory, "VLNOffroadScoutWheelGroundCandidate_user_locked.unity.meta"));
            BackupFileIfExists(sceneFullPath, Path.Combine(recoveryDirectory, $"VLNOffroadScoutWheelGroundCandidate_user_locked_{stamp}.unity"));
            BackupFileIfExists(sceneFullPath + ".meta", Path.Combine(recoveryDirectory, $"VLNOffroadScoutWheelGroundCandidate_user_locked_{stamp}.unity.meta"));

            AssetDatabase.Refresh();
            Debug.Log($"VLN_TOPGEAR_SENSOR_POSE_LOCKED path={lockedPath} compatible={overridePath} hierarchy={hierarchyPath} count={poses.Count} hierarchy_count={hierarchyData.transforms.Length}");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("已锁定", $"已锁定 {poses.Count} 个传感器位姿和 {hierarchyData.transforms.Length} 个层级 Transform。\n锁定 JSON：{lockedPath}\n层级 JSON：{hierarchyPath}\n当前主场景也已保存。", "确定");
            }
        }

        static void ApplySavedPoses(Transform root)
        {
            string path = VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorPoseUserLockedPath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("没有锁定 JSON", "还没有保存过锁定基线。请先拖好五个传感器，再点击“保存并锁定”。", "确定");
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

            if (count != SensorNames.Length)
            {
                EditorUtility.DisplayDialog("未完成", $"锁定 JSON 只应用了 {count}/{SensorNames.Length} 个传感器。请不要继续重建，先检查对象名称。", "确定");
                return;
            }

            EditorUtility.SetDirty(root.gameObject);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            EditorUtility.DisplayDialog("已应用", $"已应用并保存 {count} 个传感器位姿。", "确定");
        }

        static void BackupFileIfExists(string sourcePath, string backupPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            File.Copy(sourcePath, backupPath, overwrite: true);
        }

        static List<VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorHierarchyOverride> BuildHierarchySnapshot(Transform root)
        {
            var transforms = new List<VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorHierarchyOverride>();
            CaptureTransform(root, root.name, transforms);
            return transforms;
        }

        static void CaptureTransform(Transform transform, string path, List<VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorHierarchyOverride> transforms)
        {
            transforms.Add(new VlnOffroadScoutWheelGroundCandidateProjectSetup.TopgearSensorHierarchyOverride
            {
                path = path,
                localPosition = transform.localPosition,
                localEulerAngles = transform.localEulerAngles,
                localScale = transform.localScale,
            });

            foreach (Transform child in transform)
            {
                CaptureTransform(child, $"{path}/{child.name}", transforms);
            }
        }

    }
}
