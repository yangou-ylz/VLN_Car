using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnUnitySensorsImageSmokeTestRunner
    {
        static readonly DateTime s_Deadline = DateTime.UtcNow.AddSeconds(20);
        static bool s_StartedPlayMode;
        static bool s_ExitRequested;

        public static void Run()
        {
            VlnUnitySensorsImageProjectSetup.BuildImageSmokeScene();
            EditorSceneManager.OpenScene(VlnUnitySensorsImageProjectSetup.ScenePath);

            string resultPath = Path.Combine(Application.dataPath, "../Logs/vln_unitysensors_image_result.txt");
            if (File.Exists(resultPath))
            {
                File.Delete(resultPath);
            }

            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_UNITYSENSORS_IMAGE_RUNNER entering Play Mode");
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_StartedPlayMode = true;
                Debug.Log("VLN_UNITYSENSORS_IMAGE_RUNNER entered Play Mode");
            }

            if (state == PlayModeStateChange.EnteredEditMode && s_ExitRequested)
            {
                CleanupAndExit(0);
            }
        }

        static void Tick()
        {
            if (DateTime.UtcNow < s_Deadline)
            {
                return;
            }

            s_ExitRequested = true;
            if (s_StartedPlayMode && EditorApplication.isPlaying)
            {
                Debug.Log("VLN_UNITYSENSORS_IMAGE_RUNNER leaving Play Mode");
                EditorApplication.ExitPlaymode();
            }
            else
            {
                CleanupAndExit(0);
            }
        }

        static void CleanupAndExit(int exitCode)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Debug.Log($"VLN_UNITYSENSORS_IMAGE_RUNNER exiting with code {exitCode}");
            EditorApplication.Exit(exitCode);
        }
    }
}
