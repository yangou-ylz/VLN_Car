using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VLN.Editor
{
    public static class VlnTopgearUpperAssemblyTuner
    {
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string TopgearVisualName = "ScoutWheelGround_TopgearV2Visual";
        const string SensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        const string AssemblyRootName = "VLN_Topgear_UpperAssembly_UserAdjustableRoot";
        const string ConfigRelativePath = "config/topgear_upper_assembly_user_locked.json";
        const string BackupDirectoryRelativePath = "config/pose_backups";

        [MenuItem("VLN/Topgear 上装整体微调/绑定上装和传感器为整体", priority = 410)]
        public static void BindCurrentSceneFromMenu()
        {
            var result = BindCurrentScene();
            if (result.Success)
            {
                EditorUtility.DisplayDialog("绑定完成", result.Message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("绑定失败", result.Message, "确定");
            }
        }

        [MenuItem("VLN/Topgear 上装整体微调/选中上装整体", priority = 411)]
        public static void SelectAssemblyRootFromMenu()
        {
            var result = BindCurrentScene();
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("无法选中", result.Message, "确定");
                return;
            }

            Transform root = FindAssemblyRoot();
            Selection.activeTransform = root;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("VLN/Topgear 上装整体微调/保存当前小车模型", priority = 412)]
        public static void SaveCurrentVehicleAssemblyFromMenu()
        {
            var result = SaveCurrentVehicleAssembly(showDialog: true);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("保存失败", result.Message, "确定");
            }
        }

        public static bool SaveCurrentVehicleAssemblyFromCode(bool showDialog, out string message)
        {
            var result = SaveCurrentVehicleAssembly(showDialog: showDialog);
            message = result.Message;
            return result.Success;
        }

        public static void BindAndSaveMesaTopgearFromBatch()
        {
            try
            {
                EnsureSceneExists(VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath);
                EditorSceneManager.OpenScene(VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath, OpenSceneMode.Single);
                var result = SaveCurrentVehicleAssembly(showDialog: false);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Message);
                }

                Debug.Log("VLN_TOPGEAR_UPPER_ASSEMBLY_BATCH_SAVE_OK " + result.Message.Replace('\n', ' '));
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("VLN_TOPGEAR_UPPER_ASSEMBLY_BATCH_SAVE_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        public static bool ApplySavedAssemblyIfPresent(bool saveScene, bool showDialog)
        {
            string configPath = ProjectRootPath(ConfigRelativePath);
            if (!File.Exists(configPath))
            {
                return false;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var snapshot = JsonUtility.FromJson<AssemblySnapshot>(File.ReadAllText(configPath, Encoding.UTF8));
            if (snapshot == null || snapshot.transforms == null || snapshot.transforms.Length == 0)
            {
                throw new InvalidOperationException("Topgear 上装整体保存文件无效：" + configPath);
            }
            if (!string.IsNullOrEmpty(snapshot.scenePath) && !string.Equals(scene.path, snapshot.scenePath, StringComparison.Ordinal))
            {
                return false;
            }

            var bind = BindCurrentScene();
            if (!bind.Success)
            {
                throw new InvalidOperationException(bind.Message);
            }

            Transform root = FindAssemblyRoot();
            int appliedCount = 0;
            foreach (var pose in snapshot.transforms)
            {
                if (pose == null || string.IsNullOrWhiteSpace(pose.path))
                {
                    continue;
                }

                Transform target = FindRelativeTransform(root, pose.path);
                if (target == null)
                {
                    throw new InvalidOperationException("Topgear 上装整体保存路径缺失：" + pose.path);
                }

                target.localPosition = pose.localPosition;
                target.localRotation = Quaternion.Euler(pose.localEulerAngles);
                if (pose.localScale.x > 0f && pose.localScale.y > 0f && pose.localScale.z > 0f)
                {
                    target.localScale = pose.localScale;
                }
                EditorUtility.SetDirty(target.gameObject);
                appliedCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                var save = VlnWorldModelManualSaveWindow.SaveCurrentWorld(showDialog: false);
                if (!save.Success)
                {
                    throw new InvalidOperationException("Topgear 上装整体已应用，但场景保存失败：" + save.Message);
                }
            }

            string message = "已应用 Topgear 上装整体保存基线，Transform 数量=" + appliedCount.ToString(CultureInfo.InvariantCulture);
            Debug.Log("VLN_TOPGEAR_UPPER_ASSEMBLY_APPLIED count=" + appliedCount.ToString(CultureInfo.InvariantCulture) + " config=" + configPath);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("已应用", message, "确定");
            }
            return true;
        }

        static OperationResult BindCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return OperationResult.Fail("请先退出 Play 模式，再绑定或保存上装整体。");
            }

            Transform physicsRoot = FindRequired(PhysicsRootName);
            Transform visualRoot = FindRequired(VisualRootName);
            Transform topgearVisual = FindRequired(TopgearVisualName);
            Transform sensorRoot = FindRequired(SensorRootName);
            if (physicsRoot == null || visualRoot == null || topgearVisual == null || sensorRoot == null)
            {
                return OperationResult.Fail("当前场景缺少 Topgear 小车、上装或传感器对象。请先打开 mesa_topgear 场景。");
            }

            Transform assemblyRoot = FindAssemblyRoot();
            if (assemblyRoot == null)
            {
                var group = new GameObject(AssemblyRootName);
                assemblyRoot = group.transform;
                assemblyRoot.SetPositionAndRotation(topgearVisual.position, topgearVisual.rotation);
                assemblyRoot.localScale = Vector3.one;
                assemblyRoot.SetParent(visualRoot, worldPositionStays: true);
                Undo.RegisterCreatedObjectUndo(group, "Create Topgear upper assembly root");
            }
            else if (assemblyRoot.parent != visualRoot)
            {
                Undo.SetTransformParent(assemblyRoot, visualRoot, "Parent Topgear upper assembly root");
            }

            ParentUnderAssembly(topgearVisual, assemblyRoot);
            ParentUnderAssembly(sensorRoot, assemblyRoot);

            EditorUtility.SetDirty(assemblyRoot.gameObject);
            EditorUtility.SetDirty(topgearVisual.gameObject);
            EditorUtility.SetDirty(sensorRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeTransform = assemblyRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
            return OperationResult.Ok("已把上装模型、16线雷达和四个相机绑定到同一个整体节点：" + AssemblyRootName + "。现在拖动这个节点即可整体平移/旋转。");
        }

        static OperationResult SaveCurrentVehicleAssembly(bool showDialog)
        {
            var bind = BindCurrentScene();
            if (!bind.Success)
            {
                return bind;
            }

            Transform root = FindAssemblyRoot();
            var scene = EditorSceneManager.GetActiveScene();
            var snapshot = new AssemblySnapshot
            {
                schema = "vln_topgear_upper_assembly_user_locked_v1",
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                scenePath = scene.path,
                assemblyRootName = AssemblyRootName,
                note = "User-adjusted Topgear upper assembly. This stores only the visual upper module and sensor rig hierarchy; chassis, wheels, Rigidbody, WheelColliders and vehicle dynamics are not changed.",
                transforms = BuildHierarchySnapshot(root).ToArray(),
            };

            string configPath = ProjectRootPath(ConfigRelativePath);
            string backupDirectory = ProjectRootPath(BackupDirectoryRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
            Directory.CreateDirectory(backupDirectory);
            BackupFileIfExists(configPath, Path.Combine(backupDirectory, "topgear_upper_assembly_user_locked_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".json"));
            File.WriteAllText(configPath, JsonUtility.ToJson(snapshot, true), Encoding.UTF8);

            var save = VlnWorldModelManualSaveWindow.SaveCurrentWorld(showDialog: false);
            if (!save.Success)
            {
                return OperationResult.Fail("上装整体 JSON 已写入，但场景保存校验失败：" + save.Message);
            }

            string readBack = File.ReadAllText(configPath, Encoding.UTF8);
            if (readBack.IndexOf(AssemblyRootName, StringComparison.Ordinal) < 0 || readBack.IndexOf(snapshot.savedAtUtc, StringComparison.Ordinal) < 0)
            {
                return OperationResult.Fail("上装整体 JSON 回读校验失败：" + configPath);
            }

            string message = "小车上装整体已保存。\n" +
                             "整体节点：" + AssemblyRootName + "\n" +
                             "配置：" + ConfigRelativePath + "\n" +
                             "场景：" + scene.path + "\n" +
                             "底盘、轮子和动力学未改动。";
            Debug.Log("VLN_TOPGEAR_UPPER_ASSEMBLY_SAVED config=" + configPath + " transform_count=" + snapshot.transforms.Length.ToString(CultureInfo.InvariantCulture));
            if (showDialog)
            {
                EditorUtility.DisplayDialog("保存当前小车模型成功", message, "确定");
            }
            return OperationResult.Ok(message);
        }

        static void ParentUnderAssembly(Transform target, Transform assemblyRoot)
        {
            if (target == assemblyRoot || target.IsChildOf(assemblyRoot))
            {
                return;
            }

            Undo.SetTransformParent(target, assemblyRoot, "Bind Topgear upper assembly child");
        }

        static List<TransformSnapshot> BuildHierarchySnapshot(Transform root)
        {
            var result = new List<TransformSnapshot>();
            CaptureTransform(root, root.name, result);
            return result;
        }

        static void CaptureTransform(Transform transform, string path, List<TransformSnapshot> result)
        {
            result.Add(new TransformSnapshot
            {
                path = path,
                name = transform.name,
                localPosition = transform.localPosition,
                localEulerAngles = transform.localEulerAngles,
                localScale = transform.localScale,
            });

            foreach (Transform child in transform)
            {
                CaptureTransform(child, path + "/" + child.name, result);
            }
        }

        static Transform FindRelativeTransform(Transform root, string path)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            int index = parts[0] == root.name ? 1 : 0;
            Transform current = root;
            for (; index < parts.Length; index++)
            {
                Transform next = null;
                foreach (Transform child in current)
                {
                    if (child.name == parts[index])
                    {
                        next = child;
                        break;
                    }
                }
                if (next == null)
                {
                    return null;
                }
                current = next;
            }
            return current;
        }

        static Transform FindRequired(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        static Transform FindAssemblyRoot()
        {
            GameObject go = GameObject.Find(AssemblyRootName);
            return go != null ? go.transform : null;
        }

        static void EnsureSceneExists(string scenePath)
        {
            string absolutePath = Path.Combine(Application.dataPath, scenePath.Substring("Assets/".Length));
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("场景不存在", absolutePath);
            }
        }

        static string ProjectRootPath(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            return Path.Combine(root, relativePath);
        }

        static void BackupFileIfExists(string sourcePath, string backupPath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? string.Empty);
            File.Copy(sourcePath, backupPath, overwrite: true);
        }

        [Serializable]
        sealed class AssemblySnapshot
        {
            public string schema;
            public string savedAtUtc;
            public string scenePath;
            public string assemblyRootName;
            public string note;
            public TransformSnapshot[] transforms = Array.Empty<TransformSnapshot>();
        }

        [Serializable]
        sealed class TransformSnapshot
        {
            public string path;
            public string name;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;
        }

        sealed class OperationResult
        {
            public bool Success;
            public string Message = string.Empty;

            public static OperationResult Ok(string message) => new OperationResult { Success = true, Message = message };
            public static OperationResult Fail(string message) => new OperationResult { Success = false, Message = message };
        }
    }
}
