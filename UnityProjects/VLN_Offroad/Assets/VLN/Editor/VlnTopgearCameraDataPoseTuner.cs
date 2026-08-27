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
    public static class VlnTopgearCameraDataPoseTuner
    {
        const string AssemblyRootName = "VLN_Topgear_UpperAssembly_UserAdjustableRoot";
        const string SensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        const string CameraVisualRootName = "VLN_Topgear_CameraVisuals_UserLockedRoot";
        const string VisualChildName = "RealSense_D405_SquareStereoVisual";
        const string ConfigRelativePath = "config/topgear_camera_data_pose_user_locked.json";
        const string UpperAssemblyConfigRelativePath = "config/topgear_upper_assembly_user_locked.json";
        const string BackupDirectoryRelativePath = "config/pose_backups";

        static readonly CameraBinding[] Cameras =
        {
            new CameraBinding("front", "Topgear_Front_RGBCamera_UnitySensorsROS", "Front_RealSense_D405_SquareStereoVisual"),
            new CameraBinding("rear", "Topgear_Rear_RGBCamera_UnitySensorsROS", "Rear_RealSense_D405_SquareStereoVisual"),
            new CameraBinding("left", "Topgear_Left_RGBCamera_UnitySensorsROS", "Left_RealSense_D405_SquareStereoVisual"),
            new CameraBinding("right", "Topgear_Right_RGBCamera_UnitySensorsROS", "Right_RealSense_D405_SquareStereoVisual"),
        };

        [MenuItem("VLN/Topgear 相机数据位姿微调/解耦视觉模型和真实相机", priority = 430)]
        public static void DecoupleVisualsFromCameraSensorsFromMenu()
        {
            var result = EnsureCameraVisualsDecoupled(saveScene: true, showDialog: false);
            EditorUtility.DisplayDialog(result.Success ? "解耦完成" : "解耦失败", result.Message, "确定");
        }

        [MenuItem("VLN/Topgear 相机数据位姿微调/选中前真实相机", priority = 431)]
        public static void SelectFrontCameraFromMenu() => SelectCamera("Topgear_Front_RGBCamera_UnitySensorsROS");

        [MenuItem("VLN/Topgear 相机数据位姿微调/选中后真实相机", priority = 432)]
        public static void SelectRearCameraFromMenu() => SelectCamera("Topgear_Rear_RGBCamera_UnitySensorsROS");

        [MenuItem("VLN/Topgear 相机数据位姿微调/选中左真实相机", priority = 433)]
        public static void SelectLeftCameraFromMenu() => SelectCamera("Topgear_Left_RGBCamera_UnitySensorsROS");

        [MenuItem("VLN/Topgear 相机数据位姿微调/选中右真实相机", priority = 434)]
        public static void SelectRightCameraFromMenu() => SelectCamera("Topgear_Right_RGBCamera_UnitySensorsROS");

        [MenuItem("VLN/Topgear 相机数据位姿微调/保存当前四路真实相机位姿", priority = 435)]
        public static void SaveCurrentCameraDataPosesFromMenu()
        {
            var result = SaveCurrentCameraDataPoses(showDialog: false);
            EditorUtility.DisplayDialog(result.Success ? "保存完成" : "保存失败", result.Message, "确定");
        }

        [MenuItem("VLN/Topgear 相机数据位姿微调/应用已保存四路真实相机位姿", priority = 436)]
        public static void ApplySavedCameraDataPosesFromMenu()
        {
            bool applied = ApplySavedCameraDataPosesIfPresent(saveScene: true, showDialog: true);
            if (!applied)
            {
                EditorUtility.DisplayDialog("没有保存记录", "还没有保存过四路真实相机数据位姿。", "确定");
            }
        }

        public static bool HasSavedCameraDataPoses()
        {
            return File.Exists(ProjectRootPath(ConfigRelativePath));
        }

        public static bool SavedStateRequiresCameraVisualDecoupling()
        {
            if (HasSavedCameraDataPoses())
            {
                return true;
            }

            string upperAssemblyConfig = ProjectRootPath(UpperAssemblyConfigRelativePath);
            return File.Exists(upperAssemblyConfig) && File.ReadAllText(upperAssemblyConfig, Encoding.UTF8).IndexOf(CameraVisualRootName, StringComparison.Ordinal) >= 0;
        }

        public static void EnableAndSaveMesaTopgearFromBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath, OpenSceneMode.Single);
                var result = SaveCurrentCameraDataPoses(showDialog: false);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Message);
                }

                Debug.Log("VLN_TOPGEAR_CAMERA_DATA_POSE_BATCH_SAVE_OK " + result.Message.Replace('\n', ' '));
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("VLN_TOPGEAR_CAMERA_DATA_POSE_BATCH_SAVE_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        public static bool EnsureDecoupledIfSavedStateRequiresIt(bool saveScene, bool showDialog)
        {
            if (!SavedStateRequiresCameraVisualDecoupling())
            {
                return false;
            }

            var result = EnsureCameraVisualsDecoupled(saveScene: saveScene, showDialog: showDialog);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }
            return true;
        }

        public static bool ApplySavedCameraDataPosesIfPresent(bool saveScene, bool showDialog)
        {
            string configPath = ProjectRootPath(ConfigRelativePath);
            if (!File.Exists(configPath))
            {
                return false;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var snapshot = JsonUtility.FromJson<CameraPoseSnapshot>(File.ReadAllText(configPath, Encoding.UTF8));
            if (snapshot == null || snapshot.cameras == null || snapshot.cameras.Length == 0)
            {
                throw new InvalidOperationException("四路真实相机位姿保存文件无效：" + configPath);
            }
            if (!string.IsNullOrEmpty(snapshot.scenePath) && !string.Equals(scene.path, snapshot.scenePath, StringComparison.Ordinal))
            {
                return false;
            }

            var decouple = EnsureCameraVisualsDecoupled(saveScene: false, showDialog: false);
            if (!decouple.Success)
            {
                throw new InvalidOperationException(decouple.Message);
            }

            Transform sensorRoot = FindRequired(SensorRootName);
            int appliedCount = 0;
            foreach (var pose in snapshot.cameras)
            {
                if (pose == null || string.IsNullOrWhiteSpace(pose.name))
                {
                    continue;
                }

                Transform camera = FindDirectChild(sensorRoot, pose.name);
                if (camera == null)
                {
                    throw new InvalidOperationException("缺少真实相机对象：" + pose.name);
                }

                camera.localPosition = pose.localPosition;
                camera.localRotation = Quaternion.Euler(pose.localEulerAngles);
                if (pose.localScale.x > 0f && pose.localScale.y > 0f && pose.localScale.z > 0f)
                {
                    camera.localScale = pose.localScale;
                }
                EditorUtility.SetDirty(camera.gameObject);
                appliedCount++;
            }

            if (appliedCount != Cameras.Length)
            {
                throw new InvalidOperationException("四路真实相机位姿只应用了 " + appliedCount.ToString(CultureInfo.InvariantCulture) + "/" + Cameras.Length.ToString(CultureInfo.InvariantCulture));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                var save = VlnWorldModelManualSaveWindow.SaveCurrentWorld(showDialog: false);
                if (!save.Success)
                {
                    throw new InvalidOperationException("真实相机数据位姿已应用，但场景保存失败：" + save.Message);
                }
            }

            string message = "已应用四路真实相机数据位姿，视觉 D405 模型保持独立。";
            Debug.Log("VLN_TOPGEAR_CAMERA_DATA_POSE_APPLIED count=" + appliedCount.ToString(CultureInfo.InvariantCulture) + " config=" + configPath);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("已应用", message, "确定");
            }
            return true;
        }

        static OperationResult SaveCurrentCameraDataPoses(bool showDialog)
        {
            var decouple = EnsureCameraVisualsDecoupled(saveScene: false, showDialog: false);
            if (!decouple.Success)
            {
                return decouple;
            }

            Transform sensorRoot = FindRequired(SensorRootName);
            var poses = new List<CameraPose>();
            foreach (var binding in Cameras)
            {
                Transform camera = FindDirectChild(sensorRoot, binding.CameraName);
                if (camera == null)
                {
                    return OperationResult.Fail("缺少真实相机对象：" + binding.CameraName);
                }

                poses.Add(new CameraPose
                {
                    role = binding.Role,
                    name = camera.name,
                    localPosition = camera.localPosition,
                    localEulerAngles = camera.localEulerAngles,
                    localScale = camera.localScale,
                });
            }

            var scene = EditorSceneManager.GetActiveScene();
            var snapshot = new CameraPoseSnapshot
            {
                schema = "vln_topgear_camera_data_pose_user_locked_v1",
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                scenePath = scene.path,
                sensorRootName = SensorRootName,
                note = "User-adjusted capture/data poses for the four Topgear fisheye cameras. RealSense D405 visual meshes are decoupled and kept at their visual installation poses.",
                cameras = poses.ToArray(),
            };

            string configPath = ProjectRootPath(ConfigRelativePath);
            string backupDirectory = ProjectRootPath(BackupDirectoryRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
            Directory.CreateDirectory(backupDirectory);
            BackupFileIfExists(configPath, Path.Combine(backupDirectory, "topgear_camera_data_pose_user_locked_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".json"));
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(configPath, json, Encoding.UTF8);

            if (!VlnTopgearUpperAssemblyTuner.SaveCurrentVehicleAssemblyFromCode(showDialog: false, out string assemblySaveMessage))
            {
                return OperationResult.Fail("四路真实相机 JSON 已写入，但上装整体/场景保存失败：" + assemblySaveMessage);
            }

            string readBack = File.ReadAllText(configPath, Encoding.UTF8);
            if (readBack.IndexOf(snapshot.savedAtUtc, StringComparison.Ordinal) < 0 || readBack.IndexOf(Cameras[0].CameraName, StringComparison.Ordinal) < 0)
            {
                return OperationResult.Fail("四路真实相机 JSON 回读校验失败：" + configPath);
            }

            string message = "四路真实相机数据位姿已保存。\n" +
                             "配置：" + ConfigRelativePath + "\n" +
                             "视觉模型根：" + CameraVisualRootName + "\n" +
                             "场景已通过世界保存机制写入。";
            Debug.Log("VLN_TOPGEAR_CAMERA_DATA_POSE_SAVED config=" + configPath + " camera_count=" + poses.Count.ToString(CultureInfo.InvariantCulture));
            if (showDialog)
            {
                EditorUtility.DisplayDialog("保存完成", message, "确定");
            }
            return OperationResult.Ok(message);
        }

        static OperationResult EnsureCameraVisualsDecoupled(bool saveScene, bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                return OperationResult.Fail("请先退出 Play 模式，再解耦或保存相机数据位姿。");
            }

            Transform assemblyRoot = FindByName(AssemblyRootName);
            if (assemblyRoot == null)
            {
                if (!VlnTopgearUpperAssemblyTuner.SaveCurrentVehicleAssemblyFromCode(showDialog: false, out string assemblyMessage))
                {
                    return OperationResult.Fail("当前场景还没有上装整体根节点，且自动绑定失败：" + assemblyMessage);
                }
                assemblyRoot = FindByName(AssemblyRootName);
            }

            Transform sensorRoot = FindRequired(SensorRootName);
            Transform visualRoot = FindByName(CameraVisualRootName);
            if (visualRoot == null)
            {
                var go = new GameObject(CameraVisualRootName);
                visualRoot = go.transform;
                visualRoot.SetParent(assemblyRoot, false);
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(go, "Create Topgear camera visual root");
            }
            else if (visualRoot.parent != assemblyRoot)
            {
                Undo.SetTransformParent(visualRoot, assemblyRoot, "Parent Topgear camera visual root");
            }

            int movedCount = 0;
            foreach (var binding in Cameras)
            {
                Transform camera = FindDirectChild(sensorRoot, binding.CameraName);
                if (camera == null)
                {
                    return OperationResult.Fail("缺少真实相机对象：" + binding.CameraName);
                }

                Transform visual = FindDirectChild(camera, VisualChildName) ?? FindDirectChild(visualRoot, binding.VisualName);
                if (visual == null)
                {
                    return OperationResult.Fail("缺少 D405 视觉模型：" + binding.CameraName + "/" + VisualChildName + " 或 " + CameraVisualRootName + "/" + binding.VisualName);
                }

                if (visual.name != binding.VisualName)
                {
                    visual.name = binding.VisualName;
                }
                if (visual.parent != visualRoot)
                {
                    Undo.SetTransformParent(visual, visualRoot, "Decouple D405 visual from camera data transform");
                    movedCount++;
                }
                EditorUtility.SetDirty(visual.gameObject);
            }

            EditorUtility.SetDirty(visualRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (saveScene)
            {
                if (!VlnTopgearUpperAssemblyTuner.SaveCurrentVehicleAssemblyFromCode(showDialog: false, out string assemblySaveMessage))
                {
                    return OperationResult.Fail("相机视觉/数据已解耦，但上装整体/场景保存失败：" + assemblySaveMessage);
                }
            }

            string message = "已解耦四路 D405 视觉模型和真实相机数据 Transform。\n" +
                             "现在可以只拖动 Topgear_Front/Rear/Left/Right_RGBCamera_UnitySensorsROS 来改变真实图像采集位置，视觉模型不会跟着动。\n" +
                             "本次移动视觉模型数量=" + movedCount.ToString(CultureInfo.InvariantCulture) + "。";
            Debug.Log("VLN_TOPGEAR_CAMERA_DATA_VISUAL_DECOUPLED moved_visual_count=" + movedCount.ToString(CultureInfo.InvariantCulture));
            if (showDialog)
            {
                EditorUtility.DisplayDialog("相机视觉/数据已解耦", message, "确定");
            }
            return OperationResult.Ok(message);
        }

        static void SelectCamera(string cameraName)
        {
            var decouple = EnsureCameraVisualsDecoupled(saveScene: false, showDialog: false);
            if (!decouple.Success)
            {
                EditorUtility.DisplayDialog("无法选中", decouple.Message, "确定");
                return;
            }

            Transform sensorRoot = FindRequired(SensorRootName);
            Transform camera = FindDirectChild(sensorRoot, cameraName);
            if (camera == null)
            {
                EditorUtility.DisplayDialog("无法选中", "缺少真实相机对象：" + cameraName, "确定");
                return;
            }

            Selection.activeTransform = camera;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        static Transform FindRequired(string name)
        {
            Transform transform = FindByName(name);
            if (transform == null)
            {
                throw new InvalidOperationException("当前场景缺少对象：" + name + "。请先打开 mesa_topgear 场景。");
            }
            return transform;
        }

        static Transform FindByName(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        static Transform FindDirectChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }
            foreach (Transform child in root)
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
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

        sealed class CameraBinding
        {
            public readonly string Role;
            public readonly string CameraName;
            public readonly string VisualName;

            public CameraBinding(string role, string cameraName, string visualName)
            {
                Role = role;
                CameraName = cameraName;
                VisualName = visualName;
            }
        }

        [Serializable]
        sealed class CameraPoseSnapshot
        {
            public string schema;
            public string savedAtUtc;
            public string scenePath;
            public string sensorRootName;
            public string note;
            public CameraPose[] cameras = Array.Empty<CameraPose>();
        }

        [Serializable]
        sealed class CameraPose
        {
            public string role;
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
