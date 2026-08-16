using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySensors.DataType.LiDAR;
using UnitySensors.Sensor.Camera;
using UnitySensors.Sensor.LiDAR;
using VLN.ROS2;
using CameraInfoMsgPublisher = UnitySensors.ROS.Publisher.Camera.CameraInfoMsgPublisher;
using ImageMsgPublisher = UnitySensors.ROS.Publisher.Sensor.ImageMsgPublisher;
using LiDARPointCloud2MsgPublisher = UnitySensors.ROS.Publisher.Sensor.LiDARPointCloud2MsgPublisher;

namespace VLN.Editor
{
    public static class VlnOffroadTerrainProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNOffroadTerrainSmokeTest.unity";
        const string ImageTopic = "/vln/front/image_raw";
        const string CameraInfoTopic = "/vln/front/camera_info";
        const string PointCloudTopic = "/vln/lidar/points";
        const string CmdVelTopic = "/vln/cmd_vel";
        const string CameraFrameId = "front_camera_optical_frame";
        const string LidarFrameId = "lidar_link";
        const float FrequencyHz = 5f;
        const int PointsPerScan = 7200;
        const float MinRange = 0.4f;
        const float MaxRange = 45f;
        const float TerrainSize = 80f;
        const float TerrainHeight = 7f;
        const int HeightResolution = 129;
        static readonly Vector2Int CameraResolution = new(640, 480);

