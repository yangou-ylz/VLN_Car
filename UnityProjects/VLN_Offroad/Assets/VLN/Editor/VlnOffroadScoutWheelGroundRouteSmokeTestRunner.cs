using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnOffroadScoutWheelGroundRouteSmokeTestRunner
    {
        static DateTime s_Deadline;
        static bool s_StartedPlayMode;
        static bool s_ExitRequested;

        public static void Run()
        {
            s_Deadline = DateTime.UtcNow.AddSeconds(250);
            s_StartedPlayMode = false;
            s_ExitRequested = false;

            VlnOffroadScoutWheelGroundCandidateProjectSetup.BuildScoutWheelGroundCandidateScene();
            EditorSceneManager.OpenScene(VlnOffroadScoutWheelGroundCandidateProjectSetup.ScenePath);
            ExtendRuntimeAutoExitWindow();

            string resultPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_candidate_result.txt");
            if (File.Exists(resultPath))
            {
                File.Delete(resultPath);
            }

            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_OFFROAD_SCOUT_WHEEL_GROUND_ROUTE_RUNNER entering Play Mode");
        }

        static void ExtendRuntimeAutoExitWindow()
        {
            var controller = GameObject.Find("VLN_OffroadScoutWheelGroundCandidate_SmokeTestController");
            var smokeTest = controller != null ? controller.GetComponent<VlnOffroadScoutWheelGroundCandidateSmokeTest>() : null;
            if (smokeTest == null)
            {
                throw new InvalidOperationException("Missing Scout wheel-ground smoke test controller for route runner.");
            }

            var serialized = new SerializedObject(smokeTest);
            serialized.FindProperty("m_BatchModeAutoExitAfterSeconds").floatValue = 235f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_StartedPlayMode = true;
                Debug.Log("VLN_OFFROAD_SCOUT_WHEEL_GROUND_ROUTE_RUNNER entered Play Mode");
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
                Debug.Log("VLN_OFFROAD_SCOUT_WHEEL_GROUND_ROUTE_RUNNER leaving Play Mode");
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
            Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_ROUTE_RUNNER exiting with code {exitCode}");
            EditorApplication.Exit(exitCode);
        }
    }
}
