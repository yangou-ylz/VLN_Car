using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VLN.Editor
{
    public sealed class VlnWorldModelManualSaveWindow : EditorWindow
    {
        public const string ManualSaveManifestRelativePath = "config/world_model_current_save.json";
        const string MarkerPrefix = "VLN_WorldManualSaveMarker_";
        const string WindowTitle = "更改世界模型";
        const string VlnSceneFolderAssetPath = "Assets/VLN/Scenes";

        string _lastStatus = "尚未执行本次保存。";

        [MenuItem("VLN/更改世界模型/打开保存面板", priority = 200)]
        public static void OpenWindow()
        {
            var window = GetWindow<VlnWorldModelManualSaveWindow>(WindowTitle);
            window.minSize = new Vector2(520f, 260f);
            window.Show();
        }

        [MenuItem("VLN/更改世界模型/保存本次世界", priority = 201)]
        public static void SaveCurrentWorldFromMenu()
        {
            SaveCurrentWorld(showDialog: true);
        }

        [MenuItem("VLN/更改世界模型/打开当前主世界", priority = 202)]
        public static void OpenCurrentWorld()
        {
            VlnPureNatureMesaOasisStitchBuilder.OpenStitchedForManualReview();
        }

        public static void OpenRegisteredSceneFromCommandLine()
        {
            string scenePath = NormalizeSceneAssetPath(CommandLineValue("--vln-open-scene"));
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new InvalidOperationException("Missing --vln-open-scene <Assets/VLN/Scenes/*.unity> argument.");
            }
            string supportedScenePath = SupportedWorldSceneOrEmpty(scenePath);
            if (string.IsNullOrEmpty(supportedScenePath))
            {
                throw new InvalidOperationException("Scene is not a registered VLN world scene: " + scenePath);
            }
            string absoluteScenePath = ProjectRelativeToAbsolute(supportedScenePath);
            if (!File.Exists(absoluteScenePath))
            {
                throw new FileNotFoundException("Registered VLN world scene file does not exist", absoluteScenePath);
            }

            EditorSceneManager.OpenScene(supportedScenePath, OpenSceneMode.Single);
            Debug.Log("VLN_WORLD_MODEL_REGISTERED_SCENE_OPENED " + supportedScenePath);
        }

        public static bool HasManualSavedStitchedWorld()
        {
            return HasManualSavedWorld(VlnPureNatureMesaOasisStitchBuilder.StitchedScenePath);
        }

        public static bool HasManualSavedWorld(string sceneAssetPath)
        {
            string manifestPath = ProjectRootPath(ManualSaveManifestRelativePath);
            string scenePath = ProjectRelativeToAbsolute(sceneAssetPath);
            if (!File.Exists(manifestPath) || !File.Exists(scenePath))
            {
                return false;
            }

            string manifestText = File.ReadAllText(manifestPath, Encoding.UTF8);
            return manifestText.IndexOf("\"scene_path\": \"" + Escape(sceneAssetPath) + "\"", StringComparison.Ordinal) >= 0;
        }

        public static string ManualSaveManifestPath => ProjectRootPath(ManualSaveManifestRelativePath);

        public static void ClearManualSaveManifestForForcedRebuild(string reason)
        {
            string manifestPath = ManualSaveManifestPath;
            if (!File.Exists(manifestPath))
            {
                return;
            }

            string backupPath = manifestPath + ".cleared_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".bak";
            File.Copy(manifestPath, backupPath, overwrite: true);
            File.Delete(manifestPath);
            Debug.LogWarning("VLN_WORLD_MODEL_MANUAL_SAVE_MANIFEST_CLEARED_FOR_REBUILD manifest=" + manifestPath + " backup=" + backupPath + " reason=" + reason);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("VLN 更改世界模型", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "请在 Edit 模式下拖动、删除或添加世界物体。点击“保存本次世界”后，会保存当前主世界场景，并用 marker + JSON 记录校验它确实写入磁盘。Play 模式下不会保存，避免假成功。",
                MessageType.Info);

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("当前打开场景", string.IsNullOrEmpty(activeScene.path) ? "<未保存场景>" : activeScene.path);
            EditorGUILayout.LabelField("当前可保存世界", SupportedWorldLabel(activeScene.path));
            EditorGUILayout.LabelField("保存记录", ManualSaveManifestRelativePath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("当前处于 Play 模式或即将进入 Play 模式。请先停止 Play，再保存世界模型。", MessageType.Error);
            }
            else if (!IsSupportedWorldScene(activeScene.path))
            {
                EditorGUILayout.HelpBox("当前打开的不是阶段 21 已注册世界。为避免保存后下次仍加载旧地图，本按钮只允许保存统一脚本内置世界，或 Assets/VLN/Scenes 下符合 VLN*WorldCandidate / VLN*RouteCandidate / VLN*TopgearVehicleCandidate / VLN*VehicleVisualCandidate 命名的自动注册世界。", MessageType.Warning);
            }

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开当前主世界", GUILayout.Height(34f)))
                {
                    OpenCurrentWorld();
                }

                if (GUILayout.Button("保存本次世界", GUILayout.Height(34f)))
                {
                    var result = SaveCurrentWorld(showDialog: true);
                    _lastStatus = result.Success ? result.Message : "保存失败：" + result.Message;
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(_lastStatus, MessageType.None);
        }

        public static SaveResult SaveCurrentWorld(bool showDialog)
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                {
                    return Fail("当前在 Play 模式，Unity 不会可靠保存运行时改动。请停止 Play 后再保存。", showDialog);
                }

                var scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    return Fail("当前没有有效加载的场景。请先打开一个已注册世界。", showDialog);
                }

                string targetScenePath = SupportedWorldSceneOrEmpty(scene.path);
                if (string.IsNullOrEmpty(targetScenePath))
                {
                    return Fail("当前场景不是已注册世界：" + scene.path + "。请先通过统一脚本打开内置世界，或把新世界场景派生到 Assets/VLN/Scenes/VLN*WorldCandidate.unity / VLN*RouteCandidate.unity / VLN*TopgearVehicleCandidate.unity / VLN*VehicleVisualCandidate.unity 后再保存。", showDialog);
                }

                DateTime savedAtUtc = DateTime.UtcNow;
                string markerName = MarkerPrefix + savedAtUtc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                ReplaceSaveMarker(scene, markerName);
                EditorSceneManager.MarkSceneDirty(scene);

                string absoluteScenePath = ProjectRelativeToAbsolute(targetScenePath);
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteScenePath) ?? string.Empty);
                bool saved = EditorSceneManager.SaveScene(scene, targetScenePath, saveAsCopy: false);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (!saved)
                {
                    return Fail("Unity SaveScene 返回失败，场景未确认写入。", showDialog);
                }
                if (!File.Exists(absoluteScenePath))
                {
                    return Fail("保存后找不到场景文件：" + absoluteScenePath, showDialog);
                }

                string sceneText = File.ReadAllText(absoluteScenePath);
                if (sceneText.IndexOf(markerName, StringComparison.Ordinal) < 0)
                {
                    return Fail("场景文件里没有找到本次保存 marker，拒绝报告成功。marker=" + markerName, showDialog);
                }

                var info = new FileInfo(absoluteScenePath);
                string sha256 = Sha256File(absoluteScenePath);
                string manifestPath = ManualSaveManifestPath;
                Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? string.Empty);
                string manifest = BuildManifestJson(savedAtUtc, targetScenePath, absoluteScenePath, markerName, info.Length, sha256);
                File.WriteAllText(manifestPath, manifest, Encoding.UTF8);

                string manifestText = File.ReadAllText(manifestPath, Encoding.UTF8);
                if (manifestText.IndexOf(markerName, StringComparison.Ordinal) < 0 || manifestText.IndexOf(sha256, StringComparison.Ordinal) < 0)
                {
                    return Fail("保存记录 JSON 校验失败，拒绝报告成功：" + manifestPath, showDialog);
                }

                string message = "世界模型已真实保存并校验通过。\n" +
                                 "场景：" + targetScenePath + "\n" +
                                 "记录：" + ManualSaveManifestRelativePath + "\n" +
                                 "marker：" + markerName;
                Debug.Log("VLN_WORLD_MODEL_MANUAL_SAVE_OK scene=" + targetScenePath + " manifest=" + manifestPath + " marker=" + markerName + " sha256=" + sha256);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("保存本次世界成功", message, "确定");
                }
                return new SaveResult { Success = true, Message = message };
            }
            catch (Exception ex)
            {
                return Fail(ex.Message, showDialog);
            }
        }

        static SaveResult Fail(string message, bool showDialog)
        {
            Debug.LogError("VLN_WORLD_MODEL_MANUAL_SAVE_FAILED " + message);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("保存本次世界失败", message, "确定");
            }
            return new SaveResult { Success = false, Message = message };
        }

        static bool IsSupportedWorldScene(string scenePath)
        {
            return !string.IsNullOrEmpty(SupportedWorldSceneOrEmpty(scenePath));
        }

        static string SupportedWorldSceneOrEmpty(string scenePath)
        {
            foreach (string candidate in SupportedWorldScenes())
            {
                if (string.Equals(scenePath, candidate, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return string.Empty;
        }

        static string SupportedWorldLabel(string scenePath)
        {
            if (string.Equals(scenePath, VlnPureNatureMesaDesertRouteCandidateBuilder.CandidateScenePath, StringComparison.Ordinal))
            {
                return "第一套 Mesa：" + scenePath;
            }
            if (string.Equals(scenePath, VlnPureNatureOasisDesertRouteCandidateBuilder.CandidateScenePath, StringComparison.Ordinal))
            {
                return "第二套 Oasis：" + scenePath;
            }
            if (string.Equals(scenePath, VlnPureNatureMesaOasisStitchBuilder.StitchedScenePath, StringComparison.Ordinal))
            {
                return "融合版 Mesa+Oasis：" + scenePath;
            }
            if (string.Equals(scenePath, VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath, StringComparison.Ordinal))
            {
                return "Mesa Topgear 真实物理车：" + scenePath;
            }
            if (string.Equals(scenePath, VlnImportedWorldSceneRegistry.MeadowCandidateScenePath, StringComparison.Ordinal))
            {
                return "Meadow 湖泊树林/草甸：" + scenePath;
            }
            if (string.Equals(scenePath, VlnImportedWorldSceneRegistry.ForestLakeCandidateScenePath, StringComparison.Ordinal))
            {
                return "ForestLake 湖边村庄/森林湖泊：" + scenePath;
            }
            if (IsAutoRegisteredWorldScene(scenePath))
            {
                return "自动注册 VLN 世界：" + Path.GetFileNameWithoutExtension(scenePath) + "：" + scenePath;
            }
            return "<当前场景不在可保存世界列表>";
        }

        static string[] SupportedWorldScenes()
        {
            var scenes = new List<string>
            {
                VlnPureNatureMesaDesertRouteCandidateBuilder.CandidateScenePath,
                VlnPureNatureOasisDesertRouteCandidateBuilder.CandidateScenePath,
                VlnPureNatureMesaOasisStitchBuilder.StitchedScenePath,
                VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath,
                VlnImportedWorldSceneRegistry.MeadowCandidateScenePath,
                VlnImportedWorldSceneRegistry.ForestLakeCandidateScenePath
            };

            foreach (string autoScene in AutoRegisteredWorldScenes())
            {
                if (!scenes.Contains(autoScene, StringComparer.Ordinal))
                {
                    scenes.Add(autoScene);
                }
            }
            return scenes.ToArray();
        }

        static IEnumerable<string> AutoRegisteredWorldScenes()
        {
            string sceneDirectory = Path.Combine(Application.dataPath, "VLN/Scenes");
            if (!Directory.Exists(sceneDirectory))
            {
                yield break;
            }

            foreach (string sceneFile in Directory.GetFiles(sceneDirectory, "*.unity").OrderBy(path => path, StringComparer.Ordinal))
            {
                string assetPath = VlnSceneFolderAssetPath + "/" + Path.GetFileName(sceneFile);
                if (IsAutoRegisteredWorldScene(assetPath))
                {
                    yield return assetPath;
                }
            }
        }

        static bool IsAutoRegisteredWorldScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || !scenePath.StartsWith(VlnSceneFolderAssetPath + "/", StringComparison.Ordinal))
            {
                return false;
            }

            string fileName = Path.GetFileName(scenePath);
            if (!fileName.StartsWith("VLN", StringComparison.Ordinal) || !fileName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return fileName.EndsWith("WorldCandidate.unity", StringComparison.Ordinal) ||
                   fileName.EndsWith("RouteCandidate.unity", StringComparison.Ordinal) ||
                   fileName.EndsWith("TopgearVehicleCandidate.unity", StringComparison.Ordinal) ||
                   fileName.EndsWith("VehicleVisualCandidate.unity", StringComparison.Ordinal) ||
                   string.Equals(fileName, "VLNHighPrecisionDesertSandbox.unity", StringComparison.Ordinal);
        }

        static void ReplaceSaveMarker(Scene scene, string markerName)
        {
            foreach (var root in scene.GetRootGameObjects().Where(go => go.name.StartsWith(MarkerPrefix, StringComparison.Ordinal)).ToArray())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var marker = new GameObject(markerName);
            marker.transform.position = Vector3.zero;
            marker.transform.rotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            SceneManager.MoveGameObjectToScene(marker, scene);
        }

        static string BuildManifestJson(DateTime savedAtUtc, string scenePath, string absoluteScenePath, string markerName, long fileSize, string sha256)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"schema\": \"vln_world_model_manual_save_v1\",");
            sb.AppendLine("  \"status\": \"verified_saved_to_disk\",");
            sb.AppendLine("  \"saved_at_utc\": \"" + Escape(savedAtUtc.ToString("O", CultureInfo.InvariantCulture)) + "\",");
            sb.AppendLine("  \"scene_path\": \"" + Escape(scenePath) + "\",");
            sb.AppendLine("  \"absolute_scene_path\": \"" + Escape(absoluteScenePath) + "\",");
            sb.AppendLine("  \"save_marker\": \"" + Escape(markerName) + "\",");
            sb.AppendLine("  \"scene_file_size_bytes\": " + fileSize.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"scene_sha256\": \"" + Escape(sha256) + "\",");
            sb.AppendLine("  \"next_open_command\": \"" + Escape(SceneOpenCommand(scenePath)) + "\",");
            sb.AppendLine("  \"note\": \"This file is written only after the Unity scene file contains the matching save marker. Rebuild scripts must not overwrite this scene unless explicitly forced.\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static string SceneOpenCommand(string scenePath)
        {
            if (string.Equals(scenePath, VlnPureNatureMesaDesertRouteCandidateBuilder.CandidateScenePath, StringComparison.Ordinal))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene mesa_desert";
            }
            if (string.Equals(scenePath, VlnPureNatureOasisDesertRouteCandidateBuilder.CandidateScenePath, StringComparison.Ordinal))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene oasis_desert";
            }
            if (string.Equals(scenePath, VlnPureNatureMesaOasisStitchBuilder.StitchedScenePath, StringComparison.Ordinal))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene mesa_oasis";
            }
            if (string.Equals(scenePath, VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath, StringComparison.Ordinal))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene mesa_topgear";
            }
            if (string.Equals(scenePath, VlnImportedWorldSceneRegistry.MeadowCandidateScenePath, StringComparison.Ordinal))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene meadow_forest";
            }
            if (string.Equals(scenePath, VlnImportedWorldSceneRegistry.ForestLakeCandidateScenePath, StringComparison.Ordinal))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene forest_lake";
            }
            if (IsAutoRegisteredWorldScene(scenePath))
            {
                return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene " + scenePath;
            }
            return "cd /home/ubuntu22/VLN && ./scripts/open_high_precision_world_model.sh --scene mesa_oasis";
        }

        static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
                string prefix = key + "=";
                if (args[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return args[i].Substring(prefix.Length);
                }
            }
            return string.Empty;
        }

        static string NormalizeSceneAssetPath(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return string.Empty;
            }

            string normalized = scenePath.Trim().Replace('\\', '/');
            string projectAssetsPrefix = Path.GetFullPath(Application.dataPath).Replace('\\', '/') + "/";
            if (normalized.StartsWith(projectAssetsPrefix, StringComparison.Ordinal))
            {
                normalized = "Assets/" + normalized.Substring(projectAssetsPrefix.Length);
            }
            if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return normalized;
            }
            if (normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return VlnSceneFolderAssetPath + "/" + Path.GetFileName(normalized);
            }
            return VlnSceneFolderAssetPath + "/" + normalized + ".unity";
        }

        static string Sha256File(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        static string ProjectRootPath(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            return Path.Combine(root, relativePath);
        }

        public sealed class SaveResult
        {
            public bool Success;
            public string Message = string.Empty;
        }
    }
}
