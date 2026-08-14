using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VLN.ROS2;
using UnitySensors.Sensor.Camera;

namespace VLN.Editor
{
    public static class VlnOffroadVehicleCandidateProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNOffroadVehicleCandidate.unity";
        const string ModelRoot = "Assets/VLN/ExternalAssets/HuskyVisual/Models";
        const string RigName = "Offroad_SensorRig_StaticVehiclePlaceholder";
        const string HuskyRootName = "HuskyVisual_Root";
        const float VisualScale = 1.35f;
        const float Wheelbase = 0.5120f;
        const float Track = 0.5708f;
        const float WheelVerticalOffset = 0.03282f;
        const float BaseLinkGroundOffset = 0.178f;
        static readonly Vector2Int VehicleCandidateCameraResolution = new(1280, 720);
        // Unity's DAE importer preserves the Z-up Husky mesh in a way that exposes the underside unless corrected.
        static readonly Quaternion HuskyMeshUprightCorrection = Quaternion.AngleAxis(180f, Vector3.right);

        [MenuItem("VLN/Build Offroad Vehicle Candidate Scene")]
        public static void BuildVehicleCandidateScene()
        {
            VlnOffroadAssetCandidateProjectSetup.BuildAssetCandidateScene();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(VlnOffroadAssetCandidateProjectSetup.ScenePath, OpenSceneMode.Single);
            RemoveIfExists("VLN_OffroadAssetCandidate_SmokeTestController");
            RemoveIfExists("VLN_OffroadVehicleCandidate_SmokeTestController");

            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                throw new InvalidOperationException($"未找到传感器/车体 rig：{RigName}");
            }

            ReplacePlaceholderWithHuskyVisual(rig.transform);
            ConfigureVehicleCandidateCameraAndSensor(rig.transform);

            var controller = new GameObject("VLN_OffroadVehicleCandidate_SmokeTestController");
            controller.AddComponent<VlnOffroadVehicleCandidateSmokeTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_OFFROAD_VEHICLE_CANDIDATE_SETUP saved scene at {ScenePath}");
        }

        static void ReplacePlaceholderWithHuskyVisual(Transform rig)
        {
            DestroyChildIfExists(rig, "Offroad_VehiclePlaceholder_Body");
            DestroyChildIfExists(rig, HuskyRootName);

            var root = new GameObject(HuskyRootName);
            root.transform.SetParent(rig, false);
            root.transform.localPosition = new Vector3(0f, BaseLinkGroundOffset, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * VisualScale;
            root.isStatic = false;

            InstantiateHuskyPart(root.transform, "base_link", "BaseLink", Vector3.zero, 0f);
            InstantiateHuskyPart(root.transform, "top_chassis", "TopChassis", Vector3.zero, 0f);
            InstantiateHuskyPart(root.transform, "user_rail", "UserRail", Vector3.zero, 0f);
            InstantiateHuskyPart(root.transform, "bumper", "FrontBumper", new Vector3(0.48f, 0f, 0.091f), 0f);
            InstantiateHuskyPart(root.transform, "bumper", "RearBumper", new Vector3(-0.48f, 0f, 0.091f), 180f);

            InstantiateHuskyPart(root.transform, "wheel", "FrontLeftWheel", new Vector3(Wheelbase * 0.5f, Track * 0.5f, WheelVerticalOffset), 0f);
            InstantiateHuskyPart(root.transform, "wheel", "FrontRightWheel", new Vector3(Wheelbase * 0.5f, -Track * 0.5f, WheelVerticalOffset), 0f);
            InstantiateHuskyPart(root.transform, "wheel", "RearLeftWheel", new Vector3(-Wheelbase * 0.5f, Track * 0.5f, WheelVerticalOffset), 0f);
            InstantiateHuskyPart(root.transform, "wheel", "RearRightWheel", new Vector3(-Wheelbase * 0.5f, -Track * 0.5f, WheelVerticalOffset), 0f);
        }

        static void ConfigureVehicleCandidateCameraAndSensor(Transform rig)
        {
            ConfigurePreviewCamera(rig);
            ConfigureHighResolutionRgbSensor();
        }

        static void ConfigurePreviewCamera(Transform rig)
        {
            DestroyChildIfExists(rig, "VehicleCandidate_GameCamera");

            var overview = GameObject.Find("Offroad_ViewerCamera");
            if (overview != null)
            {
                var overviewCamera = overview.GetComponent<Camera>();
                if (overviewCamera != null)
                {
                    overviewCamera.depth = -10f;
                }
            }

            var cameraObject = new GameObject("VehicleCandidate_GameCamera");
            cameraObject.transform.SetParent(rig, false);
            cameraObject.transform.localPosition = new Vector3(2.35f, 1.55f, -3.25f);
            cameraObject.transform.LookAt(rig.position + Vector3.up * 1.0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 42f;
            camera.depth = 10f;

            var controller = cameraObject.AddComponent<VlnRuntimeMapCameraController>();
            controller.Configure(HuskyRootName, new Vector3(0f, 0.55f, 0f), 1.2f, 45.0f);
        }

        static void ConfigureHighResolutionRgbSensor()
        {
            foreach (var rgbSensor in UnityEngine.Object.FindObjectsOfType<RGBCameraSensor>())
            {
                var serializedSensor = new SerializedObject(rgbSensor);
                serializedSensor.FindProperty("_resolution").vector2IntValue = VehicleCandidateCameraResolution;
                serializedSensor.FindProperty("_fov").floatValue = 72f;
                serializedSensor.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static GameObject InstantiateHuskyPart(Transform root, string modelName, string instanceName, Vector3 rosPosition, float rosYawDegrees)
        {
            string path = Path.Combine(ModelRoot, modelName + ".dae").Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new FileNotFoundException($"未找到 Husky 视觉网格：{path}");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"无法实例化 Husky 视觉网格：{path}");
            }

            instance.name = "Husky_" + instanceName;
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = RosToUnity(rosPosition);
            instance.transform.localRotation = RosYawToUnityRotation(rosYawDegrees) * HuskyMeshUprightCorrection;
            instance.transform.localScale = Vector3.one;
            SetLayerRecursively(instance, 0);
            return instance;
        }

        static Quaternion RosYawToUnityRotation(float yawDegrees)
        {
            float yawRadians = yawDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yawRadians);
            float sin = Mathf.Sin(yawRadians);

            var rosLocalX = new Vector3(cos, sin, 0f);
            var rosLocalY = new Vector3(-sin, cos, 0f);
            var rosLocalZ = new Vector3(0f, 0f, 1f);

            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, ToVector4(RosDirectionToUnity(rosLocalX)));
            matrix.SetColumn(1, ToVector4(RosDirectionToUnity(rosLocalY)));
            matrix.SetColumn(2, ToVector4(RosDirectionToUnity(rosLocalZ)));
            return matrix.rotation;
        }

        static Vector3 RosToUnity(Vector3 ros)
        {
            return new Vector3(-ros.y, ros.z, ros.x);
        }

        static Vector3 RosDirectionToUnity(Vector3 ros)
        {
            return new Vector3(-ros.y, ros.z, ros.x);
        }

        static Vector4 ToVector4(Vector3 vector)
        {
            return new Vector4(vector.x, vector.y, vector.z, 0f);
        }

        static void DestroyChildIfExists(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        static void RemoveIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