        [MenuItem("VLN/Build Offroad Terrain Smoke Scene")]
        public static void BuildOffroadTerrainScene()
        {
            VlnRos2ProjectSetup.ConfigureRos2();
            EnsureDirectories();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rosObject = new GameObject("ROSConnection");
            var ros = rosObject.AddComponent<ROSConnection>();
            VlnRos2ProjectSetup.ConfigureRosConnection(ros);

            CreateLighting();
            CreateTerrain();
            CreateOffroadProps();
            CreateViewerCamera();
            CreateSensorRig();

            var controller = new GameObject("VLN_OffroadTerrain_SmokeTestController");
            controller.AddComponent<VlnOffroadTerrainSmokeTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_OFFROAD_TERRAIN_SETUP saved scene at {ScenePath}");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/VLN/Scenes");
            Directory.CreateDirectory("Assets/VLN/Scripts");
            Directory.CreateDirectory("Assets/VLN/Materials");
            Directory.CreateDirectory("Assets/VLN/Terrain");
        }

        static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.40f);

            var lightObject = new GameObject("Offroad_DirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
        }

        static void CreateTerrain()
        {
            var meshPath = "Assets/VLN/Terrain/OffroadTerrainMesh.asset";
            DeleteAssetIfExists(meshPath);
            DeleteAssetIfExists("Assets/VLN/Terrain/OffroadTerrainData.asset");
            DeleteAssetIfExists("Assets/VLN/Terrain/OffroadDirtTerrainLayer.terrainlayer");
            DeleteAssetIfExists("Assets/VLN/Terrain/OffroadDirtTexture.asset");

            var mesh = BuildTerrainMesh();
            AssetDatabase.CreateAsset(mesh, meshPath);

            var terrainObject = new GameObject("OffroadTerrain_ProceduralMeshLowLoad");
            terrainObject.layer = 0;

            var meshFilter = terrainObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = terrainObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_Terrain.mat", new Color(0.33f, 0.29f, 0.20f));

            var meshCollider = terrainObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
        }

        static Mesh BuildTerrainMesh()
        {
            var vertices = new Vector3[HeightResolution * HeightResolution];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(HeightResolution - 1) * (HeightResolution - 1) * 6];

            for (int z = 0; z < HeightResolution; z++)
            {
                for (int x = 0; x < HeightResolution; x++)
                {
                    int index = z * HeightResolution + x;
                    float worldX = Mathf.Lerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, x / (float)(HeightResolution - 1));
                    float worldZ = Mathf.Lerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z / (float)(HeightResolution - 1));
                    vertices[index] = new Vector3(worldX, TerrainWorldY(worldX, worldZ), worldZ);
                    uvs[index] = new Vector2(x / (float)(HeightResolution - 1), z / (float)(HeightResolution - 1));
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < HeightResolution - 1; z++)
            {
                for (int x = 0; x < HeightResolution - 1; x++)
                {
                    int bottomLeft = z * HeightResolution + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + HeightResolution;
                    int topRight = topLeft + 1;

                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                }
            }

            var mesh = new Mesh
            {
                name = "OffroadTerrainMesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float NormalizedTerrainHeight(float x, float z)
        {
            float ridge = 0.034f * Mathf.Sin(0.24f * x + 0.31f * Mathf.Sin(0.12f * z));
            float roll = 0.025f * Mathf.Cos(0.19f * z - 0.11f * x);
            float longSlope = 0.032f * Mathf.InverseLerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z);
            float baseHeight = 0.17f + ridge + roll + longSlope;

            float roadBlend = Mathf.Clamp01(1f - Mathf.Abs(x) / 4.4f);
            roadBlend = roadBlend * roadBlend * (3f - 2f * roadBlend);
            float roadHeight = 0.175f + longSlope * 0.75f + 0.006f * Mathf.Sin(0.18f * z);
            return Mathf.Clamp01(Mathf.Lerp(baseHeight, roadHeight, roadBlend * 0.9f));
        }

        static float TerrainWorldY(float x, float z)
        {
            return NormalizedTerrainHeight(x, z) * TerrainHeight;
        }

        static void CreateOffroadProps()
        {
            var roadMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_DirtRoad.mat", new Color(0.42f, 0.31f, 0.18f));
            var rockMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_Rock.mat", new Color(0.31f, 0.30f, 0.29f));
            var trunkMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_TreeTrunk.mat", new Color(0.23f, 0.15f, 0.08f));
            var foliageMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_Foliage.mat", new Color(0.13f, 0.30f, 0.12f));
            var markerMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_Marker.mat", new Color(0.76f, 0.42f, 0.12f));

            for (int i = 0; i < 9; i++)
            {
                float z = -34f + i * 8.5f;
                float x = 0.9f * Mathf.Sin(i * 0.85f);
                CreateBox($"Offroad_DirtRoad_{i:00}", new Vector3(x, TerrainWorldY(x, z) + 0.035f, z), new Vector3(6.2f, 0.05f, 8.4f), roadMaterial);
            }

            CreateBox("Offroad_ShortRamp", new Vector3(-2.2f, TerrainWorldY(-2.2f, -4f) + 0.28f, -4f), new Vector3(5.0f, 0.35f, 5.2f), roadMaterial, Quaternion.Euler(-8f, 0f, 0f));
            CreateBox("Offroad_RightBarrier", new Vector3(5.8f, TerrainWorldY(5.8f, 10f) + 0.55f, 10f), new Vector3(0.55f, 1.1f, 5.5f), markerMaterial);
            CreateBox("Offroad_LeftBarrier", new Vector3(-6.0f, TerrainWorldY(-6.0f, 17f) + 0.55f, 17f), new Vector3(0.55f, 1.1f, 5.2f), markerMaterial);

            CreateRock("Offroad_Rock_A", new Vector3(-5.8f, 0f, -12f), new Vector3(1.6f, 0.85f, 1.2f), rockMaterial);
            CreateRock("Offroad_Rock_B", new Vector3(6.4f, 0f, -2f), new Vector3(1.2f, 0.65f, 1.7f), rockMaterial);
            CreateRock("Offroad_Rock_C", new Vector3(-8.5f, 0f, 12f), new Vector3(2.0f, 1.0f, 1.4f), rockMaterial);
            CreateRock("Offroad_Rock_D", new Vector3(8.2f, 0f, 24f), new Vector3(1.5f, 0.8f, 1.5f), rockMaterial);

            CreateTree("Offroad_Tree_01", new Vector3(-12f, 0f, -18f), trunkMaterial, foliageMaterial);
            CreateTree("Offroad_Tree_02", new Vector3(12f, 0f, -10f), trunkMaterial, foliageMaterial);
            CreateTree("Offroad_Tree_03", new Vector3(-13f, 0f, 8f), trunkMaterial, foliageMaterial);
            CreateTree("Offroad_Tree_04", new Vector3(13.5f, 0f, 18f), trunkMaterial, foliageMaterial);

            CreateBox("Offroad_DistantWall_Target", new Vector3(0f, TerrainWorldY(0f, 31f) + 1.15f, 31f), new Vector3(8f, 2.3f, 0.4f), markerMaterial);
        }

        static void CreateSensorRig()
        {
            var scanPattern = LoadVlp16ScanPattern();
            var sensorOrigin = new Vector3(0f, TerrainWorldY(0f, -24f), -24f);

            var rig = new GameObject("Offroad_SensorRig_StaticVehiclePlaceholder");
            rig.transform.position = sensorOrigin;

            var bodyMaterial = EnsureMaterial("Assets/VLN/Materials/Offroad_VehiclePlaceholder.mat", new Color(0.08f, 0.12f, 0.14f));
            CreateChildBox(rig.transform, "Offroad_VehiclePlaceholder_Body", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.55f, 2.8f), bodyMaterial);

            var cameraObject = new GameObject("Front_RGBCamera_UnitySensorsROS");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.35f, 0.35f);
            cameraObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 90f;

            var rgbSensor = cameraObject.AddComponent<RGBCameraSensor>();
            ConfigureRgbSensor(rgbSensor);

            var imagePublisher = cameraObject.AddComponent<ImageMsgPublisher>();
            ConfigureImagePublisher(imagePublisher, rgbSensor);

            var cameraInfoPublisher = cameraObject.AddComponent<CameraInfoMsgPublisher>();
            ConfigureCameraInfoPublisher(cameraInfoPublisher, rgbSensor);

            var lidarObject = new GameObject("VLP16_RaycastLiDAR_UnitySensorsROS");
            lidarObject.transform.SetParent(rig.transform, false);
            lidarObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            lidarObject.transform.localRotation = Quaternion.identity;

            var lidarModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lidarModel.name = "VLP16_VisualModel";
            lidarModel.transform.SetParent(lidarObject.transform, false);
            lidarModel.transform.localPosition = Vector3.zero;
            lidarModel.transform.localScale = new Vector3(0.32f, 0.13f, 0.32f);
            lidarModel.GetComponent<Renderer>().sharedMaterial = EnsureMaterial("Assets/VLN/Materials/Lidar_SensorBody.mat", new Color(0.08f, 0.1f, 0.12f));

            var lidarSensor = lidarObject.AddComponent<RaycastLiDARSensor>();
            ConfigureLidarSensor(lidarSensor, scanPattern);

            var pointCloudPublisher = lidarObject.AddComponent<LiDARPointCloud2MsgPublisher>();
            ConfigurePointCloudPublisher(pointCloudPublisher, lidarSensor);

            var tfPublisher = rig.AddComponent<VlnVehicleTfPublisher>();
            ConfigureTfPublisher(tfPublisher, cameraObject.transform, lidarObject.transform);
        }

        static void CreateViewerCamera()
        {
            var viewerObject = new GameObject("Offroad_ViewerCamera");
            viewerObject.transform.position = new Vector3(0f, TerrainWorldY(0f, -30f) + 8.5f, -39f);
            viewerObject.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            var camera = viewerObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.depth = -1f;

            var controller = viewerObject.AddComponent<VlnRuntimeMapCameraController>();
            controller.Configure("Offroad_SensorRig_StaticVehiclePlaceholder", new Vector3(0f, 1.1f, 0f), 2.0f, 90.0f);
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

        static void ConfigureRgbSensor(RGBCameraSensor sensor)
        {
            var serializedSensor = new SerializedObject(sensor);
            serializedSensor.FindProperty("_frequency").floatValue = FrequencyHz;
            serializedSensor.FindProperty("_resolution").vector2IntValue = CameraResolution;
            serializedSensor.FindProperty("_fov").floatValue = 68f;
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
            header.FindPropertyRelative("_frame_id").stringValue = CameraFrameId;

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
            header.FindPropertyRelative("_frame_id").stringValue = CameraFrameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
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
            header.FindPropertyRelative("_frame_id").stringValue = LidarFrameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureTfPublisher(VlnVehicleTfPublisher publisher, Transform cameraTransform, Transform lidarTransform)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("m_CameraTransform").objectReferenceValue = cameraTransform;
            serializedPublisher.FindProperty("m_LidarTransform").objectReferenceValue = lidarTransform;
            serializedPublisher.FindProperty("m_CmdVelTopic").stringValue = CmdVelTopic;
            serializedPublisher.FindProperty("m_TfFrequencyHz").floatValue = 10f;
            serializedPublisher.FindProperty("m_VehicleSpeedMetersPerSecond").floatValue = 1.4f;
            serializedPublisher.FindProperty("m_PathStartZ").floatValue = -24f;
            serializedPublisher.FindProperty("m_PathEndZ").floatValue = 24f;
            serializedPublisher.FindProperty("m_AutopilotUntilFirstCommand").boolValue = false;
            serializedPublisher.FindProperty("m_CommandTimeoutSeconds").floatValue = 0.75f;
            serializedPublisher.FindProperty("m_MaxLinearSpeedMetersPerSecond").floatValue = 2.0f;
            serializedPublisher.FindProperty("m_MaxAngularSpeedRadPerSecond").floatValue = 1.2f;
            serializedPublisher.FindProperty("m_EnableObstacleCollisionStop").boolValue = true;
            serializedPublisher.FindProperty("m_CollisionHalfExtents").vector3Value = new Vector3(0.62f, 0.42f, 0.95f);
            serializedPublisher.FindProperty("m_CollisionCenterHeight").floatValue = 0.52f;
            serializedPublisher.FindProperty("m_CollisionSkinMeters").floatValue = 0.05f;
            serializedPublisher.FindProperty("m_ObstacleLayerMask").intValue = ~0;
            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
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

        static void CreateBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            CreateBox(name, position, scale, material, Quaternion.identity);
        }

        static void CreateBox(string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.rotation = rotation;
            box.transform.localScale = scale;
            box.layer = 0;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void CreateChildBox(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void CreateRock(string name, Vector3 position, Vector3 scale, Material material)
        {
            position.y = TerrainWorldY(position.x, position.z) + scale.y * 0.5f;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = name;
            rock.transform.position = position;
            rock.transform.rotation = Quaternion.Euler(0f, position.x * 13f, position.z * 7f);
            rock.transform.localScale = scale;
            rock.layer = 0;
            rock.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void CreateTree(string name, Vector3 position, Material trunkMaterial, Material foliageMaterial)
        {
            float groundY = TerrainWorldY(position.x, position.z);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = name + "_Trunk";
            trunk.transform.position = new Vector3(position.x, groundY + 1.0f, position.z);
            trunk.transform.localScale = new Vector3(0.28f, 1.0f, 0.28f);
            trunk.layer = 0;
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = name + "_Crown";
            crown.transform.position = new Vector3(position.x, groundY + 2.4f, position.z);
            crown.transform.localScale = new Vector3(1.45f, 1.25f, 1.45f);
            crown.layer = 0;
            crown.GetComponent<Renderer>().sharedMaterial = foliageMaterial;
        }

        static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
