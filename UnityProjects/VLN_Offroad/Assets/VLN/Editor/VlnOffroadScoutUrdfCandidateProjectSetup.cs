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
    public static class VlnOffroadScoutUrdfCandidateProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNOffroadScoutUrdfCandidate.unity";
        const string UrdfAssetPath = "Assets/VLN/ExternalAssets/ScoutUrdfPhysics/scout_v2_unity_import.urdf";
        const string ScoutAssetRoot = "Assets/VLN/ExternalAssets/ScoutUrdfPhysics";
        const string RigName = "Offroad_SensorRig_StaticVehiclePlaceholder";
        public const string ScoutRootName = "ScoutUrdf_Root";
        const float GroundClearanceMeters = 0.025f;

        [MenuItem("VLN/Build Offroad Scout URDF Candidate Scene")]
        public static void BuildScoutUrdfCandidateScene()
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

            var rig = GameObject.Find(RigName);
            if (rig == null)
            {
                throw new InvalidOperationException($"Missing sensor rig: {RigName}");
            }

            ReplacePlaceholderWithScoutUrdf(rig.transform);
            ConfigureScoutCandidateCamera(rig.transform);
            ConfigureVehicleCollisionEnvelope(rig);
            ConfigureOdomPublisher(rig);

            var controller = new GameObject("VLN_OffroadScoutUrdfCandidate_SmokeTestController");
            controller.AddComponent<VlnOffroadScoutUrdfCandidateSmokeTest>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_OFFROAD_SCOUT_URDF_CANDIDATE_SETUP saved scene at {ScenePath}");
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

        static void ReplacePlaceholderWithScoutUrdf(Transform rig)
        {
            DestroyChildIfExists(rig, "Offroad_VehiclePlaceholder_Body");
            DestroyChildIfExists(rig, "HuskyVisual_Root");
            DestroyChildIfExists(rig, ScoutRootName);
            RemoveIfExists("scout_v2");

            GameObject scout = ImportScoutUrdf();
            scout.name = ScoutRootName;
            scout.transform.SetParent(rig, false);
            scout.transform.localPosition = Vector3.zero;
            scout.transform.localRotation = Quaternion.identity;
            scout.transform.localScale = Vector3.one;

            DisableImportedController(scout);
            StabilizeImportedPhysicsForFirstPass(scout);
            ConfigureWheelJointCommandProbe(scout);
            AlignBottomToRigGround(scout, rig.position.y + GroundClearanceMeters);
            SetLayerRecursively(scout, 0);

            Debug.Log(BuildScoutImportSummary(scout));
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

        static void DisableImportedController(GameObject scout)
        {
            var controller = scout.GetComponent<Unity.Robotics.UrdfImporter.Control.Controller>();
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        static void StabilizeImportedPhysicsForFirstPass(GameObject scout)
        {
            foreach (var body in scout.GetComponentsInChildren<Rigidbody>(true))
            {
                body.useGravity = false;
                body.isKinematic = true;
                body.detectCollisions = true;
            }

            foreach (var body in scout.GetComponentsInChildren<ArticulationBody>(true))
            {
                body.useGravity = false;
                body.linearDamping = Mathf.Max(body.linearDamping, 0.25f);
                body.angularDamping = Mathf.Max(body.angularDamping, 0.25f);
            }

            foreach (var collider in scout.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
        }

        static void AlignBottomToRigGround(GameObject scout, float targetBottomY)
        {
            Bounds bounds = CalculateRenderableOrColliderBounds(scout);
            float deltaY = targetBottomY - bounds.min.y;
            scout.transform.position += Vector3.up * deltaY;
        }

        static void ConfigureScoutCandidateCamera(Transform rig)
        {
            DestroyChildIfExists(rig, "ScoutUrdfCandidate_GameCamera");

            var overview = GameObject.Find("Offroad_ViewerCamera");
            if (overview != null && overview.TryGetComponent<Camera>(out var overviewCamera))
            {
                overviewCamera.depth = -10f;
            }

            var cameraObject = new GameObject("ScoutUrdfCandidate_GameCamera");
            cameraObject.transform.SetParent(rig, false);
            cameraObject.transform.localPosition = new Vector3(2.2f, 1.35f, -2.65f);
            cameraObject.transform.LookAt(rig.position + Vector3.up * 0.45f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 42f;
            camera.depth = 10f;

            var controller = cameraObject.AddComponent<VlnRuntimeMapCameraController>();
            controller.Configure(ScoutRootName, new Vector3(0f, 0.45f, 0f), 1.15f, 44.0f);
        }

        static void ConfigureVehicleCollisionEnvelope(GameObject rig)
        {
            var tfPublisher = rig.GetComponent<VlnVehicleTfPublisher>();
            if (tfPublisher == null)
            {
                return;
            }

            var serializedPublisher = new SerializedObject(tfPublisher);
            serializedPublisher.FindProperty("m_CollisionHalfExtents").vector3Value = new Vector3(0.38f, 0.30f, 0.58f);
            serializedPublisher.FindProperty("m_CollisionCenterHeight").floatValue = 0.34f;
            serializedPublisher.FindProperty("m_EnableObstacleCollisionStop").boolValue = true;
            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureOdomPublisher(GameObject rig)
        {
            var odomPublisher = rig.GetComponent<VlnOdomPublisher>();
            if (odomPublisher == null)
            {
                odomPublisher = rig.AddComponent<VlnOdomPublisher>();
            }

            odomPublisher.enabled = true;
        }

        static void ConfigureWheelJointCommandProbe(GameObject scout)
        {
            var probe = scout.GetComponent<VlnScoutWheelJointCommandProbe>();
            if (probe == null)
            {
                probe = scout.AddComponent<VlnScoutWheelJointCommandProbe>();
            }
            probe.enabled = true;
        }

        static string BuildScoutImportSummary(GameObject scout)
        {
            Bounds bounds = CalculateRenderableOrColliderBounds(scout);
            return string.Format(
                "VLN_SCOUT_URDF_IMPORTED links={0} joints={1} inertials={2} urdfCollisions={3} unityColliders={4} renderers={5} articulationBodies={6} rigidbodies={7} bounds_size={8:F3},{9:F3},{10:F3}",
                scout.GetComponentsInChildren<UrdfLink>(true).Length,
                scout.GetComponentsInChildren<UrdfJoint>(true).Length,
                scout.GetComponentsInChildren<UrdfInertial>(true).Length,
                scout.GetComponentsInChildren<UrdfCollision>(true).Length,
                scout.GetComponentsInChildren<Collider>(true).Length,
                scout.GetComponentsInChildren<Renderer>(true).Length,
                scout.GetComponentsInChildren<ArticulationBody>(true).Length,
                scout.GetComponentsInChildren<Rigidbody>(true).Length,
                bounds.size.x,
                bounds.size.y,
                bounds.size.z);
        }

        static Bounds CalculateRenderableOrColliderBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                return bounds;
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                throw new InvalidOperationException("Scout URDF import produced no renderers or colliders.");
            }

            Bounds colliderBounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                colliderBounds.Encapsulate(colliders[i].bounds);
            }
            return colliderBounds;
        }

        static string ProjectRelativeToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
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
