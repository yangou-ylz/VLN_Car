using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySensors.Sensor.Camera;
using VLN.ROS2;
using CameraInfoMsgPublisher = UnitySensors.ROS.Publisher.Camera.CameraInfoMsgPublisher;
using ImageMsgPublisher = UnitySensors.ROS.Publisher.Sensor.ImageMsgPublisher;

namespace VLN.Editor
{
    public static class VlnUnitySensorsImageProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/UnitySensorsImageSmokeTest.unity";
        const string ImageTopic = "/vln/front/image_raw";
        const string CameraInfoTopic = "/vln/front/camera_info";
        const string FrameId = "front_camera_optical_frame";
        const float FrequencyHz = 5f;
        static readonly Vector2Int Resolution = new(640, 480);

        public static void BuildImageSmokeScene()
        {
            VlnRos2ProjectSetup.ConfigureRos2();
            EnsureDirectories();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rosObject = new GameObject("ROSConnection");
            var ros = rosObject.AddComponent<ROSConnection>();
            VlnRos2ProjectSetup.ConfigureRosConnection(ros);

            CreateLighting();
            CreateVisualTargets();
            CreateViewerCamera();
            CreateImageSensorRig();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_UNITYSENSORS_IMAGE_SETUP saved scene at {ScenePath}");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/VLN/Scenes");
            Directory.CreateDirectory("Assets/VLN/Scripts");
            Directory.CreateDirectory("Assets/VLN/Materials");
        }

        static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.42f);

            var lightObject = new GameObject("ImageSmokeTest_DirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        static void CreateVisualTargets()
        {
            var groundMaterial = EnsureMaterial("Assets/VLN/Materials/Smoke_Ground.mat", new Color(0.28f, 0.36f, 0.24f));
            var redMaterial = EnsureMaterial("Assets/VLN/Materials/Smoke_Red.mat", new Color(0.78f, 0.15f, 0.12f));
            var blueMaterial = EnsureMaterial("Assets/VLN/Materials/Smoke_Blue.mat", new Color(0.12f, 0.32f, 0.82f));
            var yellowMaterial = EnsureMaterial("Assets/VLN/Materials/Smoke_Yellow.mat", new Color(0.95f, 0.72f, 0.12f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ImageSmokeTest_Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            CreateCube("ImageSmokeTest_RedCube", new Vector3(-1.6f, 0.6f, 6f), new Vector3(1.2f, 1.2f, 1.2f), redMaterial);
            CreateCube("ImageSmokeTest_BlueBox", new Vector3(1.4f, 0.4f, 8f), new Vector3(1.5f, 0.8f, 1.0f), blueMaterial);
            CreateCube("ImageSmokeTest_YellowMarker", new Vector3(0f, 1.0f, 11f), new Vector3(0.8f, 2.0f, 0.8f), yellowMaterial);
        }

        static void CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material EnsureMaterial(string assetPath, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateViewerCamera()
        {
            var viewerObject = new GameObject("ImageSmokeTest_ViewerCamera");
            viewerObject.transform.position = new Vector3(0f, 3.0f, -8.0f);
            viewerObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            var camera = viewerObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.54f, 0.66f, 0.78f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.depth = -1f;
        }

        static void CreateImageSensorRig()
        {
            var cameraObject = new GameObject("Front_RGBCamera_UnitySensorsROS");
            cameraObject.transform.position = new Vector3(0f, 1.4f, -5.5f);
            cameraObject.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.54f, 0.66f, 0.78f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;

            var rgbSensor = cameraObject.AddComponent<RGBCameraSensor>();
            ConfigureRgbSensor(rgbSensor);

            var imagePublisher = cameraObject.AddComponent<ImageMsgPublisher>();
            ConfigureImagePublisher(imagePublisher, rgbSensor);

            var cameraInfoPublisher = cameraObject.AddComponent<CameraInfoMsgPublisher>();
            ConfigureCameraInfoPublisher(cameraInfoPublisher, rgbSensor);

            cameraObject.AddComponent<VlnUnitySensorsImageSmokeTest>();
        }

        static void ConfigureRgbSensor(RGBCameraSensor sensor)
        {
            var serializedSensor = new SerializedObject(sensor);
            serializedSensor.FindProperty("_frequency").floatValue = FrequencyHz;
            serializedSensor.FindProperty("_resolution").vector2IntValue = Resolution;
            serializedSensor.FindProperty("_fov").floatValue = 60f;
            serializedSensor.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureImagePublisher(ImageMsgPublisher publisher, RGBCameraSensor sensor)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("_frequency").floatValue = FrequencyHz;
            serializedPublisher.FindProperty("_topicName").stringValue = ImageTopic;

            var serializer = serializedPublisher.FindProperty("_serializer");
            serializer.FindPropertyRelative("_source").objectReferenceValue = sensor;
            serializer.FindPropertyRelative("_sourceTexture").enumValueIndex = 0;
            serializer.FindPropertyRelative("_encoding").enumValueIndex = 0;

            var header = serializer.FindPropertyRelative("_header");
            header.FindPropertyRelative("_source").objectReferenceValue = sensor;
            header.FindPropertyRelative("_frame_id").stringValue = FrameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureCameraInfoPublisher(CameraInfoMsgPublisher publisher, RGBCameraSensor sensor)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("_frequency").floatValue = FrequencyHz;
            serializedPublisher.FindProperty("_topicName").stringValue = CameraInfoTopic;

            var serializer = serializedPublisher.FindProperty("_serializer");
            serializer.FindPropertyRelative("_source").objectReferenceValue = sensor;

            var header = serializer.FindPropertyRelative("_header");
            header.FindPropertyRelative("_source").objectReferenceValue = sensor;
            header.FindPropertyRelative("_frame_id").stringValue = FrameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
