using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySensors.DataType.LiDAR;
using UnitySensors.Sensor.LiDAR;
using UnitySensors.ROS.Publisher.Sensor;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnUnitySensorsLidarProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/UnitySensorsLidarSmokeTest.unity";
        const string PointCloudTopic = "/vln/lidar/points";
        const string FrameId = "lidar_link";
        const float FrequencyHz = 5f;
        const int PointsPerScan = 7200;
        const float MinRange = 0.4f;
        const float MaxRange = 40f;

        public static void BuildLidarSmokeScene()
        {
            VlnRos2ProjectSetup.ConfigureRos2();
            EnsureDirectories();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rosObject = new GameObject("ROSConnection");
            var ros = rosObject.AddComponent<ROSConnection>();
            VlnRos2ProjectSetup.ConfigureRosConnection(ros);

            CreateLighting();
            CreateLidarTargets();
            CreateViewerCamera();
            CreateLidarRig();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_UNITYSENSORS_LIDAR_SETUP saved scene at {ScenePath}");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/VLN/Scenes");
            Directory.CreateDirectory("Assets/VLN/Scripts");
            Directory.CreateDirectory("Assets/VLN/Materials");
        }

        static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.36f);

            var lightObject = new GameObject("LidarSmokeTest_DirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
        }

        static void CreateLidarTargets()
        {
            var groundMaterial = EnsureMaterial("Assets/VLN/Materials/Lidar_Ground.mat", new Color(0.31f, 0.34f, 0.28f));
            var wallMaterial = EnsureMaterial("Assets/VLN/Materials/Lidar_Wall.mat", new Color(0.48f, 0.46f, 0.42f));
            var rockMaterial = EnsureMaterial("Assets/VLN/Materials/Lidar_Rock.mat", new Color(0.36f, 0.34f, 0.33f));
            var markerMaterial = EnsureMaterial("Assets/VLN/Materials/Lidar_Marker.mat", new Color(0.84f, 0.58f, 0.18f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "LidarSmokeTest_Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            CreateBox("LidarSmokeTest_FrontWall", new Vector3(0f, 1.5f, 12f), new Vector3(10f, 3f, 0.35f), wallMaterial);
            CreateBox("LidarSmokeTest_LeftObstacle", new Vector3(-4f, 1.0f, 5f), new Vector3(1.2f, 2.0f, 2.0f), rockMaterial);
            CreateBox("LidarSmokeTest_RightObstacle", new Vector3(4f, 0.8f, 7f), new Vector3(1.6f, 1.6f, 1.6f), markerMaterial);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "LidarSmokeTest_Ramp";
            ramp.transform.position = new Vector3(0f, 0.35f, -5f);
            ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
            ramp.transform.localScale = new Vector3(5f, 0.35f, 4f);
            ramp.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        }

        static void CreateBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
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
            var cameraObject = new GameObject("LidarSmokeTest_ViewerCamera");
            cameraObject.transform.position = new Vector3(0f, 7f, -13f);
            cameraObject.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.63f, 0.72f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
        }

        static void CreateLidarRig()
        {
            var scanPattern = LoadVlp16ScanPattern();

            var lidarObject = new GameObject("VLP16_RaycastLiDAR_UnitySensorsROS");
            lidarObject.transform.position = new Vector3(0f, 1.15f, 0f);
            lidarObject.transform.rotation = Quaternion.identity;

            var model = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            model.name = "VLP16_VisualModel";
            model.transform.SetParent(lidarObject.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = new Vector3(0.35f, 0.15f, 0.35f);
            model.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Assets/VLN/Materials/Lidar_SensorBody.mat", new Color(0.08f, 0.1f, 0.12f));

            var lidarSensor = lidarObject.AddComponent<RaycastLiDARSensor>();
            ConfigureLidarSensor(lidarSensor, scanPattern);

            var pointCloudPublisher = lidarObject.AddComponent<LiDARPointCloud2MsgPublisher>();
            ConfigurePointCloudPublisher(pointCloudPublisher, lidarSensor);

            lidarObject.AddComponent<VlnUnitySensorsLidarSmokeTest>();
        }

        static ScanPattern LoadVlp16ScanPattern()
        {
            const string vlp16Guid = "f0221c83205fa634c8ecd626305d9072";
            string path = AssetDatabase.GUIDToAssetPath(vlp16Guid);
            var scanPattern = AssetDatabase.LoadAssetAtPath<ScanPattern>(path);
            if (scanPattern == null)
            {
                throw new FileNotFoundException($"未找到 UnitySensors VLP-16 scan pattern，GUID={vlp16Guid}，path={path}");
            }

            return scanPattern;
        }

        static void ConfigureLidarSensor(RaycastLiDARSensor sensor, ScanPattern scanPattern)
        {
            var serializedSensor = new SerializedObject(sensor);
            serializedSensor.FindProperty("_frequency").floatValue = FrequencyHz;
            serializedSensor.FindProperty("_scanPattern").objectReferenceValue = scanPattern;
            serializedSensor.FindProperty("_pointsNumPerScan").intValue = PointsPerScan;
            serializedSensor.FindProperty("_minRange").floatValue = MinRange;
            serializedSensor.FindProperty("_maxRange").floatValue = MaxRange;
            serializedSensor.FindProperty("_gaussianNoiseSigma").floatValue = 0.0f;
            serializedSensor.FindProperty("_maxIntensity").floatValue = 255.0f;
            serializedSensor.FindProperty("_raycastLayerMask").intValue = 1;
            serializedSensor.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePointCloudPublisher(LiDARPointCloud2MsgPublisher publisher, RaycastLiDARSensor sensor)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("_frequency").floatValue = FrequencyHz;
            serializedPublisher.FindProperty("_topicName").stringValue = PointCloudTopic;
            serializedPublisher.FindProperty("_source").objectReferenceValue = sensor;

            var serializer = serializedPublisher.FindProperty("_serializer");
            var header = serializer.FindPropertyRelative("_header");
            header.FindPropertyRelative("_source").objectReferenceValue = sensor;
            header.FindPropertyRelative("_frame_id").stringValue = FrameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
