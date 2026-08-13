using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Robotics.ROSTCPConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnRos2ProjectSetup
    {
        const string ScenePath = "Assets/VLN/Scenes/ROS2SmokeTest.unity";
        const string PrefabPath = "Assets/Resources/ROSConnectionPrefab.prefab";

        public static void ConfigureRos2()
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup)
                .Split(';')
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            if (!defines.Contains("ROS2"))
            {
                defines.Add("ROS2");
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", defines));
                Debug.Log($"VLN_ROS2_SETUP added ROS2 scripting define for {buildTargetGroup}");
            }
            else
            {
                Debug.Log($"VLN_ROS2_SETUP ROS2 scripting define already present for {buildTargetGroup}");
            }

            AssetDatabase.SaveAssets();
        }

        public static void BuildSmokeScene()
        {
            ConfigureRos2();
            EnsureDirectories();
            CreateRosConnectionPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rosObject = new GameObject("ROSConnection");
            var ros = rosObject.AddComponent<ROSConnection>();
            ConfigureRosConnection(ros);

            var smokeObject = new GameObject("VLN_ROS2_SmokeTest");
            smokeObject.AddComponent<VlnRos2SmokeTest>();

            var cameraObject = new GameObject("SmokeTest_Camera");
            cameraObject.transform.position = new Vector3(0f, 2f, -5f);
            cameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cameraObject.AddComponent<Camera>();

            var lightObject = new GameObject("SmokeTest_Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.AddComponent<Light>().type = LightType.Directional;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_ROS2_SETUP saved smoke scene at {ScenePath}");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/VLN/Scenes");
            Directory.CreateDirectory("Assets/VLN/Scripts");
            Directory.CreateDirectory("Assets/Resources");
        }

        static void CreateRosConnectionPrefab()
        {
            var rosObject = new GameObject("ROSConnectionPrefab");
            var ros = rosObject.AddComponent<ROSConnection>();
            ConfigureRosConnection(ros);
            PrefabUtility.SaveAsPrefabAsset(rosObject, PrefabPath);
            Object.DestroyImmediate(rosObject);
            Debug.Log($"VLN_ROS2_SETUP saved ROSConnection prefab at {PrefabPath}");
        }

        public static void ConfigureRosConnection(ROSConnection ros)
        {
            ros.RosIPAddress = "127.0.0.1";
            ros.RosPort = 10000;
            ros.ConnectOnStart = true;
            ros.ShowHud = false;
            ros.listenForTFMessages = false;
            ros.KeepaliveTime = 1f;
            ros.NetworkTimeoutSeconds = 2f;
            ros.SleepTimeSeconds = 0.01f;
            ROSConnection.SetIPPref(ros.RosIPAddress);
            ROSConnection.SetPortPref(ros.RosPort);
        }
    }
}
