using System;
using System.Collections.Generic;
using System.IO;
using Unity.Robotics.UrdfImporter;
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
    public static class VlnOffroadScoutWheelGroundCandidateProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity";
        const string UrdfAssetPath = "Assets/VLN/ExternalAssets/ScoutUrdfPhysics/scout_v2_unity_import.urdf";
        const string ScoutAssetRoot = "Assets/VLN/ExternalAssets/ScoutUrdfPhysics";
        const string TopgearVisualAssetRoot = "Assets/VLN/ExternalAssets/TopgearV2Visual";
        const string TopgearVisualAssetPath = TopgearVisualAssetRoot + "/Models/topgear_v2.dae";
        const string TopgearSensorModelAssetRoot = "Assets/VLN/ExternalAssets/TopgearSensorModels";
        const string Vlp16Base1AssetPath = TopgearSensorModelAssetRoot + "/VelodyneVLP16/VLP16_base_1.dae";
        const string Vlp16Base2AssetPath = TopgearSensorModelAssetRoot + "/VelodyneVLP16/VLP16_base_2.dae";
        const string Vlp16ScanAssetPath = TopgearSensorModelAssetRoot + "/VelodyneVLP16/VLP16_scan.dae";
        const string RealSenseD405AssetPath = TopgearSensorModelAssetRoot + "/RealSenseD405/d405.stl";
        const string TopgearVisualRootName = "ScoutWheelGround_TopgearV2Visual";
        const string TopgearSensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        const string TopgearLidarName = "Topgear_VLP16_RaycastLiDAR_UnitySensorsROS";
        const string TopgearFrontCameraName = "Topgear_Front_RGBCamera_UnitySensorsROS";
        const string TopgearRearCameraName = "Topgear_Rear_RGBCamera_UnitySensorsROS";
        const string TopgearLeftCameraName = "Topgear_Left_RGBCamera_UnitySensorsROS";
        const string TopgearRightCameraName = "Topgear_Right_RGBCamera_UnitySensorsROS";
        const string FrontImageTopic = "/vln/front/image_raw";
        const string FrontCameraInfoTopic = "/vln/front/camera_info";
        const string RearImageTopic = "/vln/rear/image_raw";
        const string RearCameraInfoTopic = "/vln/rear/camera_info";
        const string LeftImageTopic = "/vln/left/image_raw";
        const string LeftCameraInfoTopic = "/vln/left/camera_info";
        const string RightImageTopic = "/vln/right/image_raw";
        const string RightCameraInfoTopic = "/vln/right/camera_info";
        const string PointCloudTopic = "/vln/lidar/points";
        const string FrontCameraFrameId = "front_camera_optical_frame";
        const string RearCameraFrameId = "rear_camera_optical_frame";
        const string LeftCameraFrameId = "left_camera_optical_frame";
        const string RightCameraFrameId = "right_camera_optical_frame";
        const string LidarFrameId = "lidar_link";
        const float SensorFrequencyHz = 5f;
        const int LidarPointsPerScan = 7200;
        const float LidarMinRange = 0.4f;
        const float LidarMaxRange = 45f;
        static readonly Vector2Int TopgearCameraResolution = new(640, 480);
        const string PbrMaterialRoot = "Assets/VLN/ExternalAssets/PBRMaterials/AmbientCG";
        const string StoneAlbedoTexturePath = PbrMaterialRoot + "/PavingStones151_1K-JPG/PavingStones151_1K-JPG_Color.jpg";
        const string StoneNormalTexturePath = PbrMaterialRoot + "/PavingStones151_1K-JPG/PavingStones151_1K-JPG_NormalGL.jpg";
        const string StoneOcclusionTexturePath = PbrMaterialRoot + "/PavingStones151_1K-JPG/PavingStones151_1K-JPG_AmbientOcclusion.jpg";
        const string StoneRoughnessTexturePath = PbrMaterialRoot + "/PavingStones151_1K-JPG/PavingStones151_1K-JPG_Roughness.jpg";
        const string SandAlbedoTexturePath = PbrMaterialRoot + "/Ground054_1K-JPG/Ground054_1K-JPG_Color.jpg";
        const string SandNormalTexturePath = PbrMaterialRoot + "/Ground054_1K-JPG/Ground054_1K-JPG_NormalGL.jpg";
        const string SandOcclusionTexturePath = PbrMaterialRoot + "/Ground054_1K-JPG/Ground054_1K-JPG_AmbientOcclusion.jpg";
        const string SandRoughnessTexturePath = PbrMaterialRoot + "/Ground054_1K-JPG/Ground054_1K-JPG_Roughness.jpg";
        const string RigName = "Offroad_SensorRig_StaticVehiclePlaceholder";
        public const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        public const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string FrictionMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_HighFriction.physicMaterial";
        const string RoadSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleRoadPhysics.mat";
        const string BridgeSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleBridgePhysics.mat";
        const string BridgeDetailMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleBridgeDetail.mat";
        const string RampDetailMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleRampDetail.mat";
        const string ChallengeGrassSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeGrassSurface.mat";
        const string ChallengeGrassBladeMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeGrassBlade.mat";
        const string ChallengeGrassBladeLightMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeGrassBladeLight.mat";
        const string ChallengeGrassBladeDryMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeGrassBladeDry.mat";
        const string ChallengeGrassSoilMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeGrassSoil.mat";
        const string ChallengeStoneSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeStoneSurface.mat";
        const string ChallengeStonePaverMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeStonePaver.mat";
        const string ChallengeStoneSeamMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeStoneSeam.mat";
        const string ChallengeStoneChipMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeStoneChip.mat";
        const string ChallengeSandSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeSandSurface.mat";
        const string ChallengeSandRippleMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeSandRipple.mat";
        const string ChallengeSandDepressionMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeSandDepression.mat";
        const string ChallengeSandGrainMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeSandGrain.mat";
        const string ChallengeObstacleMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeObstacle.mat";
        const string ChallengeMarkerMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeMarker.mat";
        const string ChallengeGrassBladeMeshPath = "Assets/VLN/Terrain/ScoutWheelGround_ChallengeGrassBladeField.asset";
        const string ChallengeGrassBladeLightMeshPath = "Assets/VLN/Terrain/ScoutWheelGround_ChallengeGrassBladeField_Light.asset";
        const string ChallengeGrassBladeDryMeshPath = "Assets/VLN/Terrain/ScoutWheelGround_ChallengeGrassBladeField_Dry.asset";
        const string ChallengeStoneChipMeshPath = "Assets/VLN/Terrain/ScoutWheelGround_ChallengeStoneChipField.asset";
        const string ChallengeSandGrainMeshPath = "Assets/VLN/Terrain/ScoutWheelGround_ChallengeSandGrainField.asset";
        const string ChallengeGrassPhysicMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeGrass.physicMaterial";
        const string ChallengeStonePhysicMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeStone.physicMaterial";
        const string ChallengeSandPhysicMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeSand.physicMaterial";
        const string ChallengeObstaclePhysicMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_ChallengeObstacle.physicMaterial";

        const float TerrainSize = 80f;
        const float TerrainHeight = 7f;
        const float WheelRadius = 0.16459f;
        const float WheelBase = 0.498f;
        const float Track = 0.58306f;
        const float InitialGroundClearance = 0f;
        const float PhysicalRoadWidth = 6.2f;
        const float PhysicalBridgeWidth = 2.25f;
        const float BridgeZoneZMin = -13.0f;
        const float BridgeZoneZMax = -1.7f;
        const float ShortRampExclusionZMin = -0.8f;
        const float ShortRampExclusionZMax = 7.2f;

        [MenuItem("VLN/Build Offroad Scout Wheel-Ground Candidate Scene")]
        public static void BuildScoutWheelGroundCandidateScene()
        {
            BackupExistingCandidateSceneBeforeRebuild();
            EnsureScoutAssetsExist();
            VlnOffroadAssetCandidateProjectSetup.BuildAssetCandidateScene();
            AssetDatabase.ImportAsset(ScoutAssetRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TopgearVisualAssetRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TopgearSensorModelAssetRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            ImportPbrTextureFolderIfPresent();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(VlnOffroadAssetCandidateProjectSetup.ScenePath, OpenSceneMode.Single);
            RemoveIfExists("VLN_OffroadTerrain_SmokeTestController");
            RemoveIfExists("VLN_OffroadAssetCandidate_SmokeTestController");
            RemoveIfExists("VLN_OffroadVehicleCandidate_SmokeTestController");
            RemoveIfExists("VLN_OffroadScoutUrdfCandidate_SmokeTestController");
            RemoveIfExists("VLN_OffroadScoutWheelGroundCandidate_SmokeTestController");
            RemoveIfExists(PhysicsRootName);
            RemoveIfExists("scout_v2");

            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                throw new InvalidOperationException($"Missing sensor rig: {RigName}");
            }

            DestroyChildIfExists(rig.transform, "Offroad_VehiclePlaceholder_Body");
            DestroyChildIfExists(rig.transform, "HuskyVisual_Root");
            DestroyChildIfExists(rig.transform, VlnOffroadScoutUrdfCandidateProjectSetup.ScoutRootName);

            var frictionMaterial = EnsureHighFrictionMaterial();
            var roadSurfaceMaterial = EnsureSurfaceMaterial(RoadSurfaceMaterialPath, new Color(0.45f, 0.32f, 0.18f));
            var bridgeSurfaceMaterial = EnsureSurfaceMaterial(BridgeSurfaceMaterialPath, new Color(0.36f, 0.25f, 0.15f));
            var bridgeDetailMaterial = EnsureSurfaceMaterial(BridgeDetailMaterialPath, new Color(0.18f, 0.11f, 0.06f));
            var rampDetailMaterial = EnsureSurfaceMaterial(RampDetailMaterialPath, new Color(0.26f, 0.18f, 0.10f));
            var challengeGrassMaterial = EnsureSurfaceMaterial(ChallengeGrassSurfaceMaterialPath, new Color(0.23f, 0.36f, 0.19f));
            var challengeGrassBladeMaterial = EnsureSurfaceMaterial(ChallengeGrassBladeMaterialPath, new Color(0.10f, 0.34f, 0.10f));
            var challengeGrassBladeLightMaterial = EnsureSurfaceMaterial(ChallengeGrassBladeLightMaterialPath, new Color(0.20f, 0.48f, 0.15f));
            var challengeGrassBladeDryMaterial = EnsureSurfaceMaterial(ChallengeGrassBladeDryMaterialPath, new Color(0.42f, 0.39f, 0.18f));
            var challengeGrassSoilMaterial = EnsureSurfaceMaterial(ChallengeGrassSoilMaterialPath, new Color(0.24f, 0.18f, 0.10f));
            var challengeStoneMaterial = EnsureSurfaceMaterial(ChallengeStoneSurfaceMaterialPath, new Color(0.31f, 0.36f, 0.36f));
            var challengeStonePaverMaterial = EnsureSurfaceMaterial(ChallengeStonePaverMaterialPath, new Color(0.42f, 0.50f, 0.50f));
            var challengeStoneSeamMaterial = EnsureSurfaceMaterial(ChallengeStoneSeamMaterialPath, new Color(0.12f, 0.14f, 0.14f));
            var challengeStoneChipMaterial = EnsureSurfaceMaterial(ChallengeStoneChipMaterialPath, new Color(0.25f, 0.30f, 0.30f));
            var challengeSandMaterial = EnsureSurfaceMaterial(ChallengeSandSurfaceMaterialPath, new Color(0.66f, 0.56f, 0.34f));
            var challengeSandRippleMaterial = EnsureSurfaceMaterial(ChallengeSandRippleMaterialPath, new Color(0.78f, 0.69f, 0.45f));
            var challengeSandDepressionMaterial = EnsureSurfaceMaterial(ChallengeSandDepressionMaterialPath, new Color(0.47f, 0.39f, 0.24f));
            var challengeSandGrainMaterial = EnsureSurfaceMaterial(ChallengeSandGrainMaterialPath, new Color(0.88f, 0.78f, 0.50f));
            var challengeObstacleMaterial = EnsureSurfaceMaterial(ChallengeObstacleMaterialPath, new Color(0.28f, 0.25f, 0.21f));
            var challengeMarkerMaterial = EnsureSurfaceMaterial(ChallengeMarkerMaterialPath, new Color(0.76f, 0.43f, 0.12f));
            ConfigureChallengePbrMaterials(
                challengeStoneMaterial,
                challengeStonePaverMaterial,
                challengeStoneChipMaterial,
                challengeSandMaterial,
                challengeSandRippleMaterial,
                challengeSandDepressionMaterial,
                challengeSandGrainMaterial);
            var challengeGrassPhysic = EnsurePhysicMaterial(ChallengeGrassPhysicMaterialPath, "ScoutWheelGround_ChallengeGrass", 1.05f, 0.92f, PhysicMaterialCombine.Maximum);
            var challengeStonePhysic = EnsurePhysicMaterial(ChallengeStonePhysicMaterialPath, "ScoutWheelGround_ChallengeStone", 1.08f, 0.96f, PhysicMaterialCombine.Maximum);
            var challengeSandPhysic = EnsurePhysicMaterial(ChallengeSandPhysicMaterialPath, "ScoutWheelGround_ChallengeSand", 0.78f, 0.64f, PhysicMaterialCombine.Average);
            var challengeObstaclePhysic = EnsurePhysicMaterial(ChallengeObstaclePhysicMaterialPath, "ScoutWheelGround_ChallengeObstacle", 1.02f, 0.90f, PhysicMaterialCombine.Maximum);
            ReplaceDecorativeTrailCollidersWithLocalizedPhysics(frictionMaterial, roadSurfaceMaterial, rampDetailMaterial);
            RemoveDecorativeBridgeObjects();
            CreateDetailedPhysicalBridge(frictionMaterial, bridgeSurfaceMaterial, bridgeDetailMaterial);
            AssignSceneFrictionMaterial(frictionMaterial);
            CreatePostRampChallengeCourse(
                challengeGrassMaterial,
                challengeGrassBladeMaterial,
                challengeGrassBladeLightMaterial,
                challengeGrassBladeDryMaterial,
                challengeGrassSoilMaterial,
                challengeStoneMaterial,
                challengeStonePaverMaterial,
                challengeStoneSeamMaterial,
                challengeStoneChipMaterial,
                challengeSandMaterial,
                challengeSandRippleMaterial,
                challengeSandDepressionMaterial,
                challengeSandGrainMaterial,
                challengeObstacleMaterial,
                challengeMarkerMaterial,
                challengeGrassPhysic,
                challengeStonePhysic,
                challengeSandPhysic,
                challengeObstaclePhysic);
            var physicsRoot = CreatePhysicalScoutRoot(rig.transform.position, frictionMaterial);
            var visualRoot = ImportScoutVisualOnly(physicsRoot.transform);
            ConfigureController(physicsRoot, visualRoot);
            ConfigureRigToFollowPhysics(rig, physicsRoot.transform);
            var topgearSensors = CreateTopgearSensorSuite(rig.transform);
            ConfigureTopgearSensorTf(rig, topgearSensors);
            ConfigureScoutWheelGroundCamera(rig.transform);

            var controller = new GameObject("VLN_OffroadScoutWheelGroundCandidate_SmokeTestController");
            controller.AddComponent<VlnOffroadScoutWheelGroundCandidateSmokeTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_SETUP saved scene at {ScenePath}");
        }

        static void EnsureScoutAssetsExist()
        {
            string[] requiredAssets =
            {
                UrdfAssetPath,
                "Assets/VLN/ExternalAssets/ScoutUrdfPhysics/meshes/base_link.dae",
                "Assets/VLN/ExternalAssets/ScoutUrdfPhysics/meshes/wheel_type1.dae",
                TopgearVisualAssetPath,
                Vlp16Base1AssetPath,
                Vlp16Base2AssetPath,
                Vlp16ScanAssetPath,
                RealSenseD405AssetPath,
            };

            foreach (string assetPath in requiredAssets)
            {
                string fullPath = ProjectRelativeToFullPath(assetPath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Missing Scout URDF asset: {assetPath}", fullPath);
                }
            }
        }

        static GameObject CreatePhysicalScoutRoot(Vector3 rigPosition, PhysicMaterial material)
        {
            float groundY = TerrainWorldY(rigPosition.x, rigPosition.z);
            var root = new GameObject(PhysicsRootName);
            root.transform.position = new Vector3(rigPosition.x, groundY + InitialGroundClearance, rigPosition.z);
            root.transform.rotation = Quaternion.identity;
            root.layer = 0;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 52f;
            body.centerOfMass = new Vector3(0f, 0.24f, 0.02f);
            body.drag = 0.12f;
            body.angularDrag = 0.55f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var chassis = root.AddComponent<BoxCollider>();
            chassis.center = new Vector3(0f, 0.45f, 0f);
            chassis.size = new Vector3(0.66f, 0.18f, 0.82f);
            chassis.material = material;

            CreateWheelCollider(root.transform, "front_left_wheel_collider", -Track * 0.5f, WheelBase * 0.5f);
            CreateWheelCollider(root.transform, "front_right_wheel_collider", Track * 0.5f, WheelBase * 0.5f);
            CreateWheelCollider(root.transform, "rear_left_wheel_collider", -Track * 0.5f, -WheelBase * 0.5f);
            CreateWheelCollider(root.transform, "rear_right_wheel_collider", Track * 0.5f, -WheelBase * 0.5f);

            return root;
        }

        static WheelCollider CreateWheelCollider(Transform parent, string name, float localX, float localZ)
        {
            var wheelObject = new GameObject(name);
            wheelObject.transform.SetParent(parent, false);
            wheelObject.transform.localPosition = new Vector3(localX, WheelRadius + 0.060f, localZ);
            wheelObject.transform.localRotation = Quaternion.identity;
            wheelObject.layer = 0;

            var wheel = wheelObject.AddComponent<WheelCollider>();
            wheel.radius = WheelRadius;
            wheel.mass = 3f;
            wheel.wheelDampingRate = 0.55f;
            wheel.suspensionDistance = 0.18f;
            wheel.forceAppPointDistance = 0.02f;

            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 26000f;
            spring.damper = 4600f;
            spring.targetPosition = 0.58f;
            wheel.suspensionSpring = spring;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.extremumSlip = 0.45f;
            forward.extremumValue = 2.8f;
            forward.asymptoteSlip = 1.20f;
            forward.asymptoteValue = 2.0f;
            forward.stiffness = 8.50f;
            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.extremumSlip = 0.28f;
            sideways.extremumValue = 1.40f;
            sideways.asymptoteSlip = 0.70f;
            sideways.asymptoteValue = 1.00f;
            sideways.stiffness = 2.10f;
            wheel.sidewaysFriction = sideways;

            wheel.ConfigureVehicleSubsteps(5f, 12, 15);

            return wheel;
        }

        static GameObject ImportScoutVisualOnly(Transform physicsRoot)
        {
            GameObject imported = ImportScoutUrdf();
            imported.name = VisualRootName;
            imported.transform.SetParent(physicsRoot, false);
            imported.transform.localPosition = Vector3.zero;
            imported.transform.localRotation = Quaternion.identity;
            imported.transform.localScale = Vector3.one;

            RemovePhysicsComponents(imported);
            AlignVisualBottomToLocalY(imported, 0.02f);
            AttachTopgearV2Visual(imported.transform);
            SetLayerRecursively(imported, 0);
            Debug.Log(BuildVisualImportSummary(imported));
            return imported;
        }

        static void AttachTopgearV2Visual(Transform visualRoot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TopgearVisualAssetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException($"Missing Topgear V2 visual asset: {TopgearVisualAssetPath}", ProjectRelativeToFullPath(TopgearVisualAssetPath));
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

            instance.name = TopgearVisualRootName;
            instance.transform.SetParent(visualRoot, false);
            instance.transform.localPosition = Vector3.zero;
            // topgear_v2.dae is authored as Blender Z-up with +Y as the visual front.
            // Mount it on the Scout visual frame where +Y is up and +Z is vehicle-forward.
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            instance.transform.localScale = Vector3.one;
            RemovePhysicsComponents(instance);
            // Keep the topgear assembly visually seated on the Scout top deck.
            AlignRendererBoundsToLocalFrame(instance, visualRoot, new Vector3(0f, 0.115f, 0.045f));

            Debug.Log(BuildTopgearVisualSummary(instance, visualRoot));
        }

        static void AlignRendererBoundsToLocalFrame(GameObject root, Transform localFrame, Vector3 targetLocalCenterXBottomYCenterZ)
        {
            Bounds localBounds = CalculateRendererBoundsInLocalFrame(root, localFrame);
            var localDelta = new Vector3(
                targetLocalCenterXBottomYCenterZ.x - localBounds.center.x,
                targetLocalCenterXBottomYCenterZ.y - localBounds.min.y,
                targetLocalCenterXBottomYCenterZ.z - localBounds.center.z);
            root.transform.localPosition += localDelta;
        }

        static string BuildTopgearVisualSummary(GameObject topgearRoot, Transform localFrame)
        {
            Bounds localBounds = CalculateRendererBoundsInLocalFrame(topgearRoot, localFrame);
            return string.Format(
                "VLN_SCOUT_WHEEL_GROUND_TOPGEAR_VISUAL_ATTACHED renderers={0} colliders={1} rigidbodies={2} local_bounds_min={3:F3},{4:F3},{5:F3} local_bounds_size={6:F3},{7:F3},{8:F3}",
                topgearRoot.GetComponentsInChildren<Renderer>(true).Length,
                topgearRoot.GetComponentsInChildren<Collider>(true).Length,
                topgearRoot.GetComponentsInChildren<Rigidbody>(true).Length,
                localBounds.min.x,
                localBounds.min.y,
                localBounds.min.z,
                localBounds.size.x,
                localBounds.size.y,
                localBounds.size.z);
        }

        static GameObject ImportScoutUrdf()
        {
            string fullUrdfPath = ProjectRelativeToFullPath(UrdfAssetPath);
            var settings = ImportSettings.DefaultSettings();
            settings.chosenAxis = ImportSettings.axisType.yAxis;
            settings.convexMethod = ImportSettings.convexDecomposer.unity;
            settings.OverwriteExistingPrefabs = true;

            Selection.activeObject = null;
            IEnumerator<GameObject> import = UrdfRobotExtensions.Create(fullUrdfPath, settings, loadStatus: false, forceRuntimeMode: false);
            GameObject imported = null;
            while (import.MoveNext())
            {
                if (import.Current != null)
                {
                    imported = import.Current;
                }
            }

            if (imported == null)
            {
                imported = GameObject.Find("scout_v2");
            }

            if (imported == null)
            {
                throw new InvalidOperationException("URDF Importer did not create a Scout robot GameObject.");
            }

            return imported;
        }

        static void RemovePhysicsComponents(GameObject root)
        {
            foreach (var controller in root.GetComponentsInChildren<Unity.Robotics.UrdfImporter.Control.Controller>(true))
            {
                UnityEngine.Object.DestroyImmediate(controller);
            }

            foreach (var joint in root.GetComponentsInChildren<UrdfJoint>(true))
            {
                UnityEngine.Object.DestroyImmediate(joint);
            }

            foreach (var inertial in root.GetComponentsInChildren<UrdfInertial>(true))
            {
                UnityEngine.Object.DestroyImmediate(inertial);
            }

            foreach (var collision in root.GetComponentsInChildren<UrdfCollision>(true))
            {
                UnityEngine.Object.DestroyImmediate(collision);
            }

            foreach (var visual in root.GetComponentsInChildren<UrdfVisual>(true))
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }

            foreach (var link in root.GetComponentsInChildren<UrdfLink>(true))
            {
                UnityEngine.Object.DestroyImmediate(link);
            }

            foreach (var robot in root.GetComponentsInChildren<UrdfRobot>(true))
            {
                UnityEngine.Object.DestroyImmediate(robot);
            }

            foreach (var body in root.GetComponentsInChildren<ArticulationBody>(true))
            {
                UnityEngine.Object.DestroyImmediate(body);
            }

            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                UnityEngine.Object.DestroyImmediate(body);
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        static void ConfigureController(GameObject physicsRoot, GameObject visualRoot)
        {
            var controller = physicsRoot.AddComponent<VlnScoutWheelGroundController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("m_FrontLeftWheel").objectReferenceValue = FindWheelCollider(physicsRoot, "front_left_wheel_collider");
            serializedController.FindProperty("m_FrontRightWheel").objectReferenceValue = FindWheelCollider(physicsRoot, "front_right_wheel_collider");
            serializedController.FindProperty("m_RearLeftWheel").objectReferenceValue = FindWheelCollider(physicsRoot, "rear_left_wheel_collider");
            serializedController.FindProperty("m_RearRightWheel").objectReferenceValue = FindWheelCollider(physicsRoot, "rear_right_wheel_collider");
            serializedController.FindProperty("m_FrontLeftVisual").objectReferenceValue = FindDeepChild(visualRoot.transform, "front_left_wheel_link");
            serializedController.FindProperty("m_FrontRightVisual").objectReferenceValue = FindDeepChild(visualRoot.transform, "front_right_wheel_link");
            serializedController.FindProperty("m_RearLeftVisual").objectReferenceValue = FindDeepChild(visualRoot.transform, "rear_left_wheel_link");
            serializedController.FindProperty("m_RearRightVisual").objectReferenceValue = FindDeepChild(visualRoot.transform, "rear_right_wheel_link");
            serializedController.FindProperty("m_WheelRadiusMeters").floatValue = WheelRadius;
            serializedController.FindProperty("m_TrackMeters").floatValue = Track;
            serializedController.FindProperty("m_WheelMotorDirection").floatValue = 1f;
            serializedController.FindProperty("m_WheelYawDirection").floatValue = 1f;
            serializedController.FindProperty("m_WheelLinearMotorScale").floatValue = 0f;
            serializedController.FindProperty("m_WheelAngularMotorScale").floatValue = 0f;
            serializedController.FindProperty("m_WheelVisualVerticalOffset").floatValue = 0.085f;
            serializedController.FindProperty("m_WheelVisualForwardRollDirection").floatValue = 1f;
            serializedController.FindProperty("m_WheelVisualAngularSmoothing").floatValue = 14f;
            serializedController.FindProperty("m_MaxLinearSpeedMetersPerSecond").floatValue = 20.0f;
            serializedController.FindProperty("m_MaxAngularSpeedRadPerSecond").floatValue = 1.0f;
            serializedController.FindProperty("m_MaxMotorTorque").floatValue = 160f;
            serializedController.FindProperty("m_MaxBrakeTorque").floatValue = 220f;
            serializedController.FindProperty("m_RpmVelocityGain").floatValue = 0.90f;
            serializedController.FindProperty("m_LongitudinalAssistGain").floatValue = 3.00f;
            serializedController.FindProperty("m_MaxLongitudinalAssistAcceleration").floatValue = 4.00f;
            serializedController.FindProperty("m_LongitudinalVelocityKp").floatValue = 3.20f;
            serializedController.FindProperty("m_LongitudinalVelocityKi").floatValue = 0.12f;
            serializedController.FindProperty("m_LongitudinalVelocityKd").floatValue = 0.45f;
            serializedController.FindProperty("m_LongitudinalIntegralLimit").floatValue = 1.20f;
            serializedController.FindProperty("m_YawAssistGain").floatValue = 3.0f;
            serializedController.FindProperty("m_MaxYawAssistAngularAcceleration").floatValue = 5.0f;
            serializedController.FindProperty("m_YawRateKp").floatValue = 8.50f;
            serializedController.FindProperty("m_YawRateKi").floatValue = 0.08f;
            serializedController.FindProperty("m_YawRateKd").floatValue = 0.55f;
            serializedController.FindProperty("m_YawRateIntegralLimit").floatValue = 0.70f;
            serializedController.FindProperty("m_EnableStraightHeadingHold").boolValue = true;
            serializedController.FindProperty("m_StraightHeadingHoldKp").floatValue = 4.20f;
            serializedController.FindProperty("m_StraightHeadingHoldKd").floatValue = 1.80f;
            serializedController.FindProperty("m_MaxStraightHeadingHoldAngularAcceleration").floatValue = 3.50f;
            serializedController.FindProperty("m_LateralDampingGain").floatValue = 9.0f;
            serializedController.FindProperty("m_MaxLateralDampingAcceleration").floatValue = 8.0f;
            serializedController.FindProperty("m_StopVelocityDampingGain").floatValue = 7.0f;
            serializedController.FindProperty("m_MaxStopDampingAcceleration").floatValue = 6.0f;
            serializedController.FindProperty("m_StopYawDampingGain").floatValue = 36.0f;
            serializedController.FindProperty("m_DirectStopVelocityDampingGain").floatValue = 10.0f;
            serializedController.FindProperty("m_DirectStopYawDampingGain").floatValue = 60.0f;
            serializedController.FindProperty("m_PureTurnTranslationDampingGain").floatValue = 14.0f;
            serializedController.FindProperty("m_MaxPureTurnDampingAcceleration").floatValue = 9.0f;
            serializedController.FindProperty("m_LongitudinalOverspeedMargin").floatValue = 0.35f;
            serializedController.FindProperty("m_OverspeedBrakeTorqueRatio").floatValue = 0.25f;
            serializedController.FindProperty("m_RollingBrakeSpeedThreshold").floatValue = 0.08f;
            serializedController.FindProperty("m_CommandTimeoutSeconds").floatValue = 0.18f;
            serializedController.FindProperty("m_GrassRollingResistanceAcceleration").floatValue = 0.12f;
            serializedController.FindProperty("m_StoneRollingResistanceAcceleration").floatValue = 0.03f;
            serializedController.FindProperty("m_SandRollingResistanceAcceleration").floatValue = 0.32f;
            serializedController.FindProperty("m_GrassTractionAssistReduction").floatValue = 0.05f;
            serializedController.FindProperty("m_SandTractionAssistReduction").floatValue = 0.16f;
            serializedController.FindProperty("m_SandLateralDampingReduction").floatValue = 0.20f;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureRigToFollowPhysics(GameObject rig, Transform physicsRoot)
        {
            rig.transform.SetPositionAndRotation(physicsRoot.position, physicsRoot.rotation);

            var tfPublisher = rig.GetComponent<VlnVehicleTfPublisher>();
            if (tfPublisher != null)
            {
                var serializedPublisher = new SerializedObject(tfPublisher);
                serializedPublisher.FindProperty("m_EnableKinematicMotion").boolValue = false;
                serializedPublisher.FindProperty("m_EnableObstacleCollisionStop").boolValue = false;
                serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
            }

            var odomPublisher = rig.GetComponent<VlnOdomPublisher>();
            if (odomPublisher == null)
            {
                odomPublisher = rig.AddComponent<VlnOdomPublisher>();
            }
            odomPublisher.enabled = true;

            var follower = rig.GetComponent<VlnFollowTransformPose>();
            if (follower == null)
            {
                follower = rig.AddComponent<VlnFollowTransformPose>();
            }
            follower.Configure(physicsRoot, Vector3.zero, true);
        }

        sealed class TopgearSensorSuiteHandles
        {
            public Transform Root;
            public Transform FrontCamera;
            public Transform RearCamera;
            public Transform LeftCamera;
            public Transform RightCamera;
            public Transform Lidar;
        }

        static TopgearSensorSuiteHandles CreateTopgearSensorSuite(Transform rig)
        {
            DestroyChildIfExists(rig, "Front_RGBCamera_UnitySensorsROS");
            DestroyChildIfExists(rig, "VLP16_RaycastLiDAR_UnitySensorsROS");
            DestroyChildIfExists(rig, TopgearSensorRootName);

            var root = new GameObject(TopgearSensorRootName);
            root.transform.SetParent(rig, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var bodyMaterial = EnsureSurfaceMaterial("Assets/VLN/Materials/TopgearSensorSuite_DarkBody.mat", new Color(0.155f, 0.165f, 0.170f));
            var ringMaterial = EnsureSurfaceMaterial("Assets/VLN/Materials/TopgearSensorSuite_SatinBlackRing.mat", new Color(0.285f, 0.295f, 0.305f));
            var glassMaterial = EnsureSurfaceMaterial("Assets/VLN/Materials/TopgearSensorSuite_Glass.mat", new Color(0.02f, 0.48f, 0.58f));
            var metalMaterial = EnsureSurfaceMaterial("Assets/VLN/Materials/TopgearSensorSuite_BoltMetal.mat", new Color(0.80f, 0.84f, 0.86f));
            var lidarAccentMaterial = EnsureSurfaceMaterial("Assets/VLN/Materials/TopgearSensorSuite_LidarAccent.mat", new Color(0.08f, 0.34f, 0.42f));

            ConfigureSensorMaterial(bodyMaterial, 0.02f, 0.20f);
            ConfigureSensorMaterial(ringMaterial, 0.00f, 0.35f);
            ConfigureSensorMaterial(glassMaterial, 0.00f, 0.70f);
            ConfigureSensorMaterial(metalMaterial, 0.65f, 0.52f);
            ConfigureSensorMaterial(lidarAccentMaterial, 0.05f, 0.42f);

            ScanPattern scanPattern = LoadVlp16ScanPattern();

            Transform lidar = CreateTopgearLidar(root.transform, scanPattern, bodyMaterial, ringMaterial, glassMaterial, metalMaterial, lidarAccentMaterial);

            // Sensor poses are the user's manually aligned Unity baseline. Do not re-derive these
            // from mesh bounds or old hole estimates; those defaults were visually wrong on Topgear.
            const float cameraCenterY = 0.723f;
            const float cameraBoxCenterZ = 0.077f;
            const float cameraBoxSideX = 0.050f;
            const float cameraBoxFrontX = 0.006f;
            const float cameraBoxFrontY = 0.8409f;
            const float cameraBoxFrontZ = 0.296f;
            const float cameraBoxRearZ = -0.041f;
            Transform front = CreateTopgearCamera(root.transform, TopgearFrontCameraName, new Vector3(cameraBoxFrontX, cameraBoxFrontY, cameraBoxFrontZ), Quaternion.identity, FrontImageTopic, FrontCameraInfoTopic, FrontCameraFrameId, bodyMaterial, ringMaterial, glassMaterial, metalMaterial);
            Transform rear = CreateTopgearCamera(root.transform, TopgearRearCameraName, new Vector3(0f, cameraCenterY, cameraBoxRearZ), Quaternion.Euler(0f, 180f, 0f), RearImageTopic, RearCameraInfoTopic, RearCameraFrameId, bodyMaterial, ringMaterial, glassMaterial, metalMaterial);
            Transform left = CreateTopgearCamera(root.transform, TopgearLeftCameraName, new Vector3(-cameraBoxSideX, cameraCenterY, cameraBoxCenterZ), Quaternion.Euler(0f, -90f, 0f), LeftImageTopic, LeftCameraInfoTopic, LeftCameraFrameId, bodyMaterial, ringMaterial, glassMaterial, metalMaterial);
            Transform right = CreateTopgearCamera(root.transform, TopgearRightCameraName, new Vector3(cameraBoxSideX, cameraCenterY, cameraBoxCenterZ), Quaternion.Euler(0f, 90f, 0f), RightImageTopic, RightCameraInfoTopic, RightCameraFrameId, bodyMaterial, ringMaterial, glassMaterial, metalMaterial);
            ApplyTopgearSensorPoseOverrides(root.transform);
            ApplyTopgearSensorHierarchyOverrides(root.transform);

            Debug.Log("VLN_TOPGEAR_SENSOR_SUITE_ATTACHED lidar=1 cameras=4 parent=rig topics=/vln/front,/vln/rear,/vln/left,/vln/right,/vln/lidar");
            return new TopgearSensorSuiteHandles
            {
                Root = root.transform,
                FrontCamera = front,
                RearCamera = rear,
                LeftCamera = left,
                RightCamera = right,
                Lidar = lidar,
            };
        }

        static Transform CreateTopgearLidar(Transform parent, ScanPattern scanPattern, Material bodyMaterial, Material ringMaterial, Material glassMaterial, Material metalMaterial, Material accentMaterial)
        {
            var lidarObject = new GameObject(TopgearLidarName);
            lidarObject.transform.SetParent(parent, false);
            // User-aligned LiDAR baseline. Keep this as the default so scene rebuilds do not fall
            // back to the old visually wrong center-only pose if JSON is unavailable.
            lidarObject.transform.localPosition = new Vector3(0.0021f, 0.8419999f, 0.0097f);
            lidarObject.transform.localRotation = Quaternion.Euler(0f, 266.9234f, 0f);
            lidarObject.transform.localScale = Vector3.one;
            lidarObject.layer = 0;

            CreateTopgearVlp16Visual(lidarObject.transform, bodyMaterial, ringMaterial, glassMaterial, metalMaterial, accentMaterial);

            var lidarSensor = lidarObject.AddComponent<RaycastLiDARSensor>();
            ConfigureLidarSensor(lidarSensor, scanPattern);

            var pointCloudPublisher = lidarObject.AddComponent<LiDARPointCloud2MsgPublisher>();
            ConfigurePointCloudPublisher(pointCloudPublisher, lidarSensor, PointCloudTopic, LidarFrameId);
            return lidarObject.transform;
        }

        static Transform CreateTopgearCamera(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, string imageTopic, string cameraInfoTopic, string frameId, Material bodyMaterial, Material ringMaterial, Material glassMaterial, Material metalMaterial)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = localPosition;
            cameraObject.transform.localRotation = localRotation;
            cameraObject.transform.localScale = Vector3.one;
            cameraObject.layer = 0;

            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.035f;
            camera.farClipPlane = 90f;
            camera.fieldOfView = 78f;
            camera.depth = -20f;

            var rgbSensor = cameraObject.AddComponent<RGBCameraSensor>();
            ConfigureRgbSensor(rgbSensor);

            var imagePublisher = cameraObject.AddComponent<ImageMsgPublisher>();
            ConfigureImagePublisher(imagePublisher, rgbSensor, imageTopic, frameId);

            var cameraInfoPublisher = cameraObject.AddComponent<CameraInfoMsgPublisher>();
            ConfigureCameraInfoPublisher(cameraInfoPublisher, rgbSensor, cameraInfoTopic, frameId);

            CreateTopgearD405Visual(cameraObject.transform, bodyMaterial, ringMaterial, glassMaterial, metalMaterial);

            return cameraObject.transform;
        }

        static void CreateTopgearVlp16Visual(Transform parent, Material bodyMaterial, Material ringMaterial, Material glassMaterial, Material metalMaterial, Material accentMaterial)
        {
            var visual = InstantiateImportedSensorModel(
                parent,
                "Velodyne_VLP16_OfficialMesh",
                new[] { Vlp16Base1AssetPath, Vlp16ScanAssetPath, Vlp16Base2AssetPath },
                Vector3.zero,
                // The VLP-16 DAE files declare Y_UP, but their mesh bounds show the cylindrical height
                // along source Z. Rotate source Z into Unity/vehicle Y so the official mesh stands upright.
                Quaternion.Euler(-90f, 0f, 0f),
                new Vector3(0.112f, 0.074f, 0.112f),
                Vector3.zero,
                bodyMaterial);
            TintImportedRenderersByName(visual, bodyMaterial, ringMaterial, glassMaterial, accentMaterial);
        }

        static void CreateTopgearD405Visual(Transform parent, Material bodyMaterial, Material ringMaterial, Material glassMaterial, Material metalMaterial)
        {
            var root = new GameObject("RealSense_D405_SquareStereoVisual");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.layer = 0;

            if (!TryCreateBinaryStlMeshVisual(root.transform, RealSenseD405AssetPath, "RealSense_D405_OfficialStlBody", bodyMaterial))
            {
                throw new FileNotFoundException($"Missing or invalid official RealSense D405 STL mesh: {RealSenseD405AssetPath}", ProjectRelativeToFullPath(RealSenseD405AssetPath));
            }
        }

        static bool TryCreateBinaryStlMeshVisual(Transform parent, string assetPath, string name, Material material)
        {
            string fullPath = ProjectRelativeToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return false;
            }

            byte[] data = File.ReadAllBytes(fullPath);
            if (data.Length < 84)
            {
                return false;
            }

            int triangleCount = checked((int)BitConverter.ToUInt32(data, 80));
            long expectedLength = 84L + triangleCount * 50L;
            if (triangleCount == 0 || expectedLength > data.Length)
            {
                return false;
            }

            float unitScale = EstimateStlUnitScale(data, triangleCount);
            var vertices = new Vector3[triangleCount * 3];
            var normals = new Vector3[triangleCount * 3];
            var indices = new int[triangleCount * 3];

            int vertexIndex = 0;
            for (int i = 0; i < triangleCount; i++)
            {
                int offset = 84 + i * 50;
                Vector3 normal = new Vector3(
                    BitConverter.ToSingle(data, offset),
                    BitConverter.ToSingle(data, offset + 4),
                    BitConverter.ToSingle(data, offset + 8));
                if (normal.sqrMagnitude < 1e-8f)
                {
                    normal = Vector3.forward;
                }
                normal.Normalize();

                for (int j = 0; j < 3; j++)
                {
                    int vertexOffset = offset + 12 + j * 12;
                    vertices[vertexIndex] = new Vector3(
                        BitConverter.ToSingle(data, vertexOffset) * unitScale,
                        BitConverter.ToSingle(data, vertexOffset + 4) * unitScale,
                        BitConverter.ToSingle(data, vertexOffset + 8) * unitScale);
                    normals[vertexIndex] = normal;
                    indices[vertexIndex] = vertexIndex;
                    vertexIndex++;
                }
            }

            var mesh = new Mesh
            {
                name = name + "Mesh",
                vertices = vertices,
                normals = normals,
                triangles = indices,
            };
            mesh.RecalculateBounds();

            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent, false);
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localRotation = Quaternion.identity;
            meshObject.transform.localScale = Vector3.one;
            meshObject.layer = 0;
            var meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return true;
        }

        static float EstimateStlUnitScale(byte[] data, int triangleCount)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < triangleCount; i++)
            {
                int offset = 84 + i * 50 + 12;
                for (int j = 0; j < 3; j++)
                {
                    int vertexOffset = offset + j * 12;
                    var vertex = new Vector3(
                        BitConverter.ToSingle(data, vertexOffset),
                        BitConverter.ToSingle(data, vertexOffset + 4),
                        BitConverter.ToSingle(data, vertexOffset + 8));
                    min = Vector3.Min(min, vertex);
                    max = Vector3.Max(max, vertex);
                }
            }

            Vector3 size = max - min;
            return Mathf.Max(size.x, size.y, size.z) > 1.0f ? 0.001f : 1.0f;
        }

        static void ConfigureTopgearSensorTf(GameObject rig, TopgearSensorSuiteHandles sensors)
        {
            var tfPublisher = rig.GetComponent<VlnVehicleTfPublisher>();
            if (tfPublisher == null)
            {
                tfPublisher = rig.AddComponent<VlnVehicleTfPublisher>();
            }

            var serializedPublisher = new SerializedObject(tfPublisher);
            serializedPublisher.FindProperty("m_CameraFrame").stringValue = FrontCameraFrameId;
            serializedPublisher.FindProperty("m_LidarFrame").stringValue = LidarFrameId;
            serializedPublisher.FindProperty("m_CameraTransform").objectReferenceValue = sensors.FrontCamera;
            serializedPublisher.FindProperty("m_LidarTransform").objectReferenceValue = sensors.Lidar;

            var additionalFrames = serializedPublisher.FindProperty("m_AdditionalSensorFrames");
            additionalFrames.arraySize = 3;
            SetAdditionalSensorFrame(additionalFrames.GetArrayElementAtIndex(0), RearCameraFrameId, sensors.RearCamera);
            SetAdditionalSensorFrame(additionalFrames.GetArrayElementAtIndex(1), LeftCameraFrameId, sensors.LeftCamera);
            SetAdditionalSensorFrame(additionalFrames.GetArrayElementAtIndex(2), RightCameraFrameId, sensors.RightCamera);
            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetAdditionalSensorFrame(SerializedProperty element, string frameId, Transform sensorTransform)
        {
            element.FindPropertyRelative("m_FrameId").stringValue = frameId;
            element.FindPropertyRelative("m_Transform").objectReferenceValue = sensorTransform;
        }

        [Serializable]
        public sealed class TopgearSensorPoseOverrideSet
        {
            public TopgearSensorPoseOverride[] sensors = Array.Empty<TopgearSensorPoseOverride>();
        }

        [Serializable]
        public sealed class TopgearSensorPoseOverride
        {
            public string name;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;
        }

        [Serializable]
        public sealed class TopgearSensorHierarchyOverrideSet
        {
            public TopgearSensorHierarchyOverride[] transforms = Array.Empty<TopgearSensorHierarchyOverride>();
        }

        [Serializable]
        public sealed class TopgearSensorHierarchyOverride
        {
            public string path;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public Vector3 localScale;
        }

        public static string TopgearSensorPoseOverridePath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", "config", "topgear_sensor_pose_overrides.json"));
        public static string TopgearSensorPoseUserLockedPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", "config", "topgear_sensor_pose_user_locked.json"));
        public static string TopgearSensorHierarchyUserLockedPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", "config", "topgear_sensor_hierarchy_user_locked.json"));

        public static string ActiveTopgearSensorPosePath => File.Exists(TopgearSensorPoseUserLockedPath) ? TopgearSensorPoseUserLockedPath : TopgearSensorPoseOverridePath;

        static void ApplyTopgearSensorPoseOverrides(Transform sensorRoot)
        {
            string path = ActiveTopgearSensorPosePath;
            if (!File.Exists(path))
            {
                return;
            }

            var overrides = JsonUtility.FromJson<TopgearSensorPoseOverrideSet>(File.ReadAllText(path));
            if (overrides == null || overrides.sensors == null)
            {
                if (path == TopgearSensorPoseUserLockedPath)
                {
                    throw new InvalidOperationException($"Topgear locked sensor pose file is invalid: {path}");
                }

                return;
            }

            int appliedCount = 0;
            foreach (var sensor in overrides.sensors)
            {
                if (sensor == null || string.IsNullOrWhiteSpace(sensor.name))
                {
                    continue;
                }

                Transform target = FindDeepChild(sensorRoot, sensor.name);
                if (target == null || target.parent != sensorRoot)
                {
                    continue;
                }

                target.localPosition = sensor.localPosition;
                target.localRotation = Quaternion.Euler(sensor.localEulerAngles);
                if (sensor.localScale.x > 0f && sensor.localScale.y > 0f && sensor.localScale.z > 0f)
                {
                    target.localScale = sensor.localScale;
                }
                appliedCount++;
            }

            if (path == TopgearSensorPoseUserLockedPath && appliedCount < 5)
            {
                throw new InvalidOperationException($"Topgear locked sensor pose file applied only {appliedCount}/5 sensors: {path}");
            }

            Debug.Log($"VLN_TOPGEAR_SENSOR_POSE_APPLIED path={path} count={appliedCount}");
        }

        static void ApplyTopgearSensorHierarchyOverrides(Transform sensorRoot)
        {
            string path = TopgearSensorHierarchyUserLockedPath;
            if (!File.Exists(path))
            {
                return;
            }

            var overrides = JsonUtility.FromJson<TopgearSensorHierarchyOverrideSet>(File.ReadAllText(path));
            if (overrides == null || overrides.transforms == null || overrides.transforms.Length == 0)
            {
                throw new InvalidOperationException($"Topgear locked sensor hierarchy file is invalid: {path}");
            }

            int appliedCount = 0;
            foreach (var pose in overrides.transforms)
            {
                if (pose == null || string.IsNullOrWhiteSpace(pose.path))
                {
                    continue;
                }

                Transform target = FindRelativeTransform(sensorRoot, pose.path);
                if (target == null)
                {
                    throw new InvalidOperationException($"Topgear locked sensor hierarchy path is missing after rebuild: {pose.path}");
                }

                target.localPosition = pose.localPosition;
                target.localRotation = Quaternion.Euler(pose.localEulerAngles);
                if (pose.localScale.x > 0f && pose.localScale.y > 0f && pose.localScale.z > 0f)
                {
                    target.localScale = pose.localScale;
                }

                appliedCount++;
            }

            Debug.Log($"VLN_TOPGEAR_SENSOR_HIERARCHY_APPLIED path={path} count={appliedCount}");
        }

        static Transform FindRelativeTransform(Transform root, string path)
        {
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            int index = parts[0] == root.name ? 1 : 0;
            Transform current = root;
            for (; index < parts.Length; index++)
            {
                Transform next = null;
                foreach (Transform child in current)
                {
                    if (child.name == parts[index])
                    {
                        next = child;
                        break;
                    }
                }

                if (next == null)
                {
                    return null;
                }

                current = next;
            }

            return current;
        }

        static GameObject InstantiateImportedSensorModel(Transform parent, string name, string[] assetPaths, Vector3 localPosition, Quaternion localRotation, Vector3 targetSize, Vector3 targetCenter, Material fallbackMaterial)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);
            container.transform.localPosition = localPosition;
            container.transform.localRotation = localRotation;
            container.transform.localScale = Vector3.one;
            container.layer = 0;

            foreach (string assetPath in assetPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                GameObject instance;
                if (prefab == null && assetPath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
                {
                    instance = CreateBinaryStlMeshObject(assetPath, fallbackMaterial);
                }
                else if (prefab == null)
                {
                    throw new FileNotFoundException($"Missing imported sensor mesh asset: {assetPath}", ProjectRelativeToFullPath(assetPath));
                }
                else
                {
                    instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (instance == null)
                    {
                        instance = UnityEngine.Object.Instantiate(prefab);
                    }
                }

                instance.name = Path.GetFileNameWithoutExtension(assetPath);
                instance.transform.SetParent(container.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                RemovePhysicsComponents(instance);
                SetLayerRecursively(instance, 0);

                if (fallbackMaterial != null)
                {
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.sharedMaterial == null)
                        {
                            renderer.sharedMaterial = fallbackMaterial;
                        }
                    }
                }
            }

            FitRendererBoundsToLocalBox(container, targetSize, targetCenter);
            return container;
        }

        static GameObject CreateBinaryStlMeshObject(string assetPath, Material material)
        {
            string fullPath = ProjectRelativeToFullPath(assetPath);
            byte[] data = File.ReadAllBytes(fullPath);
            if (data.Length < 84)
            {
                throw new InvalidDataException($"STL file too short: {assetPath}");
            }

            int triangleCount = BitConverter.ToInt32(data, 80);
            int expectedLength = 84 + triangleCount * 50;
            if (triangleCount <= 0 || data.Length < expectedLength)
            {
                throw new InvalidDataException($"Invalid binary STL triangle payload: {assetPath}");
            }

            var vertices = new Vector3[triangleCount * 3];
            var triangles = new int[triangleCount * 3];
            int offset = 84;
            const float millimetersToMeters = 0.001f;
            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                offset += 12; // facet normal, recalculated below after import.
                for (int vertex = 0; vertex < 3; vertex++)
                {
                    float x = BitConverter.ToSingle(data, offset) * millimetersToMeters;
                    float y = BitConverter.ToSingle(data, offset + 4) * millimetersToMeters;
                    float z = BitConverter.ToSingle(data, offset + 8) * millimetersToMeters;
                    int index = triangle * 3 + vertex;
                    vertices[index] = new Vector3(x, y, z);
                    triangles[index] = index;
                    offset += 12;
                }
                offset += 2; // attribute byte count.
            }

            var mesh = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(assetPath) + "_mesh"
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var instance = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
            var meshFilter = instance.AddComponent<MeshFilter>();
            var meshRenderer = instance.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            return instance;
        }

        static void FitRendererBoundsToLocalBox(GameObject root, Vector3 targetSize, Vector3 targetCenter)
        {
            Bounds initialBounds = CalculateRendererBoundsInLocalFrame(root, root.transform);
            if (initialBounds.size.x <= 0f || initialBounds.size.y <= 0f || initialBounds.size.z <= 0f)
            {
                throw new InvalidOperationException($"Cannot fit sensor mesh with empty bounds: {root.name}");
            }

            Vector3 rotatedSize = AxisAlignedSizeAfterRotation(root.transform.localRotation, targetSize);
            root.transform.localScale = new Vector3(
                rotatedSize.x / initialBounds.size.x,
                rotatedSize.y / initialBounds.size.y,
                rotatedSize.z / initialBounds.size.z);

            Bounds scaledBounds = CalculateRendererBoundsInLocalFrame(root, root.transform.parent);
            root.transform.localPosition += targetCenter - scaledBounds.center;
        }

        static Vector3 AxisAlignedSizeAfterRotation(Quaternion rotation, Vector3 targetParentSize)
        {
            Matrix4x4 matrix = Matrix4x4.Rotate(rotation);
            var localX = new Vector3(Mathf.Abs(matrix.m00), Mathf.Abs(matrix.m10), Mathf.Abs(matrix.m20));
            var localY = new Vector3(Mathf.Abs(matrix.m01), Mathf.Abs(matrix.m11), Mathf.Abs(matrix.m21));
            var localZ = new Vector3(Mathf.Abs(matrix.m02), Mathf.Abs(matrix.m12), Mathf.Abs(matrix.m22));
            return new Vector3(
                Vector3.Dot(localX, targetParentSize),
                Vector3.Dot(localY, targetParentSize),
                Vector3.Dot(localZ, targetParentSize));
        }

        static void TintImportedRenderersByName(GameObject root, Material bodyMaterial, Material ringMaterial, Material glassMaterial, Material accentMaterial)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string lower = renderer.name.ToLowerInvariant();
                if (lower.Contains("scan"))
                {
                    renderer.sharedMaterial = glassMaterial;
                }
                else if (lower.Contains("base_2"))
                {
                    renderer.sharedMaterial = ringMaterial;
                }
                else if (lower.Contains("base_1"))
                {
                    renderer.sharedMaterial = bodyMaterial;
                }
                else if (renderer.sharedMaterial == null)
                {
                    renderer.sharedMaterial = accentMaterial;
                }
            }
        }

        static void EnsureReadableCameraMaterials(GameObject root, Material bodyMaterial, Material glassMaterial, Material metalMaterial)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial == null)
                {
                    renderer.sharedMaterial = bodyMaterial;
                    continue;
                }

                string materialName = renderer.sharedMaterial.name.ToLowerInvariant();
                if (materialName.Contains("glass") || materialName.Contains("lens"))
                {
                    renderer.sharedMaterial = glassMaterial;
                }
                else if (materialName.Contains("aluminum") || materialName.Contains("metal"))
                {
                    renderer.sharedMaterial = metalMaterial;
                }
            }
        }

        static void ConfigureSensorMaterial(Material material, float metallic, float glossiness)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", glossiness);
            }
            EditorUtility.SetDirty(material);
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
            serializedSensor.FindProperty("_frequency").floatValue = SensorFrequencyHz;
            serializedSensor.FindProperty("_resolution").vector2IntValue = TopgearCameraResolution;
            serializedSensor.FindProperty("_fov").floatValue = 78f;
            serializedSensor.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureImagePublisher(ImageMsgPublisher publisher, RGBCameraSensor sensor, string topicName, string frameId)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("_frequency").floatValue = SensorFrequencyHz;
            serializedPublisher.FindProperty("_topicName").stringValue = topicName;

            var serializer = serializedPublisher.FindProperty("_serializer");
            serializer.FindPropertyRelative("_source").objectReferenceValue = sensor;
            serializer.FindPropertyRelative("_sourceTexture").enumValueIndex = 0;
            serializer.FindPropertyRelative("_encoding").enumValueIndex = 0;

            var header = serializer.FindPropertyRelative("_header");
            header.FindPropertyRelative("_source").objectReferenceValue = sensor;
            header.FindPropertyRelative("_frame_id").stringValue = frameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureCameraInfoPublisher(CameraInfoMsgPublisher publisher, RGBCameraSensor sensor, string topicName, string frameId)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("_frequency").floatValue = SensorFrequencyHz;
            serializedPublisher.FindProperty("_topicName").stringValue = topicName;

            var serializer = serializedPublisher.FindProperty("_serializer");
            serializer.FindPropertyRelative("_source").objectReferenceValue = sensor;

            var header = serializer.FindPropertyRelative("_header");
            header.FindPropertyRelative("_source").objectReferenceValue = sensor;
            header.FindPropertyRelative("_frame_id").stringValue = frameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLidarSensor(RaycastLiDARSensor sensor, ScanPattern scanPattern)
        {
            var serializedSensor = new SerializedObject(sensor);
            serializedSensor.FindProperty("_frequency").floatValue = SensorFrequencyHz;
            serializedSensor.FindProperty("_scanPattern").objectReferenceValue = scanPattern;
            serializedSensor.FindProperty("_pointsNumPerScan").intValue = LidarPointsPerScan;
            serializedSensor.FindProperty("_minRange").floatValue = LidarMinRange;
            serializedSensor.FindProperty("_maxRange").floatValue = LidarMaxRange;
            serializedSensor.FindProperty("_gaussianNoiseSigma").floatValue = 0.0f;
            serializedSensor.FindProperty("_maxIntensity").floatValue = 255.0f;
            serializedSensor.FindProperty("_raycastLayerMask").intValue = 1;
            serializedSensor.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePointCloudPublisher(LiDARPointCloud2MsgPublisher publisher, RaycastLiDARSensor sensor, string topicName, string frameId)
        {
            var serializedPublisher = new SerializedObject(publisher);
            serializedPublisher.FindProperty("_frequency").floatValue = SensorFrequencyHz;
            serializedPublisher.FindProperty("_topicName").stringValue = topicName;
            serializedPublisher.FindProperty("_source").objectReferenceValue = sensor;

            var serializer = serializedPublisher.FindProperty("_serializer");
            var header = serializer.FindPropertyRelative("_header");
            header.FindPropertyRelative("_source").objectReferenceValue = sensor;
            header.FindPropertyRelative("_frame_id").stringValue = frameId;

            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureScoutWheelGroundCamera(Transform rig)
        {
            DestroyChildIfExists(rig, "ScoutWheelGroundCandidate_GameCamera");

            var overview = GameObject.Find("Offroad_ViewerCamera");
            if (overview != null && overview.TryGetComponent<Camera>(out var overviewCamera))
            {
                overviewCamera.depth = -10f;
            }

            var cameraObject = new GameObject("ScoutWheelGroundCandidate_GameCamera");
            cameraObject.transform.SetParent(rig, false);
            cameraObject.transform.localPosition = new Vector3(2.3f, 1.45f, -2.85f);
            cameraObject.transform.LookAt(rig.position + Vector3.up * 0.45f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 43f;
            camera.depth = 10f;

            var controller = cameraObject.AddComponent<VlnRuntimeMapCameraController>();
            controller.Configure(PhysicsRootName, new Vector3(0f, 0.45f, 0f), 1.20f, 44.0f);
        }

        static PhysicMaterial EnsureHighFrictionMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(FrictionMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory("Assets/VLN/Materials");
                material = new PhysicMaterial("ScoutWheelGround_HighFriction");
                AssetDatabase.CreateAsset(material, FrictionMaterialPath);
            }

            material.staticFriction = 1.2f;
            material.dynamicFriction = 1.05f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicMaterialCombine.Maximum;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static PhysicMaterial EnsurePhysicMaterial(string assetPath, string materialName, float staticFriction, float dynamicFriction, PhysicMaterialCombine frictionCombine)
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(assetPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                material = new PhysicMaterial(materialName);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.staticFriction = staticFriction;
            material.dynamicFriction = dynamicFriction;
            material.bounciness = 0f;
            material.frictionCombine = frictionCombine;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material EnsureSurfaceMaterial(string assetPath, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.color = color;
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.08f);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ImportPbrTextureFolderIfPresent()
        {
            if (!AssetDatabase.IsValidFolder(PbrMaterialRoot))
            {
                Debug.Log("VLN_SCOUT_WHEEL_GROUND_PBR_TEXTURES_MISSING using_color_fallback=true");
                return;
            }

            AssetDatabase.ImportAsset(PbrMaterialRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            ConfigurePbrTextureImporter(StoneAlbedoTexturePath, TextureImporterType.Default, true);
            ConfigurePbrTextureImporter(StoneNormalTexturePath, TextureImporterType.NormalMap, false);
            ConfigurePbrTextureImporter(StoneOcclusionTexturePath, TextureImporterType.Default, false);
            ConfigurePbrTextureImporter(StoneRoughnessTexturePath, TextureImporterType.Default, false);
            ConfigurePbrTextureImporter(SandAlbedoTexturePath, TextureImporterType.Default, true);
            ConfigurePbrTextureImporter(SandNormalTexturePath, TextureImporterType.NormalMap, false);
            ConfigurePbrTextureImporter(SandOcclusionTexturePath, TextureImporterType.Default, false);
            ConfigurePbrTextureImporter(SandRoughnessTexturePath, TextureImporterType.Default, false);
        }

        static void ConfigurePbrTextureImporter(string texturePath, TextureImporterType textureType, bool sRgbTexture)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.textureType != textureType)
            {
                importer.textureType = textureType;
                changed = true;
            }

            if (importer.sRGBTexture != sRgbTexture)
            {
                importer.sRGBTexture = sRgbTexture;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        static void ConfigureChallengePbrMaterials(
            Material stoneSurface,
            Material stonePaver,
            Material stoneChip,
            Material sandSurface,
            Material sandRipple,
            Material sandDepression,
            Material sandGrain)
        {
            ConfigurePbrMaterial(
                stoneSurface,
                new Color(0.92f, 0.96f, 0.96f),
                StoneAlbedoTexturePath,
                StoneNormalTexturePath,
                StoneOcclusionTexturePath,
                0.82f,
                0.68f,
                0.05f);
            ConfigurePbrMaterial(
                stonePaver,
                new Color(0.96f, 0.99f, 0.99f),
                StoneAlbedoTexturePath,
                StoneNormalTexturePath,
                StoneOcclusionTexturePath,
                1.05f,
                0.78f,
                0.06f);
            ConfigurePbrMaterial(
                stoneChip,
                new Color(0.78f, 0.84f, 0.84f),
                StoneAlbedoTexturePath,
                StoneNormalTexturePath,
                StoneOcclusionTexturePath,
                1.35f,
                0.55f,
                0.04f);
            ConfigurePbrMaterial(
                sandSurface,
                new Color(1.00f, 0.94f, 0.76f),
                SandAlbedoTexturePath,
                SandNormalTexturePath,
                SandOcclusionTexturePath,
                0.95f,
                0.72f,
                0.025f);
            ConfigurePbrMaterial(
                sandRipple,
                new Color(1.00f, 0.96f, 0.80f),
                SandAlbedoTexturePath,
                SandNormalTexturePath,
                SandOcclusionTexturePath,
                1.25f,
                0.85f,
                0.02f);
            ConfigurePbrMaterial(
                sandDepression,
                new Color(0.82f, 0.75f, 0.58f),
                SandAlbedoTexturePath,
                SandNormalTexturePath,
                SandOcclusionTexturePath,
                0.75f,
                0.45f,
                0.015f);
            ConfigurePbrMaterial(
                sandGrain,
                new Color(1.00f, 0.93f, 0.70f),
                SandAlbedoTexturePath,
                SandNormalTexturePath,
                SandOcclusionTexturePath,
                1.55f,
                0.40f,
                0.018f);
        }

        static void ConfigurePbrMaterial(
            Material material,
            Color tint,
            string albedoPath,
            string normalPath,
            string occlusionPath,
            float textureScale,
            float normalScale,
            float glossiness)
        {
            if (material == null)
            {
                return;
            }

            material.color = tint;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(occlusionPath);

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", albedo);
                material.SetTextureScale("_MainTex", Vector2.one * textureScale);
            }

            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", normal != null ? normalScale : 1f);
                if (normal != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    material.DisableKeyword("_NORMALMAP");
                }
            }

            if (material.HasProperty("_OcclusionMap"))
            {
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetFloat("_OcclusionStrength", occlusion != null ? 0.72f : 0f);
                if (occlusion != null)
                {
                    material.EnableKeyword("_OCCLUSIONMAP");
                }
                else
                {
                    material.DisableKeyword("_OCCLUSIONMAP");
                }
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", glossiness);
            }

            EditorUtility.SetDirty(material);
        }

        static void AssignSceneFrictionMaterial(PhysicMaterial material)
        {
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if (collider is WheelCollider)
                {
                    continue;
                }

                if (collider.transform.IsChildOf(GameObject.Find(RigName)?.transform))
                {
                    continue;
                }

                collider.material = material;
            }
        }

        static void CreatePostRampChallengeCourse(
            Material grassMaterial,
            Material grassBladeMaterial,
            Material grassBladeLightMaterial,
            Material grassBladeDryMaterial,
            Material grassSoilMaterial,
            Material stoneMaterial,
            Material stonePaverMaterial,
            Material stoneSeamMaterial,
            Material stoneChipMaterial,
            Material sandMaterial,
            Material sandRippleMaterial,
            Material sandDepressionMaterial,
            Material sandGrainMaterial,
            Material obstacleMaterial,
            Material markerMaterial,
            PhysicMaterial grassPhysic,
            PhysicMaterial stonePhysic,
            PhysicMaterial sandPhysic,
            PhysicMaterial obstaclePhysic)
        {
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_ChallengeSurface_");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_ChallengeMarker_");
            RemoveIfExists("Offroad_DistantWall_Target");

            CreateChallengeProfileSurface(
                "ScoutWheelGround_ChallengeSurface_Grass",
                0f,
                5.4f,
                new[] { 10.0f, 11.6f, 13.4f, 15.2f, 16.8f },
                new[] { 0.068f, 0.096f, 0.078f, 0.105f, 0.072f },
                0.055f,
                grassPhysic,
                grassMaterial);
            CreateGrassDetails(grassBladeMaterial, grassBladeLightMaterial, grassBladeDryMaterial, grassSoilMaterial, obstacleMaterial, obstaclePhysic);
            CreateGrassPhysicsProxies(grassSoilMaterial, grassPhysic);

            CreateChallengeProfileSurface(
                "ScoutWheelGround_ChallengeSurface_Stone",
                0f,
                5.6f,
                new[] { 20.0f, 21.5f, 23.1f, 24.8f, 26.5f, 28.0f },
                new[] { 0.066f, 0.082f, 0.071f, 0.088f, 0.074f, 0.064f },
                0.055f,
                stonePhysic,
                stoneMaterial);
            CreateStonePaverDetails(stonePaverMaterial, stoneSeamMaterial, stoneChipMaterial, obstacleMaterial, obstaclePhysic);
            CreateStonePhysicsProxies(stoneSeamMaterial, stonePhysic);

            CreateChallengeProfileSurface(
                "ScoutWheelGround_ChallengeSurface_Sand",
                0f,
                5.8f,
                new[] { 32.0f, 34.0f, 36.2f, 38.6f, 41.0f, 43.4f, 46.0f, 49.0f },
                new[] { 0.060f, 0.102f, 0.052f, 0.116f, 0.071f, 0.086f, 0.064f, 0.058f },
                0.062f,
                sandPhysic,
                sandMaterial);
            CreateSandDetails(sandRippleMaterial, sandDepressionMaterial, sandGrainMaterial, obstacleMaterial, obstaclePhysic);
            CreateSandPhysicsProxies(sandRippleMaterial, sandPhysic);

            CreateChallengeBox(
                "Offroad_DistantWall_Target",
                0f,
                53.50f,
                8.0f,
                2.3f,
                0.4f,
                1.15f,
                markerMaterial,
                obstaclePhysic,
                true);
            CreateChallengeBox(
                "ScoutWheelGround_ChallengeMarker_EndWallMoved",
                0f,
                53.48f,
                8.2f,
                0.06f,
                0.50f,
                0.04f,
                markerMaterial,
                obstaclePhysic,
                true);

            Debug.Log("VLN_SCOUT_WHEEL_GROUND_CHALLENGE_COURSE_READY surfaces=3 grass_z=10.0..16.8 stone_z=20.0..28.0 sand_z=32.0..49.0 obstacles=scattered_passable_low_features material_physics_proxies=grass_stone_sand end_wall_z=53.50 baseline_bridge_and_ramp_unchanged=true");
        }

        static void CreateChallengeProfileSurface(string name, float centerX, float width, float[] zValues, float[] yOffsets, float thickness, PhysicMaterial material, Material renderMaterial)
        {
            if (zValues == null || yOffsets == null || zValues.Length != yOffsets.Length)
            {
                throw new ArgumentException("Challenge profile surface requires matching z/y-offset arrays.");
            }

            var yValues = new float[zValues.Length];
            for (int i = 0; i < zValues.Length; i++)
            {
                yValues[i] = TerrainWorldY(centerX, zValues[i]) + yOffsets[i];
            }

            CreateProfiledPhysicalSurface(name, centerX, width, zValues, yValues, thickness, material, renderMaterial);
        }

        static void CreateGrassDetails(Material bladeMaterial, Material bladeLightMaterial, Material bladeDryMaterial, Material soilMaterial, Material bumpMaterial, PhysicMaterial physicMaterial)
        {
            CreateGrassBladeField("ScoutWheelGround_ChallengeObstacle_GrassBladeField_Dark", bladeMaterial, ChallengeGrassBladeMeshPath, 0, 680, 0.18f, 0.43f, 0.000f);
            CreateGrassBladeField("ScoutWheelGround_ChallengeObstacle_GrassBladeField_Light", bladeLightMaterial, ChallengeGrassBladeLightMeshPath, 131, 560, 0.15f, 0.35f, 0.018f);
            CreateGrassBladeField("ScoutWheelGround_ChallengeObstacle_GrassBladeField_Dry", bladeDryMaterial, ChallengeGrassBladeDryMeshPath, 263, 260, 0.11f, 0.28f, -0.014f);

            for (int i = 0; i < 8; i++)
            {
                float x = ((i * 5) % 9 - 4) * 0.45f;
                float z = 10.8f + i * 0.72f;
                CreateVisualBox($"ScoutWheelGround_ChallengeObstacle_GrassSoilPatch_{i:00}", x, TerrainWorldY(x, z) + 0.118f, z, 0.62f, 0.008f, 0.34f, i * 21f, soilMaterial);
            }

            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_GrassLowRoot_00", -0.80f, 12.20f, 0.34f, 0.024f, 0.46f, 0.101f, bumpMaterial, physicMaterial, false, 12f);
            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_GrassLowRoot_01", 0.95f, 14.60f, 0.38f, 0.026f, 0.50f, 0.105f, bumpMaterial, physicMaterial, false, -18f);
        }

        static void CreateGrassPhysicsProxies(Material material, PhysicMaterial physicMaterial)
        {
            float[] zValues = { 10.90f, 12.05f, 13.28f, 14.45f, 15.70f };
            for (int i = 0; i < zValues.Length; i++)
            {
                float x = i % 2 == 0 ? -0.10f : 0.14f;
                float yaw = i % 2 == 0 ? -9f : 8f;
                CreateChallengeBox(
                    $"ScoutWheelGround_ChallengePhysicsProxy_GrassFlexRow_{i:00}",
                    x,
                    zValues[i],
                    4.70f,
                    0.012f,
                    0.115f,
                    0.132f,
                    material,
                    physicMaterial,
                    false,
                    yaw);
            }
        }

        static void CreateGrassBladeField(string name, Material bladeMaterial, string meshPath, int seedOffset, int bladeCount, float minHeight, float maxHeight, float zBias)
        {
            const float halfWidth = 2.45f;
            const float zStart = 10.28f;
            const float zLength = 6.20f;
            var vertices = new Vector3[bladeCount * 6];
            var triangles = new int[bladeCount * 6];
            var uvs = new Vector2[bladeCount * 6];

            for (int i = 0; i < bladeCount; i++)
            {
                float xNoise = PseudoRandom01(seedOffset + i * 17 + 3);
                float zNoise = PseudoRandom01(seedOffset + i * 29 + 11);
                float leanNoise = PseudoRandom01(seedOffset + i * 43 + 7);
                float widthNoise = PseudoRandom01(seedOffset + i * 59 + 19);
                float heightNoise = PseudoRandom01(seedOffset + i * 71 + 23);
                float yaw = PseudoRandom01(seedOffset + i * 83 + 31) * Mathf.PI * 2f;
                float x = Mathf.Lerp(-halfWidth, halfWidth, xNoise);
                float z = zStart + zNoise * zLength + zBias;
                float baseY = TerrainWorldY(x, z) + 0.136f;
                float bladeWidth = Mathf.Lerp(0.018f, 0.042f, widthNoise);
                float bladeHeight = Mathf.Lerp(minHeight, maxHeight, heightNoise);
                float lean = Mathf.Lerp(-0.075f, 0.075f, leanNoise);
                Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 forward = new Vector3(-right.z, 0f, right.x);
                Vector3 baseCenter = new Vector3(x, baseY, z);
                Vector3 tip = baseCenter + Vector3.up * bladeHeight + forward * lean;
                Vector3 leftBase = baseCenter - right * bladeWidth;
                Vector3 rightBase = baseCenter + right * bladeWidth;
                int v = i * 6;

                vertices[v + 0] = leftBase;
                vertices[v + 1] = rightBase;
                vertices[v + 2] = tip;
                vertices[v + 3] = rightBase;
                vertices[v + 4] = leftBase;
                vertices[v + 5] = tip;

                for (int j = 0; j < 6; j++)
                {
                    triangles[v + j] = v + j;
                }

                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0.5f, 1f);
                uvs[v + 3] = new Vector2(1f, 0f);
                uvs[v + 4] = new Vector2(0f, 0f);
                uvs[v + 5] = new Vector2(0.5f, 1f);
            }

            var mesh = new Mesh { name = "ScoutWheelGround_ChallengeGrassBladeFieldMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            DeleteAssetIfExists(meshPath);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            AssetDatabase.CreateAsset(mesh, meshPath);

            var field = new GameObject(name);
            field.transform.position = Vector3.zero;
            field.transform.rotation = Quaternion.identity;
            field.isStatic = true;
            var meshFilter = field.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var meshRenderer = field.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = bladeMaterial;
            var deformer = field.AddComponent<VlnChallengeGrassDeformer>();
            deformer.Configure(PhysicsRootName, 0.56f, 0.25f, 0.26f, 0.32f, 0.012f);
        }

        static float PseudoRandom01(int seed)
        {
            float value = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        static void CreateStonePaverDetails(Material paverMaterial, Material seamMaterial, Material chipMaterial, Material bumpMaterial, PhysicMaterial physicMaterial)
        {
            int slabIndex = 0;
            for (int row = 0; row < 8; row++)
            {
                float z = 20.35f + row * 0.88f;
                float rowShift = row % 2 == 0 ? 0.0f : 0.36f;
                for (int col = -3; col <= 3; col++)
                {
                    float x = col * 0.78f + rowShift;
                    if (Mathf.Abs(x) > 2.45f)
                    {
                        continue;
                    }

                    float y = TerrainWorldY(x, z) + 0.093f + 0.003f * ((row + col + 8) % 3);
                    float width = 0.60f + 0.08f * PseudoRandom01(row * 37 + col * 11 + 5);
                    float length = 0.53f + 0.09f * PseudoRandom01(row * 41 + col * 13 + 17);
                    CreateVisualBox($"ScoutWheelGround_ChallengeObstacle_StonePaverVisual_{slabIndex:00}", x, y, z, width, 0.014f, length, (row + col) * 2.0f, paverMaterial);

                    if ((row + col + 7) % 3 == 0)
                    {
                        CreateVisualBox($"ScoutWheelGround_ChallengeObstacle_StoneHairlineCrack_{slabIndex:00}", x + 0.05f, y + 0.009f, z, 0.018f, 0.004f, length * 0.62f, 31f + row * 3f, seamMaterial);
                    }

                    slabIndex++;
                }
            }

            for (int i = 0; i < 7; i++)
            {
                float z = 20.75f + i * 0.88f;
                CreateVisualBox($"ScoutWheelGround_ChallengeObstacle_StoneDarkJoint_{i:00}", 0f, TerrainWorldY(0f, z) + 0.096f, z, 5.15f, 0.006f, 0.030f, 0f, seamMaterial);
            }

            CreateStoneChipField("ScoutWheelGround_ChallengeObstacle_StoneChipField", chipMaterial);

            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_StoneSettledBump_00", -0.62f, 22.35f, 0.52f, 0.018f, 0.38f, 0.106f, bumpMaterial, physicMaterial, false, 4f);
            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_StoneSettledBump_01", 0.72f, 24.10f, 0.54f, 0.020f, 0.40f, 0.108f, bumpMaterial, physicMaterial, false, -6f);
            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_StoneSettledBump_02", -0.10f, 26.10f, 0.48f, 0.018f, 0.34f, 0.106f, bumpMaterial, physicMaterial, false, 9f);
        }

        static void CreateStonePhysicsProxies(Material material, PhysicMaterial physicMaterial)
        {
            for (int i = 0; i < 7; i++)
            {
                float z = 20.75f + i * 0.88f;
                CreateChallengeBox(
                    $"ScoutWheelGround_ChallengePhysicsProxy_StoneRigidJoint_{i:00}",
                    0f,
                    z,
                    5.10f,
                    0.010f,
                    0.030f,
                    0.116f,
                    material,
                    physicMaterial,
                    false,
                    0f);
            }
        }

        static void CreateStoneChipField(string name, Material chipMaterial)
        {
            const int chipCount = 140;
            var vertices = new Vector3[chipCount * 4];
            var triangles = new int[chipCount * 6];
            var uvs = new Vector2[chipCount * 4];

            for (int i = 0; i < chipCount; i++)
            {
                float x = Mathf.Lerp(-2.35f, 2.35f, PseudoRandom01(i * 19 + 5));
                float z = Mathf.Lerp(20.25f, 27.75f, PseudoRandom01(i * 23 + 13));
                float yaw = PseudoRandom01(i * 29 + 17) * Mathf.PI;
                float halfWidth = Mathf.Lerp(0.025f, 0.070f, PseudoRandom01(i * 31 + 19));
                float halfLength = Mathf.Lerp(0.018f, 0.055f, PseudoRandom01(i * 37 + 23));
                float y = TerrainWorldY(x, z) + 0.106f;
                Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 forward = new Vector3(-right.z, 0f, right.x);
                Vector3 center = new Vector3(x, y, z);
                int v = i * 4;
                vertices[v + 0] = center - right * halfWidth - forward * halfLength;
                vertices[v + 1] = center + right * halfWidth - forward * halfLength * 0.8f;
                vertices[v + 2] = center + right * halfWidth * 0.7f + forward * halfLength;
                vertices[v + 3] = center - right * halfWidth * 0.9f + forward * halfLength * 0.8f;

                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
            }

            CreateMeshFieldAssetAndObject(name, ChallengeStoneChipMeshPath, vertices, triangles, uvs, chipMaterial);
        }

        static void CreateSandDetails(Material rippleMaterial, Material depressionMaterial, Material grainMaterial, Material bumpMaterial, PhysicMaterial physicMaterial)
        {
            for (int i = 0; i < 24; i++)
            {
                float z = 32.40f + i * 0.70f;
                float x = Mathf.Lerp(-0.34f, 0.34f, PseudoRandom01(i * 23 + 11));
                float yaw = Mathf.Lerp(-14f, 14f, PseudoRandom01(i * 31 + 7));
                float width = Mathf.Lerp(3.4f, 5.1f, PseudoRandom01(i * 37 + 13));
                float length = Mathf.Lerp(0.045f, 0.095f, PseudoRandom01(i * 41 + 19));
                CreateVisualBox($"ScoutWheelGround_ChallengeObstacle_SandRippleVisual_{i:00}", x, TerrainWorldY(x, z) + 0.126f, z, width, 0.010f, length, yaw, rippleMaterial);
            }

            for (int i = 0; i < 11; i++)
            {
                float x = (i - 5) * 0.34f;
                float z = 33.0f + i * 1.48f;
                CreateVisualCylinderDisk($"ScoutWheelGround_ChallengeObstacle_SandShallowDepression_{i:00}", x, TerrainWorldY(x, z) + 0.123f, z, 0.44f + 0.04f * (i % 2), 0.22f, 0.006f, i * 19f, depressionMaterial);
            }

            CreateSandGrainField("ScoutWheelGround_ChallengeObstacle_SandGrainField", grainMaterial);

            for (int i = 0; i < 8; i++)
            {
                float z = 33.0f + i * 1.85f;
                float x = i % 2 == 0 ? -0.22f : 0.28f;
                CreateChallengeBox($"ScoutWheelGround_ChallengeObstacle_SandSoftRipple_{i:00}", x, z, 2.55f, 0.018f, 0.050f, 0.135f + 0.003f * (i % 2), bumpMaterial, physicMaterial, false, i % 2 == 0 ? 7f : -7f);
            }

            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_SandSideStoneLeft", -1.95f, 39.70f, 0.34f, 0.070f, 0.42f, 0.135f, bumpMaterial, physicMaterial, false, 14f);
            CreateChallengeBox("ScoutWheelGround_ChallengeObstacle_SandSideStoneRight", 1.90f, 41.10f, 0.32f, 0.065f, 0.40f, 0.132f, bumpMaterial, physicMaterial, false, -12f);
        }

        static void CreateSandPhysicsProxies(Material material, PhysicMaterial physicMaterial)
        {
            for (int i = 0; i < 10; i++)
            {
                float z = 32.65f + i * 1.58f;
                float x = Mathf.Lerp(-0.22f, 0.22f, PseudoRandom01(i * 29 + 7));
                float yaw = Mathf.Lerp(-12f, 12f, PseudoRandom01(i * 31 + 13));
                float width = Mathf.Lerp(3.9f, 5.0f, PseudoRandom01(i * 37 + 17));
                CreateChallengeBox(
                    $"ScoutWheelGround_ChallengePhysicsProxy_SandSoftRipple_{i:00}",
                    x,
                    z,
                    width,
                    0.012f,
                    0.080f,
                    0.140f,
                    material,
                    physicMaterial,
                    false,
                    yaw);
            }
        }

        static void CreateSandGrainField(string name, Material grainMaterial)
        {
            const int grainCount = 360;
            var vertices = new Vector3[grainCount * 4];
            var triangles = new int[grainCount * 6];
            var uvs = new Vector2[grainCount * 4];

            for (int i = 0; i < grainCount; i++)
            {
                float x = Mathf.Lerp(-2.55f, 2.55f, PseudoRandom01(i * 17 + 41));
                float z = Mathf.Lerp(32.15f, 48.65f, PseudoRandom01(i * 29 + 47));
                float yaw = PseudoRandom01(i * 31 + 53) * Mathf.PI;
                float halfWidth = Mathf.Lerp(0.010f, 0.026f, PseudoRandom01(i * 37 + 59));
                float halfLength = Mathf.Lerp(0.007f, 0.022f, PseudoRandom01(i * 43 + 61));
                float y = TerrainWorldY(x, z) + 0.129f;
                Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 forward = new Vector3(-right.z, 0f, right.x);
                Vector3 center = new Vector3(x, y, z);
                int v = i * 4;
                vertices[v + 0] = center - right * halfWidth - forward * halfLength;
                vertices[v + 1] = center + right * halfWidth - forward * halfLength;
                vertices[v + 2] = center + right * halfWidth + forward * halfLength;
                vertices[v + 3] = center - right * halfWidth + forward * halfLength;

                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
            }

            CreateMeshFieldAssetAndObject(name, ChallengeSandGrainMeshPath, vertices, triangles, uvs, grainMaterial);
        }

        static void CreateMeshFieldAssetAndObject(string name, string meshPath, Vector3[] vertices, int[] triangles, Vector2[] uvs, Material material)
        {
            var mesh = new Mesh { name = name + "Mesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            DeleteAssetIfExists(meshPath);
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            AssetDatabase.CreateAsset(mesh, meshPath);

            var field = new GameObject(name);
            field.transform.position = Vector3.zero;
            field.transform.rotation = Quaternion.identity;
            field.isStatic = true;
            var meshFilter = field.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            var meshRenderer = field.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
        }

        static void CreateVisualBox(string name, float x, float y, float z, float width, float height, float length, float yawDegrees, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = new Vector3(x, y, z);
            box.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            box.transform.localScale = new Vector3(width, height, length);
            box.layer = 0;
            box.isStatic = true;
            box.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        }

        static void CreateVisualCylinderDisk(string name, float x, float y, float z, float radiusX, float radiusZ, float height, float yawDegrees, Material material)
        {
            var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name = name;
            disk.transform.position = new Vector3(x, y, z);
            disk.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            disk.transform.localScale = new Vector3(radiusX, height * 0.5f, radiusZ);
            disk.layer = 0;
            disk.isStatic = true;
            disk.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(disk.GetComponent<Collider>());
        }

        static void CreateChallengeBox(string name, float x, float z, float width, float height, float length, float topOffset, Material material, PhysicMaterial physicMaterial, bool centerHeightIsTopOffset, float yawDegrees = 0f)
        {
            float centerY = centerHeightIsTopOffset ? TerrainWorldY(x, z) + topOffset : TerrainWorldY(x, z) + topOffset - height * 0.5f;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = new Vector3(x, centerY, z);
            box.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            box.transform.localScale = new Vector3(width, height, length);
            box.layer = 0;
            box.isStatic = true;
            box.GetComponent<Renderer>().sharedMaterial = material;
            var collider = box.GetComponent<BoxCollider>();
            collider.material = physicMaterial;
        }

        static void CreateChallengeCylinder(string name, float x, float z, float length, float diameter, Material material, PhysicMaterial physicMaterial)
        {
            float radius = diameter * 0.5f;
            float y = TerrainWorldY(x, z) + 0.155f + radius;
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.position = new Vector3(x, y, z);
            cylinder.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            cylinder.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            cylinder.layer = 0;
            cylinder.isStatic = true;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            var collider = cylinder.GetComponent<Collider>();
            collider.material = physicMaterial;
        }

        static void ReplaceDecorativeTrailCollidersWithLocalizedPhysics(PhysicMaterial material, Material roadSurfaceMaterial, Material rampDetailMaterial)
        {
            RemoveIfExists("ScoutWheelGround_PhysicalTrailSurface_Front");
            RemoveIfExists("ScoutWheelGround_PhysicalTrailSurface_Rear");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_PhysicalRoadSlab_");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_PhysicalRoadSeam_");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_PhysicalShortRamp");

            RemoveDecorativeTrailSurfaceCollidersAndMismatchedRamp();
            CreatePhysicalRoadSlabs(material, roadSurfaceMaterial);
            CreatePhysicalRoadSeams(material, roadSurfaceMaterial);
            CreatePhysicalShortRamp(material, roadSurfaceMaterial, rampDetailMaterial);
        }

        static void RemoveDecorativeTrailSurfaceCollidersAndMismatchedRamp()
        {
            int removedCount = 0;
            int removedRampCount = 0;
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if (collider == null || collider.gameObject == null)
                {
                    continue;
                }

                if (collider.gameObject.name.StartsWith("Offroad_DirtRoad_", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                    removedCount++;
                }
            }

            var oldShortRamp = GameObject.Find("Offroad_ShortRamp");
            if (oldShortRamp != null)
            {
                UnityEngine.Object.DestroyImmediate(oldShortRamp);
                removedRampCount++;
            }

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_DECORATIVE_TRAIL_COLLIDERS_REMOVED count={removedCount} mismatched_short_ramp_removed={removedRampCount}");
        }

        static void CreatePhysicalRoadSlabs(PhysicMaterial material, Material roadSurfaceMaterial)
        {
            int createdCount = 0;
            for (int i = 0; i < 9; i++)
            {
                float centerZ = RoadBlockCenterZ(i);
                float centerX = RoadBlockCenterX(i);
                float zMin = centerZ - 4.25f;
                float zMax = centerZ + 4.25f;

                createdCount += CreatePhysicalRoadSlabSegments($"ScoutWheelGround_PhysicalRoadSlab_{i:00}", centerX, zMin, zMax, material, roadSurfaceMaterial);
            }

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_LOCAL_ROAD_SLABS_READY count={createdCount} bridge_zone_excluded=true");
        }

        static int CreatePhysicalRoadSlabSegments(string baseName, float centerX, float zMin, float zMax, PhysicMaterial material, Material roadSurfaceMaterial)
        {
            int created = 0;
            float current = zMin;
            created += CreateRoadSegmentBeforeExclusion(baseName, centerX, ref current, zMax, BridgeZoneZMin, BridgeZoneZMax, material, roadSurfaceMaterial);
            created += CreateRoadSegmentBeforeExclusion(baseName, centerX, ref current, zMax, ShortRampExclusionZMin, ShortRampExclusionZMax, material, roadSurfaceMaterial);

            if (current < zMax - 0.05f)
            {
                CreatePhysicalRoadSlab(MakeRoadSegmentName(baseName, created), centerX, current, zMax, material, roadSurfaceMaterial);
                created++;
            }

            return created;
        }

        static int CreateRoadSegmentBeforeExclusion(string baseName, float centerX, ref float current, float zMax, float exclusionMin, float exclusionMax, PhysicMaterial material, Material roadSurfaceMaterial)
        {
            if (zMax <= exclusionMin || current >= exclusionMax)
            {
                return 0;
            }

            int created = 0;
            if (current < exclusionMin - 0.05f)
            {
                CreatePhysicalRoadSlab(MakeRoadSegmentName(baseName, created), centerX, current, Mathf.Min(zMax, exclusionMin), material, roadSurfaceMaterial);
                created++;
            }

            current = Mathf.Max(current, exclusionMax);
            return created;
        }

        static string MakeRoadSegmentName(string baseName, int segmentIndex)
        {
            return segmentIndex == 0 ? baseName : $"{baseName}_Part{segmentIndex:00}";
        }

        static void CreatePhysicalRoadSlab(string name, float centerX, float zMin, float zMax, PhysicMaterial material, Material roadSurfaceMaterial)
        {
            if (zMax <= zMin + 0.05f)
            {
                return;
            }

            const float roadThickness = 0.06f;
            float centerZ = (zMin + zMax) * 0.5f;
            float topY = TerrainWorldY(centerX, centerZ) + 0.062f;
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.position = new Vector3(centerX, topY - roadThickness * 0.5f, centerZ);
            slab.transform.rotation = Quaternion.identity;
            slab.transform.localScale = new Vector3(PhysicalRoadWidth, roadThickness, zMax - zMin);
            slab.layer = 0;
            slab.isStatic = true;
            slab.GetComponent<Renderer>().sharedMaterial = roadSurfaceMaterial;

            var collider = slab.GetComponent<BoxCollider>();
            collider.material = material;
        }

        static void CreatePhysicalRoadSeams(PhysicMaterial material, Material roadSurfaceMaterial)
        {
            int createdCount = 0;
            for (int i = 0; i < 8; i++)
            {
                float prevZ = RoadBlockCenterZ(i);
                float nextZ = RoadBlockCenterZ(i + 1);
                float seamStartZ = prevZ + 3.95f;
                float seamEndZ = nextZ - 3.95f;

                if (seamEndZ <= seamStartZ + 0.05f || OverlapsRoadExclusion(seamStartZ, seamEndZ))
                {
                    continue;
                }

                float prevX = RoadBlockCenterX(i);
                float nextX = RoadBlockCenterX(i + 1);
                float yStart = TerrainWorldY(prevX, prevZ) + 0.062f;
                float yEnd = TerrainWorldY(nextX, nextZ) + 0.062f;
                CreatePhysicalRampAtCenterline(
                    $"ScoutWheelGround_PhysicalRoadSeam_{i:00}_{i + 1:00}",
                    prevX,
                    nextX,
                    seamStartZ,
                    seamEndZ,
                    yStart,
                    yEnd,
                    PhysicalRoadWidth,
                    0.06f,
                    material,
                    roadSurfaceMaterial);
                createdCount++;
            }

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_LOCAL_ROAD_SEAMS_READY count={createdCount} bridge_zone_excluded=true");
        }

        static bool OverlapsRoadExclusion(float zMin, float zMax)
        {
            return zMax > BridgeZoneZMin && zMin < BridgeZoneZMax ||
                   zMax > ShortRampExclusionZMin && zMin < ShortRampExclusionZMax;
        }

        static void CreatePhysicalShortRamp(PhysicMaterial material, Material roadSurfaceMaterial, Material rampDetailMaterial)
        {
            // One continuous visible MeshCollider avoids internal vertical edges between a
            // separate ramp body and transition pieces. This remains a real visible ramp:
            // the road slab is removed from this zone, so the wheels must climb this mesh.
            // The profile deliberately keeps a clear hump; do not flatten it just to make
            // the route easier.
            const float centerX = 0f;
            const float width = 4.8f;
            const float thickness = 0.07f;
            float[] z = { -0.8f, 0.6f, 2.0f, 3.3f, 4.8f, 6.1f, 7.2f };
            float[] y =
            {
                TerrainWorldY(centerX, z[0]) + 0.062f,
                TerrainWorldY(centerX, z[1]) + 0.160f,
                TerrainWorldY(centerX, z[2]) + 0.500f,
                TerrainWorldY(centerX, z[3]) + 0.760f,
                TerrainWorldY(centerX, z[4]) + 0.620f,
                TerrainWorldY(centerX, z[5]) + 0.320f,
                TerrainWorldY(centerX, z[6]) + 0.062f,
            };

            CreateProfiledPhysicalSurface(
                "ScoutWheelGround_PhysicalShortRampContinuous",
                centerX,
                width,
                z,
                y,
                thickness,
                material,
                roadSurfaceMaterial);
            CreateShortRampVisualDetails(centerX, width, z, y, rampDetailMaterial);

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_SHORT_RAMP_PHYSICS_READY continuous=1 centerline=true x={centerX:F2} width={width:F2} z={z[0]:F2}..{z[z.Length - 1]:F2} height_delta={(MaxValue(y) - MinValue(y)):F3}");
        }

        static void CreateShortRampVisualDetails(float centerX, float width, float[] zValues, float[] yValues, Material material)
        {
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_VisibleShortRampDetail_");
            if (material == null)
            {
                return;
            }

            const int markerCount = 11;
            float zMin = zValues[0];
            float zMax = zValues[zValues.Length - 1];
            for (int i = 0; i < markerCount; i++)
            {
                float t = markerCount == 1 ? 0f : i / (float)(markerCount - 1);
                float z = Mathf.Lerp(zMin, zMax, t);
                float y = EvaluateProfileY(zValues, yValues, z) + 0.004f;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"ScoutWheelGround_VisibleShortRampDetail_CrossBand_{i:00}";
                marker.transform.position = new Vector3(centerX, y, z);
                marker.transform.rotation = Quaternion.identity;
                marker.transform.localScale = new Vector3(width, 0.006f, 0.045f);
                marker.layer = 0;
                marker.isStatic = true;
                marker.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<BoxCollider>());
            }
        }

        static void CreateProfiledPhysicalSurface(string name, float centerX, float width, float[] zValues, float[] yValues, float thickness, PhysicMaterial material, Material renderMaterial)
        {
            if (zValues == null || yValues == null || zValues.Length != yValues.Length || zValues.Length < 2)
            {
                throw new ArgumentException("Profiled physical surface requires matching z/y arrays with at least two points.");
            }

            var surface = new GameObject(name);
            surface.transform.position = Vector3.zero;
            surface.transform.rotation = Quaternion.identity;
            surface.layer = 0;
            surface.isStatic = true;

            int profileCount = zValues.Length;
            float halfWidth = width * 0.5f;
            var vertices = new Vector3[profileCount * 4];
            var uvs = new Vector2[profileCount * 4];
            for (int i = 0; i < profileCount; i++)
            {
                vertices[i * 2] = new Vector3(centerX - halfWidth, yValues[i], zValues[i]);
                vertices[i * 2 + 1] = new Vector3(centerX + halfWidth, yValues[i], zValues[i]);
                vertices[profileCount * 2 + i * 2] = new Vector3(centerX - halfWidth, yValues[i] - thickness, zValues[i]);
                vertices[profileCount * 2 + i * 2 + 1] = new Vector3(centerX + halfWidth, yValues[i] - thickness, zValues[i]);

                uvs[i * 2] = new Vector2(vertices[i * 2].x, vertices[i * 2].z);
                uvs[i * 2 + 1] = new Vector2(vertices[i * 2 + 1].x, vertices[i * 2 + 1].z);
                uvs[profileCount * 2 + i * 2] = new Vector2(vertices[profileCount * 2 + i * 2].x, vertices[profileCount * 2 + i * 2].z);
                uvs[profileCount * 2 + i * 2 + 1] = new Vector2(vertices[profileCount * 2 + i * 2 + 1].x, vertices[profileCount * 2 + i * 2 + 1].z);
            }

            var triangles = new List<int>();
            for (int i = 0; i < profileCount - 1; i++)
            {
                int topLeft0 = i * 2;
                int topRight0 = i * 2 + 1;
                int topLeft1 = (i + 1) * 2;
                int topRight1 = (i + 1) * 2 + 1;
                int bottomLeft0 = profileCount * 2 + i * 2;
                int bottomRight0 = profileCount * 2 + i * 2 + 1;
                int bottomLeft1 = profileCount * 2 + (i + 1) * 2;
                int bottomRight1 = profileCount * 2 + (i + 1) * 2 + 1;

                AddQuad(triangles, topLeft0, topLeft1, topRight0, topRight0, topLeft1, topRight1);
                AddQuad(triangles, bottomLeft0, bottomRight0, bottomLeft1, bottomRight0, bottomRight1, bottomLeft1);
                AddQuad(triangles, topLeft0, bottomLeft0, topLeft1, topLeft1, bottomLeft0, bottomLeft1);
                AddQuad(triangles, topRight0, topRight1, bottomRight0, topRight1, bottomRight1, bottomRight0);
            }

            int last = profileCount - 1;
            AddQuad(triangles, 0, 1, profileCount * 2, 1, profileCount * 2 + 1, profileCount * 2);
            AddQuad(triangles, last * 2, profileCount * 2 + last * 2, last * 2 + 1, last * 2 + 1, profileCount * 2 + last * 2, profileCount * 2 + last * 2 + 1);

            var mesh = new Mesh
            {
                name = name + "Mesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var meshFilter = surface.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var meshRenderer = surface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = renderMaterial;

            var collider = surface.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.material = material;
        }

        static float EvaluateProfileY(float[] zValues, float[] yValues, float z)
        {
            if (z <= zValues[0])
            {
                return yValues[0];
            }

            for (int i = 0; i < zValues.Length - 1; i++)
            {
                if (z <= zValues[i + 1])
                {
                    float t = Mathf.InverseLerp(zValues[i], zValues[i + 1], z);
                    return Mathf.Lerp(yValues[i], yValues[i + 1], t);
                }
            }

            return yValues[yValues.Length - 1];
        }

        static float MinValue(float[] values)
        {
            float minValue = float.PositiveInfinity;
            foreach (float value in values)
            {
                minValue = Mathf.Min(minValue, value);
            }

            return minValue;
        }

        static float MaxValue(float[] values)
        {
            float maxValue = float.NegativeInfinity;
            foreach (float value in values)
            {
                maxValue = Mathf.Max(maxValue, value);
            }

            return maxValue;
        }

        static void AddQuad(List<int> triangles, int a, int b, int c, int d, int e, int f)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(d);
            triangles.Add(e);
            triangles.Add(f);
        }

        static float RoadBlockCenterZ(int index)
        {
            return -34f + index * 8.5f;
        }

        static float RoadBlockCenterX(int index)
        {
            return 0.9f * Mathf.Sin(index * 0.85f);
        }

        static void RemoveDecorativeBridgeObjects()
        {
            var bridgeRoots = new HashSet<GameObject>();
            foreach (var gameObject in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (gameObject == null)
                {
                    continue;
                }

                Transform bridgeRoot = FindDecorativeBridgeRoot(gameObject.transform);
                if (bridgeRoot != null)
                {
                    bridgeRoots.Add(bridgeRoot.gameObject);
                }
            }

            foreach (var bridgeRoot in bridgeRoots)
            {
                UnityEngine.Object.DestroyImmediate(bridgeRoot);
            }

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_DECORATIVE_BRIDGE_OBJECTS_REMOVED count={bridgeRoots.Count}");
        }

        static void CreateDetailedPhysicalBridge(PhysicMaterial material, Material bridgeSurfaceMaterial, Material bridgeDetailMaterial)
        {
            RemoveIfExists("ScoutWheelGround_PhysicalBridgeDeck");
            RemoveIfExists("ScoutWheelGround_PhysicalBridgeFrontRamp");
            RemoveIfExists("ScoutWheelGround_PhysicalBridgeRearRamp");
            RemoveIfExists("ScoutWheelGround_VisibleBridgeLeftRail");
            RemoveIfExists("ScoutWheelGround_VisibleBridgeRightRail");
            RemoveGeneratedObjectsByPrefix("ScoutWheelGround_VisibleBridgeDetail_");

            // A narrow visible physics bridge. It is intentionally not a road-width slab:
            // the car must cross the bridge region instead of being carried by a broad flat
            // bypass surface. Visual details are added on top, but this deck remains the
            // visible load-bearing collider.
            const float bridgeWidth = PhysicalBridgeWidth;
            const float bridgeTopZMin = -9.2f;
            const float bridgeTopZMax = -4.7f;
            const float bridgeThickness = 0.08f;
            float bridgeEntryY = Mathf.Max(TerrainWorldY(0f, bridgeTopZMin), TerrainWorldY(0f, bridgeTopZMax)) + 0.08f;
            float bridgeCenterZ = (bridgeTopZMin + bridgeTopZMax) * 0.5f;
            float bridgeLength = bridgeTopZMax - bridgeTopZMin;
            float[] bridgeZ = { bridgeTopZMin, -8.25f, -6.95f, -5.65f, bridgeTopZMax };
            float[] bridgeY =
            {
                bridgeEntryY,
                bridgeEntryY + 0.070f,
                bridgeEntryY + 0.155f,
                bridgeEntryY + 0.070f,
                bridgeEntryY,
            };

            CreateProfiledPhysicalSurface(
                "ScoutWheelGround_PhysicalBridgeDeck",
                0f,
                bridgeWidth,
                bridgeZ,
                bridgeY,
                bridgeThickness,
                material,
                bridgeSurfaceMaterial);

            CreateBridgeVisualDetails(bridgeWidth, bridgeCenterZ, bridgeLength, bridgeZ, bridgeY, bridgeDetailMaterial);

            CreatePhysicalRamp(
                "ScoutWheelGround_PhysicalBridgeFrontRamp",
                -13.0f,
                bridgeTopZMin,
                TerrainWorldY(0f, -13.0f) + 0.062f,
                bridgeEntryY,
                bridgeWidth,
                bridgeThickness,
                material,
                bridgeSurfaceMaterial);
            CreatePhysicalRamp(
                "ScoutWheelGround_PhysicalBridgeRearRamp",
                bridgeTopZMax,
                -1.7f,
                bridgeEntryY,
                TerrainWorldY(0f, -1.7f) + 0.062f,
                bridgeWidth,
                bridgeThickness,
                material,
                bridgeSurfaceMaterial);

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_PHYSICAL_BRIDGE_READY deck_z={bridgeTopZMin:F1}..{bridgeTopZMax:F1} entry_y={bridgeEntryY:F3} arch_height={(MaxValue(bridgeY) - MinValue(bridgeY)):F3} ramps=true decorative_bridge_removed=true visual_details=true");
        }

        static void CreateBridgeVisualDetails(float bridgeWidth, float centerZ, float length, float[] zValues, float[] yValues, Material material)
        {
            if (material == null)
            {
                return;
            }

            float zMin = centerZ - length * 0.5f;
            float zMax = centerZ + length * 0.5f;
            for (int i = 1; i < 6; i++)
            {
                float x = -bridgeWidth * 0.5f + bridgeWidth * i / 6f;
                CreateBridgeSegmentedDetail($"ScoutWheelGround_VisibleBridgeDetail_LongGroove_{i:00}", x, 0.020f, zValues, yValues, 0.022f, 0.006f, material);
            }

            const int crossBandCount = 13;
            for (int i = 0; i < crossBandCount; i++)
            {
                float z = Mathf.Lerp(zMin + 0.20f, zMax - 0.20f, i / (float)(crossBandCount - 1));
                float y = EvaluateProfileY(zValues, yValues, z);
                CreateBridgeDetailCube($"ScoutWheelGround_VisibleBridgeDetail_CrossPlank_{i:00}", 0f, y + 0.006f, z, bridgeWidth, 0.012f, 0.055f, material);
            }

            CreateBridgeSegmentedDetail("ScoutWheelGround_VisibleBridgeDetail_LeftRail", -bridgeWidth * 0.5f, 0.112f, zValues, yValues, 0.080f, 0.120f, material, keepCollider: true);
            CreateBridgeSegmentedDetail("ScoutWheelGround_VisibleBridgeDetail_RightRail", bridgeWidth * 0.5f, 0.112f, zValues, yValues, 0.080f, 0.120f, material, keepCollider: true);
            CreateBridgeSegmentedDetail("ScoutWheelGround_VisibleBridgeDetail_LeftLowerRail", -bridgeWidth * 0.5f, 0.235f, zValues, yValues, 0.055f, 0.075f, material, keepCollider: true);
            CreateBridgeSegmentedDetail("ScoutWheelGround_VisibleBridgeDetail_RightLowerRail", bridgeWidth * 0.5f, 0.235f, zValues, yValues, 0.055f, 0.075f, material, keepCollider: true);

            const int postCount = 9;
            for (int i = 0; i < postCount; i++)
            {
                float z = Mathf.Lerp(zMin + 0.15f, zMax - 0.15f, i / (float)(postCount - 1));
                float y = EvaluateProfileY(zValues, yValues, z);
                CreateBridgeDetailCube($"ScoutWheelGround_VisibleBridgeDetail_LeftPost_{i:00}", -bridgeWidth * 0.5f, y + 0.17f, z, 0.085f, 0.34f, 0.085f, material, keepCollider: true);
                CreateBridgeDetailCube($"ScoutWheelGround_VisibleBridgeDetail_RightPost_{i:00}", bridgeWidth * 0.5f, y + 0.17f, z, 0.085f, 0.34f, 0.085f, material, keepCollider: true);
            }
        }

        static void CreateBridgeSegmentedDetail(string baseName, float x, float yOffset, float[] zValues, float[] yValues, float width, float height, Material material, bool keepCollider = false)
        {
            for (int i = 0; i < zValues.Length - 1; i++)
            {
                float z0 = zValues[i];
                float z1 = zValues[i + 1];
                float y0 = yValues[i] + yOffset;
                float y1 = yValues[i + 1] + yOffset;
                float centerZ = (z0 + z1) * 0.5f;
                float centerY = (y0 + y1) * 0.5f;
                float length = z1 - z0;
                float pitch = -Mathf.Atan2(y1 - y0, length) * Mathf.Rad2Deg;
                var detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                detail.name = $"{baseName}_{i:00}";
                detail.transform.position = new Vector3(x, centerY, centerZ);
                detail.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
                detail.transform.localScale = new Vector3(width, height, length);
                detail.layer = 0;
                detail.isStatic = true;
                detail.GetComponent<Renderer>().sharedMaterial = material;
                var collider = detail.GetComponent<BoxCollider>();
                if (!keepCollider)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }
        }

        static void CreateBridgeDetailCube(string name, float x, float y, float z, float width, float height, float length, Material material, bool keepCollider = false)
        {
            var detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            detail.name = name;
            detail.transform.position = new Vector3(x, y, z);
            detail.transform.rotation = Quaternion.identity;
            detail.transform.localScale = new Vector3(width, height, length);
            detail.layer = 0;
            detail.isStatic = true;
            detail.GetComponent<Renderer>().sharedMaterial = material;
            var collider = detail.GetComponent<BoxCollider>();
            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        static void CreatePhysicalRamp(string name, float zStart, float zEnd, float yStart, float yEnd, float width, float thickness, PhysicMaterial material, Material renderMaterial)
        {
            CreatePhysicalRampAtCenterline(name, 0f, 0f, zStart, zEnd, yStart, yEnd, width, thickness, material, renderMaterial);
        }

        static void CreatePhysicalRampAtCenterline(string name, float xStart, float xEnd, float zStart, float zEnd, float yStart, float yEnd, float width, float thickness, PhysicMaterial material, Material renderMaterial)
        {
            var ramp = new GameObject(name);
            ramp.transform.position = Vector3.zero;
            ramp.transform.rotation = Quaternion.identity;
            ramp.layer = 0;
            ramp.isStatic = true;

            float halfWidth = width * 0.5f;
            float bottomStart = yStart - thickness;
            float bottomEnd = yEnd - thickness;
            var mesh = new Mesh
            {
                name = name + "Mesh",
                vertices = new[]
                {
                    new Vector3(xStart - halfWidth, yStart, zStart),
                    new Vector3(xStart + halfWidth, yStart, zStart),
                    new Vector3(xEnd - halfWidth, yEnd, zEnd),
                    new Vector3(xEnd + halfWidth, yEnd, zEnd),
                    new Vector3(xStart - halfWidth, bottomStart, zStart),
                    new Vector3(xStart + halfWidth, bottomStart, zStart),
                    new Vector3(xEnd - halfWidth, bottomEnd, zEnd),
                    new Vector3(xEnd + halfWidth, bottomEnd, zEnd),
                },
                triangles = new[]
                {
                    0, 2, 1, 1, 2, 3,
                    4, 5, 6, 5, 7, 6,
                    0, 4, 2, 2, 4, 6,
                    1, 3, 5, 3, 7, 5,
                    0, 1, 4, 1, 5, 4,
                    2, 6, 3, 3, 6, 7,
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (renderMaterial != null)
            {
                var meshFilter = ramp.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                var meshRenderer = ramp.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = renderMaterial;
            }

            var collider = ramp.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.material = material;
        }

        static bool IsDecorativeBridgeTransform(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                string name = current.name;
                if (name.Contains("WoodBridge", StringComparison.Ordinal) ||
                    name.Contains("Kenney_bridge", StringComparison.Ordinal) ||
                    name.Contains("bridge_wood", StringComparison.Ordinal) ||
                    name.Contains("bridge_center_wood", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        static Transform FindDecorativeBridgeRoot(Transform transform)
        {
            if (!IsDecorativeBridgeTransform(transform))
            {
                return null;
            }

            Transform current = transform;
            while (current.parent != null && IsDecorativeBridgeTransform(current.parent))
            {
                current = current.parent;
            }

            return current;
        }

        static WheelCollider FindWheelCollider(GameObject root, string name)
        {
            Transform child = FindDeepChild(root.transform, name);
            return child != null ? child.GetComponent<WheelCollider>() : null;
        }

        static void AlignVisualBottomToLocalY(GameObject visualRoot, float targetLocalBottomY)
        {
            Bounds bounds = CalculateRendererBounds(visualRoot);
            float targetWorldY = visualRoot.transform.parent.position.y + targetLocalBottomY;
            visualRoot.transform.position += Vector3.up * (targetWorldY - bounds.min.y);
        }

        static string BuildVisualImportSummary(GameObject visualRoot)
        {
            Bounds bounds = CalculateRendererBounds(visualRoot);
            return string.Format(
                "VLN_SCOUT_WHEEL_GROUND_VISUAL_IMPORTED urdfLinks={0} renderers={1} colliders={2} articulationBodies={3} rigidbodies={4} bounds_size={5:F3},{6:F3},{7:F3}",
                visualRoot.GetComponentsInChildren<UrdfLink>(true).Length,
                visualRoot.GetComponentsInChildren<Renderer>(true).Length,
                visualRoot.GetComponentsInChildren<Collider>(true).Length,
                visualRoot.GetComponentsInChildren<ArticulationBody>(true).Length,
                visualRoot.GetComponentsInChildren<Rigidbody>(true).Length,
                bounds.size.x,
                bounds.size.y,
                bounds.size.z);
        }

        static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("Scout visual import produced no renderers.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        static Bounds CalculateRendererBoundsInLocalFrame(GameObject root, Transform localFrame)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"{root.name} produced no renderers.");
            }

            bool initialized = false;
            Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z),
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 localCorner = localFrame.InverseTransformPoint(corner);
                    if (!initialized)
                    {
                        bounds = new Bounds(localCorner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localCorner);
                    }
                }
            }

            return bounds;
        }

        static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform result = FindDeepChild(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
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

        static void RemoveGeneratedObjectsByPrefix(string prefix)
        {
            var objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gameObject in objects)
            {
                if (gameObject != null && gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
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

        static string ProjectRelativeToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        static void BackupExistingCandidateSceneBeforeRebuild()
        {
            string sceneFullPath = ProjectRelativeToFullPath(ScenePath);
            if (!File.Exists(sceneFullPath))
            {
                return;
            }

            try
            {
                string backupRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_SceneBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss")));
                Directory.CreateDirectory(backupRoot);

                string sceneBackupPath = Path.Combine(backupRoot, Path.GetFileName(sceneFullPath));
                File.Copy(sceneFullPath, sceneBackupPath, overwrite: true);

                string metaPath = sceneFullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Copy(metaPath, sceneBackupPath + ".meta", overwrite: true);
                }

                Debug.Log($"VLN_SCOUT_WHEEL_GROUND_SCENE_BACKUP before rebuild: {sceneBackupPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VLN_SCOUT_WHEEL_GROUND_SCENE_BACKUP_FAILED before rebuild: {ex.Message}");
            }
        }

        static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        static float TerrainWorldY(float x, float z)
        {
            return NormalizedTerrainHeight(x, z) * TerrainHeight;
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
    }
}
