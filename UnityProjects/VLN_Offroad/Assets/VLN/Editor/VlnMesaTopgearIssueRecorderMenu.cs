using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnMesaTopgearIssueRecorderMenu
    {
        const string MenuRoot = "VLN/Mesa Desert/录制问题轨迹";

        [MenuItem(MenuRoot + "/开始录制", priority = 70)]
        public static void BeginRecording()
        {
            WithRecorder("开始录制", recorder =>
            {
                recorder.BeginRecordingFromMenu();
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_STARTED dir=" + recorder.RunDirectory);
            });
        }

        [MenuItem(MenuRoot + "/停止录制", priority = 71)]
        public static void EndRecording()
        {
            WithRecorder("停止录制", recorder =>
            {
                recorder.EndRecordingFromMenu();
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_STOPPED dir=" + recorder.RunDirectory + " samples=" + recorder.SampleCount);
            });
        }

        [MenuItem(MenuRoot + "/标记问题点", priority = 72)]
        public static void MarkIssue()
        {
            WithRecorder("标记问题点", recorder =>
            {
                recorder.MarkIssueFromMenu();
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_MARKED dir=" + recorder.RunDirectory + " marks=" + recorder.MarkedIssueCount);
            });
        }

        [MenuItem(MenuRoot + "/截图", priority = 73)]
        public static void CaptureScreenshot()
        {
            WithRecorder("截图", recorder =>
            {
                recorder.CaptureScreenshotFromMenu();
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_SCREENSHOT dir=" + recorder.RunDirectory);
            });
        }

        [MenuItem(MenuRoot + "/写入 Summary", priority = 74)]
        public static void WriteSummary()
        {
            WithRecorder("写入 Summary", recorder =>
            {
                recorder.WriteSummaryFromMenu();
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_SUMMARY dir=" + recorder.RunDirectory + " samples=" + recorder.SampleCount);
            });
        }

        [MenuItem(MenuRoot + "/打开当前记录目录", priority = 76)]
        public static void RevealCurrentDirectory()
        {
            WithRecorder("打开当前记录目录", recorder =>
            {
                string directory = recorder.RunDirectory;
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    EditorUtility.DisplayDialog("记录目录不存在", "当前 Play 会话还没有生成有效记录目录。", "知道了");
                    return;
                }
                EditorUtility.RevealInFinder(directory);
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_REVEALED dir=" + directory);
            });
        }

        [MenuItem(MenuRoot + "/复制当前记录目录", priority = 77)]
        public static void CopyCurrentDirectory()
        {
            WithRecorder("复制当前记录目录", recorder =>
            {
                string directory = recorder.RunDirectory;
                if (string.IsNullOrWhiteSpace(directory))
                {
                    EditorUtility.DisplayDialog("记录目录未就绪", "当前 Play 会话还没有生成记录目录。", "知道了");
                    return;
                }
                EditorGUIUtility.systemCopyBuffer = directory;
                Debug.Log("VLN_MESA_ISSUE_RECORDING_MENU_COPIED dir=" + directory);
            });
        }

        [MenuItem(MenuRoot + "/查看状态", priority = 78)]
        public static void ShowStatus()
        {
            WithRecorder("查看状态", recorder =>
            {
                string status = recorder.IsRecording ? "录制中" : "未录制";
                string message =
                    "状态：" + status + "\n" +
                    "样本数：" + recorder.SampleCount + "\n" +
                    "标记数：" + recorder.MarkedIssueCount + "\n" +
                    "目录：" + recorder.RunDirectory;
                EditorUtility.DisplayDialog("Mesa 问题轨迹录制", message, "知道了");
            });
        }

        static void WithRecorder(string actionName, Action<VlnMesaTopgearIssueRecorder> action)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "请先点击 Play",
                    "请先用 ./scripts/open_high_precision_world_model.sh --scene mesa_topgear 打开场景，启动 endpoint，然后在 Unity 点击 Play，再执行：“" + actionName + "”。",
                    "知道了");
                return;
            }

            var recorder = UnityEngine.Object.FindObjectOfType<VlnMesaTopgearIssueRecorder>(true);
            if (recorder == null)
            {
                EditorUtility.DisplayDialog(
                    "没有找到记录器",
                    "当前 Play 场景里没有 VlnMesaTopgearIssueRecorder。请确认打开的是 mesa_topgear 世界，而不是其他世界模型。",
                    "知道了");
                return;
            }

            action(recorder);
        }
    }
}
