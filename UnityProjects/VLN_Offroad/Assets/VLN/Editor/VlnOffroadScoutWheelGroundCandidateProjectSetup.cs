using System;
using System.Collections.Generic;
using System.IO;
using Unity.Robotics.UrdfImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnOffroadScoutWheelGroundCandidateProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity";
        const string UrdfAssetPath = "Assets/VLN/ExternalAssets/ScoutUrdfPhysics/scout_v2_unity_import.urdf";
        const string ScoutAssetRoot = "Assets/VLN/ExternalAssets/ScoutUrdfPhysics";
        const string RigName = "Offroad_SensorRig_StaticVehiclePlaceholder";
        public const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        public const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string FrictionMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_HighFriction.physicMaterial";
        const string RoadSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleRoadPhysics.mat";
        const string BridgeSurfaceMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleBridgePhysics.mat";
        const string BridgeDetailMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleBridgeDetail.mat";
        const string RampDetailMaterialPath = "Assets/VLN/Materials/ScoutWheelGround_VisibleRampDetail.mat";

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
            EnsureScoutAssetsExist();
            VlnOffroadAssetCandidateProjectSetup.BuildAssetCandidateScene();
            AssetDatabase.ImportAsset(ScoutAssetRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
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
            ReplaceDecorativeTrailCollidersWithLocalizedPhysics(frictionMaterial, roadSurfaceMaterial, rampDetailMaterial);
            RemoveDecorativeBridgeObjects();
            CreateDetailedPhysicalBridge(frictionMaterial, bridgeSurfaceMaterial, bridgeDetailMaterial);
            AssignSceneFrictionMaterial(frictionMaterial);
            var physicsRoot = CreatePhysicalScoutRoot(rig.transform.position, frictionMaterial);
            var visualRoot = ImportScoutVisualOnly(physicsRoot.transform);
            ConfigureController(physicsRoot, visualRoot);
            ConfigureRigToFollowPhysics(rig, physicsRoot.transform);
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
            SetLayerRecursively(imported, 0);
            Debug.Log(BuildVisualImportSummary(imported));
            return imported;
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
            serializedController.FindProperty("m_MaxLinearSpeedMetersPerSecond").floatValue = 2.0f;
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
            EditorUtility.SetDirty(material);
            return material;
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
            for (int i = 0; i < profileCount; i++)
            {
                vertices[i * 2] = new Vector3(centerX - halfWidth, yValues[i], zValues[i]);
                vertices[i * 2 + 1] = new Vector3(centerX + halfWidth, yValues[i], zValues[i]);
                vertices[profileCount * 2 + i * 2] = new Vector3(centerX - halfWidth, yValues[i] - thickness, zValues[i]);
                vertices[profileCount * 2 + i * 2 + 1] = new Vector3(centerX + halfWidth, yValues[i] - thickness, zValues[i]);
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
