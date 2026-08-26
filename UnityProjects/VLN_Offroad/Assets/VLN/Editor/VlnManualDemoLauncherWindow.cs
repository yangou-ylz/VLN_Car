using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public sealed class VlnManualDemoLauncherWindow : EditorWindow
    {
        const string VlnRoot = "/home/ubuntu22/VLN";
        const string LauncherScript = "/home/ubuntu22/VLN/scripts/unity_menu_launch.sh";
        const string CleanupScript = "/home/ubuntu22/VLN/scripts/cleanup_unity_menu_processes.sh";
        const string ControlPanelUrl = "http://127.0.0.1:8765/";

        Vector2 m_Scroll;

        [MenuItem("VLN/ROS2 手工演示面板", priority = 1)]
        public static void OpenWindow()
        {
            var window = GetWindow<VlnManualDemoLauncherWindow>("VLN ROS2 演示");
            window.minSize = new Vector2(460f, 420f);
            window.Show();
        }

        [MenuItem("VLN/手工演示/1 打开 Scout 场景", priority = 10)]
        public static void OpenScoutSceneMenu()
        {
            OpenScoutScene();
        }

        [MenuItem("VLN/手工演示/2 启动 ROS-TCP-Endpoint", priority = 11)]
        public static void StartEndpointMenu()
        {
            LaunchMode("endpoint", requirePlayMode: false);
        }

        [MenuItem("VLN/手工演示/3 运行 16 点挑战路线", priority = 12)]
        public static void StartChallengeRouteMenu()
        {
            LaunchMode("challenge", requirePlayMode: true);
        }

        [MenuItem("VLN/手工演示/查看相机图像/rqt", priority = 30)]
        public static void ViewImageRqtMenu() => LaunchMode("image_all", requirePlayMode: true);

        [MenuItem("VLN/手工演示/查看相机图像/全部相机", priority = 31)]
        public static void ViewImageAllMenu() => VlnTopgearCameraPreviewWindow.OpenAllCameras();

        [MenuItem("VLN/手工演示/查看相机图像/前相机", priority = 32)]
        public static void ViewImageFrontMenu() => VlnTopgearCameraPreviewWindow.OpenFrontCamera();

        [MenuItem("VLN/手工演示/查看相机图像/后相机", priority = 33)]
        public static void ViewImageRearMenu() => VlnTopgearCameraPreviewWindow.OpenRearCamera();

        [MenuItem("VLN/手工演示/查看相机图像/左相机", priority = 34)]
        public static void ViewImageLeftMenu() => VlnTopgearCameraPreviewWindow.OpenLeftCamera();

        [MenuItem("VLN/手工演示/查看相机图像/右相机", priority = 35)]
        public static void ViewImageRightMenu() => VlnTopgearCameraPreviewWindow.OpenRightCamera();

        [MenuItem("VLN/手工演示/查看雷达点云", priority = 40)]
        public static void ViewRvizMenu()
        {
            LaunchMode("rviz", requirePlayMode: true);
        }

        [MenuItem("VLN/手工演示/启动中文控制面板", priority = 41)]
        public static void StartControlPanelMenu()
        {
            LaunchMode("panel", requirePlayMode: true);
        }

        [MenuItem("VLN/手工演示/关闭 VLN 后台终端", priority = 90)]
        public static void CleanupMenuProcessesMenu()
        {
            CleanupUnityMenuProcesses(includeKnownProjectProcesses: true, showDialog: true);
        }

        void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            EditorGUILayout.LabelField("VLN ROS2 手工演示面板", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这个面板只启动现有 ROS2 脚本。控制仍在 ROS2 外部完成，Unity 只提供仿真世界、传感器和物理交互。推荐顺序：打开场景 -> 启动 endpoint -> Unity 点击 Play -> 运行挑战路线或查看传感器。",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("基础流程", EditorStyles.boldLabel);
                if (GUILayout.Button("1. 打开 Scout wheel-ground 场景", GUILayout.Height(30f)))
                {
                    OpenScoutScene();
                }

                if (GUILayout.Button("2. 启动 ROS-TCP-Endpoint", GUILayout.Height(30f)))
                {
                    LaunchMode("endpoint", requirePlayMode: false);
                }

                EditorGUILayout.HelpBox("启动 endpoint 后，回到 Unity 顶部点击 Play，再运行挑战路线或查看传感器。", MessageType.None);

                if (GUILayout.Button("3. 运行 16 点挑战路线", GUILayout.Height(30f)))
                {
                    LaunchMode("challenge", requirePlayMode: true);
                }
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("传感器和控制", EditorStyles.boldLabel);
                if (GUILayout.Button("查看全部相机", GUILayout.Height(28f)))
                {
                    VlnTopgearCameraPreviewWindow.OpenAllCameras();
                }

                if (GUILayout.Button("打开雷达点云 RViz", GUILayout.Height(28f)))
                {
                    LaunchMode("rviz", requirePlayMode: true);
                }

                if (GUILayout.Button("启动中文控制面板", GUILayout.Height(28f)))
                {
                    LaunchMode("panel", requirePlayMode: true);
                }

                if (GUILayout.Button("打开控制面板网页", GUILayout.Height(28f)))
                {
                    Application.OpenURL(ControlPanelUrl);
                }

                if (GUILayout.Button("关闭 VLN 后台终端", GUILayout.Height(28f)))
                {
                    CleanupUnityMenuProcesses(includeKnownProjectProcesses: true, showDialog: true);
                }
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("说明", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel("场景：" + VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath, GUILayout.Height(18f));
                EditorGUILayout.SelectableLabel("启动包装器：" + LauncherScript, GUILayout.Height(18f));
                EditorGUILayout.SelectableLabel("控制 topic：/vln/cmd_vel", GUILayout.Height(18f));
            }

            EditorGUILayout.EndScrollView();
        }

        static void OpenScoutScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("当前正在 Play", "请先退出 Play Mode，再打开或切换场景。", "知道了");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string scenePath = VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                EditorUtility.DisplayDialog("场景不存在", "找不到场景：" + scenePath, "知道了");
                return;
            }

            EditorSceneManager.OpenScene(scenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            EditorUtility.DisplayDialog("场景已打开", "下一步：启动 ROS-TCP-Endpoint，然后点击 Unity 顶部 Play。", "知道了");
        }

        static void LaunchMode(string mode, bool requirePlayMode)
        {
            if (!File.Exists(LauncherScript))
            {
                EditorUtility.DisplayDialog("启动包装器缺失", "找不到脚本：" + LauncherScript, "知道了");
                return;
            }

            if (requirePlayMode && !EditorApplication.isPlaying)
            {
                bool shouldContinue = EditorUtility.DisplayDialog(
                    "建议先点击 Play",
                    "这个功能需要 Unity 场景正在 Play，并且 ROS-TCP-Endpoint 已启动。是否仍然启动外部脚本？",
                    "继续启动",
                    "取消");
                if (!shouldContinue)
                {
                    return;
                }
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = LauncherScript,
                    Arguments = mode + " " + Process.GetCurrentProcess().Id,
                    WorkingDirectory = VlnRoot,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                Process.Start(startInfo);
                UnityEngine.Debug.Log($"VLN manual demo launcher started mode={mode}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("启动失败", ex.Message, "知道了");
                UnityEngine.Debug.LogError(ex);
            }
        }

        internal static void CleanupUnityMenuProcesses(bool includeKnownProjectProcesses, bool showDialog)
        {
            if (!File.Exists(CleanupScript))
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("清理脚本缺失", "找不到脚本：" + CleanupScript, "知道了");
                }
                return;
            }

            string arguments = includeKnownProjectProcesses ? "--include-known" : "--tracked-only";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = CleanupScript,
                    Arguments = arguments,
                    WorkingDirectory = VlnRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    bool exited = process.WaitForExit(3000);
                    if (!exited)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // Best-effort termination of the cleanup helper process.
                        }
                    }

                    UnityEngine.Debug.Log($"VLN cleanup finished args={arguments} exit={(exited ? process.ExitCode.ToString() : "timeout")} output={output} error={error}");
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog("清理完成", string.IsNullOrWhiteSpace(output) ? "已请求关闭 VLN 后台终端。" : output.Trim(), "知道了");
                    }
                }
            }
            catch (Exception ex)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("清理失败", ex.Message, "知道了");
                }
                UnityEngine.Debug.LogError(ex);
            }
        }
    }
}
