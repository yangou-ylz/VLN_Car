using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnPureNatureOasisDesertRouteCandidateBuilder
    {
        public const string SourceScenePath = "Assets/BK/PureNature_Oasis/Scenes/Scene_Oasis_Day.unity";
        public const string CandidateScenePath = "Assets/VLN/Scenes/VLNOasisDesertRouteCandidate.unity";

        const string DevelopmentRootName = "VLN_Oasis_DevelopmentRoot";

        [MenuItem("VLN/Oasis Desert/Build Route Candidate Scene")]
        public static void BuildCandidateFromMenu()
        {
            BuildCandidateScene();
            Debug.Log("VLN_OASIS_ROUTE_CANDIDATE_BUILT " + CandidateScenePath);
        }

        public static void OpenCandidateForManualReview()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(CandidateScenePath)))
            {
                BuildCandidateScene();
            }
            else
            {
                EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
            }

            Debug.Log("VLN_OASIS_ROUTE_CANDIDATE_OPENED_FOR_MANUAL_REVIEW " + CandidateScenePath);
        }

        public static void BuildCandidateScene()
        {
            EnsureRequiredAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(CandidateScenePath)) ?? string.Empty);

            if (File.Exists(ProjectRelativeToAbsolute(CandidateScenePath)))
            {
                EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), CandidateScenePath);
                EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
            }

            EnsureDevelopmentRoot();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), CandidateScenePath);
            AssetDatabase.Refresh();
        }

        static void EnsureRequiredAssets()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(SourceScenePath)))
            {
                throw new FileNotFoundException("Missing Oasis day scene", SourceScenePath);
            }
        }

        static void EnsureDevelopmentRoot()
        {
            if (GameObject.Find(DevelopmentRootName) != null)
            {
                return;
            }

            var root = new GameObject(DevelopmentRootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }
    }
}
