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

        const float TerrainSize = 80f;
        const float TerrainHeight = 7f;
        const float WheelRadius = 0.16459f;
        const float WheelBase = 0.498f;
        const float Track = 0.58306f;
        const float InitialGroundClearance = 0f;

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
            RemoveDecorativeRoadColliders();
            RemoveDecorativeBridgeColliders();
            CreateSimplifiedPhysicalBridge(frictionMaterial);
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
            body.centerOfMass = new Vector3(0f, 0.29f, 0.02f);
            body.drag = 0.12f;
            body.angularDrag = 0.55f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var chassis = root.AddComponent<BoxCollider>();
            chassis.center = new Vector3(0f, 0.37f, 0f);
            chassis.size = new Vector3(0.68f, 0.24f, 0.92f);
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
            wheelObject.transform.localPosition = new Vector3(localX, WheelRadius + 0.045f, localZ);
            wheelObject.transform.localRotation = Quaternion.identity;
            wheelObject.layer = 0;

            var wheel = wheelObject.AddComponent<WheelCollider>();
            wheel.radius = WheelRadius;
            wheel.mass = 3f;
            wheel.wheelDampingRate = 0.38f;
            wheel.suspensionDistance = 0.14f;
            wheel.forceAppPointDistance = 0.02f;

            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 18000f;
            spring.damper = 3400f;
            spring.targetPosition = 0.55f;
            wheel.suspensionSpring = spring;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.extremumSlip = 0.35f;
            forward.extremumValue = 1.6f;
            forward.asymptoteSlip = 0.80f;
            forward.asymptoteValue = 1.05f;
            forward.stiffness = 2.25f;
            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.extremumSlip = 0.28f;
            sideways.extremumValue = 1.05f;
            sideways.asymptoteSlip = 0.70f;
            sideways.asymptoteValue = 0.72f;
            sideways.stiffness = 1.25f;
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
            serializedController.FindProperty("m_WheelMotorDirection").floatValue = -1f;
            serializedController.FindProperty("m_WheelVisualVerticalOffset").floatValue = 0.085f;
            serializedController.FindProperty("m_MaxLinearSpeedMetersPerSecond").floatValue = 2.0f;
            serializedController.FindProperty("m_MaxAngularSpeedRadPerSecond").floatValue = 1.0f;
            serializedController.FindProperty("m_MaxMotorTorque").floatValue = 140f;
            serializedController.FindProperty("m_MaxBrakeTorque").floatValue = 220f;
            serializedController.FindProperty("m_RpmVelocityGain").floatValue = 1.35f;
            serializedController.FindProperty("m_LongitudinalAssistGain").floatValue = 1.50f;
            serializedController.FindProperty("m_MaxLongitudinalAssistAcceleration").floatValue = 1.20f;
            serializedController.FindProperty("m_LongitudinalOverspeedMargin").floatValue = 0.35f;
            serializedController.FindProperty("m_OverspeedBrakeTorqueRatio").floatValue = 0.25f;
            serializedController.FindProperty("m_RollingBrakeSpeedThreshold").floatValue = 0.08f;
            serializedController.FindProperty("m_CommandTimeoutSeconds").floatValue = 0.75f;
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

        static void RemoveDecorativeRoadColliders()
        {
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if (collider == null || collider.gameObject == null)
                {
                    continue;
                }

                if (collider.gameObject.name.StartsWith("Offroad_DirtRoad_", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }
        }

        static void RemoveDecorativeBridgeColliders()
        {
            int removedCount = 0;
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if (collider == null || collider.gameObject == null)
                {
                    continue;
                }

                if (IsDecorativeBridgeTransform(collider.transform))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                    removedCount++;
                }
            }

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_BRIDGE_COLLIDERS_REMOVED count={removedCount}");
        }

        static void CreateSimplifiedPhysicalBridge(PhysicMaterial material)
        {
            RemoveIfExists("ScoutWheelGround_PhysicalBridgeDeck");
            RemoveIfExists("ScoutWheelGround_PhysicalBridgeFrontRamp");
            RemoveIfExists("ScoutWheelGround_PhysicalBridgeRearRamp");

            const float bridgeWidth = 3.3f;
            const float bridgeTopZMin = -9.2f;
            const float bridgeTopZMax = -4.7f;
            const float bridgeThickness = 0.08f;
            float bridgeTopY = Mathf.Max(TerrainWorldY(0f, bridgeTopZMin), TerrainWorldY(0f, bridgeTopZMax)) + 0.08f;
            float bridgeCenterZ = (bridgeTopZMin + bridgeTopZMax) * 0.5f;
            float bridgeLength = bridgeTopZMax - bridgeTopZMin;

            var bridge = new GameObject("ScoutWheelGround_PhysicalBridgeDeck");
            bridge.transform.position = new Vector3(0f, bridgeTopY - bridgeThickness * 0.5f, bridgeCenterZ);
            bridge.transform.rotation = Quaternion.identity;
            bridge.layer = 0;

            var collider = bridge.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(bridgeWidth, bridgeThickness, bridgeLength);
            collider.material = material;

            CreatePhysicalRamp(
                "ScoutWheelGround_PhysicalBridgeFrontRamp",
                -12.2f,
                bridgeTopZMin,
                TerrainWorldY(0f, -12.2f) + 0.04f,
                bridgeTopY,
                bridgeWidth,
                bridgeThickness,
                material);
            CreatePhysicalRamp(
                "ScoutWheelGround_PhysicalBridgeRearRamp",
                bridgeTopZMax,
                -2.2f,
                bridgeTopY,
                TerrainWorldY(0f, -2.2f) + 0.04f,
                bridgeWidth,
                bridgeThickness,
                material);

            Debug.Log($"VLN_SCOUT_WHEEL_GROUND_PHYSICAL_BRIDGE_READY deck_z={bridgeTopZMin:F1}..{bridgeTopZMax:F1} top_y={bridgeTopY:F3} ramps=true");
        }

        static void CreatePhysicalRamp(string name, float zStart, float zEnd, float yStart, float yEnd, float width, float thickness, PhysicMaterial material)
        {
            var ramp = new GameObject(name);
            ramp.transform.position = Vector3.zero;
            ramp.transform.rotation = Quaternion.identity;
            ramp.layer = 0;

            float halfWidth = width * 0.5f;
            float bottomStart = yStart - thickness;
            float bottomEnd = yEnd - thickness;
            var mesh = new Mesh
            {
                name = name + "Mesh",
                vertices = new[]
                {
                    new Vector3(-halfWidth, yStart, zStart),
                    new Vector3( halfWidth, yStart, zStart),
                    new Vector3(-halfWidth, yEnd, zEnd),
                    new Vector3( halfWidth, yEnd, zEnd),
                    new Vector3(-halfWidth, bottomStart, zStart),
                    new Vector3( halfWidth, bottomStart, zStart),
                    new Vector3(-halfWidth, bottomEnd, zEnd),
                    new Vector3( halfWidth, bottomEnd, zEnd),
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
