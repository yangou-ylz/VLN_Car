using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VLN.ROS2;

namespace VLN.Editor
{
    public static class VlnMesaTopgearVehicleCandidateBuilder
    {
        public const string CandidateScenePath = "Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity";
        const string MesaScenePath = "Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity";
        const string SourceVehicleScenePath = "Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity";
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string RigName = "Offroad_SensorRig_StaticVehiclePlaceholder";
        const string RosConnectionName = "ROSConnection";
        const string ViewerCameraName = "VLN_MesaTopgearVehicle_ReviewCamera";
        const string SmokeControllerName = "VLN_MesaTopgearVehicle_SmokeTestController";
        const string SpawnMarkerName = "VLN_MesaTopgearVehicle_Spawn";
        const string MesaSandPhysicMaterialPath = "Assets/VLN/Materials/HighPrecisionDesert/VLN_Mesa_SandTerrain.physicMaterial";
        const string MesaRockSlidePhysicMaterialPath = "Assets/VLN/Materials/HighPrecisionDesert/VLN_Mesa_RockCliff_Slide.physicMaterial";
        static int s_FocusSceneViewAttempts;

        [MenuItem("VLN/Mesa Desert/Build Topgear Vehicle Candidate Scene")]
        public static void BuildCandidateFromMenu()
        {
            BuildCandidateScene();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_CANDIDATE_BUILT " + CandidateScenePath);
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

            VlnTopgearCameraDataPoseTuner.EnsureDecoupledIfSavedStateRequiresIt(saveScene: false, showDialog: false);
            VlnTopgearUpperAssemblyTuner.ApplySavedAssemblyIfPresent(saveScene: false, showDialog: false);
            VlnTopgearCameraDataPoseTuner.ApplySavedCameraDataPosesIfPresent(saveScene: false, showDialog: false);
            var sensorConfig = VlnTopgearFisheyeSensorConfig.ApplyCurrentSceneSensorConfig(saveScene: true);
            if (!sensorConfig.Success)
            {
                throw new InvalidOperationException("Mesa Topgear manual open sensor config failed: " + sensorConfig.Summary);
            }

            ScheduleFocusSceneViewOnVehicle();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_CANDIDATE_OPENED " + CandidateScenePath);
        }

        [MenuItem("VLN/Mesa Desert/Focus Topgear Vehicle In Scene View")]
        public static void FocusTopgearVehicleInSceneViewFromMenu()
        {
            ScheduleFocusSceneViewOnVehicle();
        }

        static void ScheduleFocusSceneViewOnVehicle()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            s_FocusSceneViewAttempts = 0;
            EditorApplication.update -= FocusSceneViewOnVehicleTick;
            EditorApplication.update += FocusSceneViewOnVehicleTick;
        }

        static void FocusSceneViewOnVehicleTick()
        {
            s_FocusSceneViewAttempts++;
            GameObject target = GameObject.Find(PhysicsRootName) ?? GameObject.Find(ViewerCameraName) ?? GameObject.Find(SpawnMarkerName);
            bool hasSceneView = SceneView.sceneViews != null && SceneView.sceneViews.Count > 0;
            if (target != null && hasSceneView)
            {
                Selection.activeGameObject = target;
                foreach (SceneView sceneView in SceneView.sceneViews)
                {
                    if (sceneView == null)
                    {
                        continue;
                    }
                    sceneView.FrameSelected();
                    sceneView.Repaint();
                }
                EditorApplication.update -= FocusSceneViewOnVehicleTick;
                Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_SCENE_VIEW_FOCUSED target=" + target.name);
                return;
            }

            if (s_FocusSceneViewAttempts >= 180)
            {
                EditorApplication.update -= FocusSceneViewOnVehicleTick;
                Debug.LogWarning("VLN_MESA_TOPGEAR_VEHICLE_SCENE_VIEW_FOCUS_SKIPPED target_found=" + (target != null ? "1" : "0") + " scene_view_found=" + (hasSceneView ? "1" : "0"));
            }
        }

        public static void RunBuildAndPhysicsSmokeTest()
        {
            BuildCandidateScene();
            ConfigureSmokeAutoExit(22f);
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_PHYSICS_SMOKE_ENTER_PLAYMODE");
        }

        public static void RunBuildAndCmdVelSmokeTest()
        {
            BuildCandidateScene();
            ConfigureSmokeAutoExit(78f);
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_CMD_VEL_SMOKE_ENTER_PLAYMODE");
        }

        public static void RunExistingSceneSensorRateSmokeTest()
        {
            EnsureSceneExists(CandidateScenePath);
            EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
            VlnTopgearCameraDataPoseTuner.EnsureDecoupledIfSavedStateRequiresIt(saveScene: false, showDialog: false);
            VlnTopgearUpperAssemblyTuner.ApplySavedAssemblyIfPresent(saveScene: false, showDialog: false);
            VlnTopgearCameraDataPoseTuner.ApplySavedCameraDataPosesIfPresent(saveScene: false, showDialog: false);
            var sensorConfig = VlnTopgearFisheyeSensorConfig.ApplyCurrentSceneSensorConfig(saveScene: false);
            if (!sensorConfig.Success)
            {
                throw new InvalidOperationException("Mesa Topgear existing scene sensor config failed: " + sensorConfig.Summary);
            }
            ConfigureSmokeAutoExit(42f, saveScene: false, forceSuccessfulBatchExit: false);
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_SENSOR_RATE_SMOKE_ENTER_PLAYMODE");
        }

        public static void RunBuildAndObstacleImpactSmokeTest()
        {
            BuildCandidateScene();
            PrepareObstacleImpactProbe();
            ConfigureSmokeAutoExit(88f, saveScene: false);
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_OBSTACLE_IMPACT_SMOKE_ENTER_PLAYMODE");
        }

        public static void RunBuildAndCliffDropSmokeTest()
        {
            BuildCandidateScene();
            PrepareCliffDropProbe();
            ConfigureSmokeAutoExit(78f, saveScene: false, forceSuccessfulBatchExit: true);
            EditorApplication.EnterPlaymode();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_CLIFF_DROP_SMOKE_ENTER_PLAYMODE");
        }

        public static BuildResult BuildCandidateScene()
        {
            EnsureSceneExists(MesaScenePath);
            EnsureSceneExists(SourceVehicleScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(CandidateScenePath)) ?? string.Empty);

            var scene = EditorSceneManager.OpenScene(MesaScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, CandidateScenePath, saveAsCopy: false))
            {
                throw new InvalidOperationException("Could not save Mesa scene copy as " + CandidateScenePath);
            }
            scene = EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);

            RemoveIfExists(PhysicsRootName);
            RemoveIfExists(RigName);
            RemoveIfExists(RosConnectionName);
            RemoveIfExists(ViewerCameraName);
            RemoveIfExists(SmokeControllerName);
            RemoveIfExists(SpawnMarkerName);

            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Mesa Topgear candidate requires a Terrain with TerrainData.");
            }
            Physics.SyncTransforms();

            var routePoints = ReadRouteWaypoints();
            var spawnContext = BuildSpawnSearchContext();
            var spawn = FindFlatSpawnPoint(terrain, routePoints, spawnContext);
            Quaternion spawnRotation = RouteAlignedRotation(routePoints, spawn.Position);
            ApplyMesaPhysicsMaterials();

            Scene sourceScene = EditorSceneManager.OpenScene(SourceVehicleScenePath, OpenSceneMode.Additive);
            GameObject physicsRoot = DuplicateRootFromScene(sourceScene, PhysicsRootName, scene);
            GameObject rig = DuplicateRootFromScene(sourceScene, RigName, scene);
            GameObject rosConnection = TryDuplicateRootFromScene(sourceScene, RosConnectionName, scene);
            EditorSceneManager.CloseScene(sourceScene, removeScene: true);

            physicsRoot.transform.SetPositionAndRotation(spawn.Position, spawnRotation);
            RebindVehicleForMesa(physicsRoot, rig, spawnRotation);
            VlnTopgearCameraDataPoseTuner.EnsureDecoupledIfSavedStateRequiresIt(saveScene: false, showDialog: false);
            VlnTopgearUpperAssemblyTuner.ApplySavedAssemblyIfPresent(saveScene: false, showDialog: false);
            VlnTopgearCameraDataPoseTuner.ApplySavedCameraDataPosesIfPresent(saveScene: false, showDialog: false);
            var sensorConfig = VlnTopgearFisheyeSensorConfig.ApplyCurrentSceneSensorConfig(saveScene: false);
            if (!sensorConfig.Success)
            {
                throw new InvalidOperationException("Mesa Topgear sensor fisheye/high-frequency config failed: " + sensorConfig.Summary);
            }
            if (rosConnection != null)
            {
                rosConnection.name = RosConnectionName;
            }

            CreateSpawnMarker(spawn.Position);
            CreateReviewCamera(spawn.Position, spawnRotation);
            CreateSmokeController();

            var result = new BuildResult
            {
                ScenePath = CandidateScenePath,
                SourceWorldScenePath = MesaScenePath,
                SourceVehicleScenePath = SourceVehicleScenePath,
                SpawnPosition = spawn.Position,
                SpawnSlopeDegrees = spawn.SlopeDegrees,
                SpawnHeightRangeMeters = spawn.HeightRangeMeters,
                SpawnValleyWallReliefMeters = spawn.ValleyWallReliefMeters,
                SpawnNearestWaterDistanceMeters = spawn.NearestWaterDistanceMeters,
                SpawnWaterSurfaceClearanceMeters = spawn.WaterSurfaceClearanceMeters,
                SpawnNearestCactusDistanceMeters = spawn.NearestCactusDistanceMeters,
                SpawnNearbyCactusCount = spawn.NearbyCactusCount,
                SpawnObstacleCount = spawn.ObstacleCount,
                SpawnScore = spawn.Score,
                WheelColliderCount = physicsRoot.GetComponentsInChildren<WheelCollider>(true).Length,
                RigidbodyCount = physicsRoot.GetComponentsInChildren<Rigidbody>(true).Length,
                VisualRendererCount = CountRenderers(physicsRoot, VisualRootName),
                SensorCameraCount = CountTopgearSensorCameras(rig),
                SensorLidarCount = CountDeepChildren(rig, "LiDAR"),
                TerrainColliderCount = UnityEngine.Object.FindObjectsOfType<TerrainCollider>(true).Length,
                SceneColliderCount = UnityEngine.Object.FindObjectsOfType<Collider>(true).Length,
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CandidateScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteConfig(result, routePoints);
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_CANDIDATE_READY spawn=" + FormatVector(result.SpawnPosition) + " slope=" + result.SpawnSlopeDegrees.ToString("F2", CultureInfo.InvariantCulture));
            return result;
        }

        static void RebindVehicleForMesa(GameObject physicsRoot, GameObject rig, Quaternion spawnRotation)
        {
            if (physicsRoot == null || rig == null)
            {
                throw new InvalidOperationException("Vehicle copy failed: missing physics root or sensor rig.");
            }

            rig.transform.SetPositionAndRotation(physicsRoot.transform.position, spawnRotation);

            var follower = rig.GetComponent<VlnFollowTransformPose>();
            if (follower == null)
            {
                follower = rig.AddComponent<VlnFollowTransformPose>();
            }
            follower.Configure(physicsRoot.transform, Vector3.zero, true);

            var tfPublisher = rig.GetComponent<VlnVehicleTfPublisher>();
            if (tfPublisher != null)
            {
                var serialized = new SerializedObject(tfPublisher);
                SetBool(serialized, "m_EnableKinematicMotion", false);
                SetBool(serialized, "m_EnableObstacleCollisionStop", false);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var odom = rig.GetComponent<VlnOdomPublisher>();
            if (odom == null)
            {
                odom = rig.AddComponent<VlnOdomPublisher>();
            }
            odom.enabled = true;

            var controller = physicsRoot.GetComponent<VlnScoutWheelGroundController>();
            if (controller == null)
            {
                throw new InvalidOperationException("Copied vehicle physics root is missing VlnScoutWheelGroundController.");
            }
            var serializedController = new SerializedObject(controller);
            SetBool(serializedController, "m_TreatTerrainContactAsSand", true);
            SetBool(serializedController, "m_RelaxStopDampingOnSteepOrAirborne", true);
            SetFloat(serializedController, "m_RelaxStopDampingSlopeDegrees", 28f);
            SetBool(serializedController, "m_RelaxWheelFrictionOnSteepOrAirborne", true);
            SetFloat(serializedController, "m_RelaxedWheelFrictionStiffnessScale", 0.30f);
            SetBool(serializedController, "m_RelaxWheelSuspensionOnSteepOrAirborne", true);
            SetFloat(serializedController, "m_RelaxedWheelSuspensionSpringScale", 0.08f);
            SetFloat(serializedController, "m_RelaxedWheelSuspensionDamperScale", 0.25f);
            SetBool(serializedController, "m_EnableSteepSlopeGravityAssist", true);
            SetFloat(serializedController, "m_SteepSlopeGravityAssistAcceleration", 5.5f);
            SetFloat(serializedController, "m_WheelVisualVerticalOffset", 0.0f);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            var issueRecorder = physicsRoot.GetComponent<VlnMesaTopgearIssueRecorder>();
            if (issueRecorder == null)
            {
                issueRecorder = physicsRoot.AddComponent<VlnMesaTopgearIssueRecorder>();
            }
            issueRecorder.enabled = true;

            var body = physicsRoot.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.drag = 0.08f;
                body.angularDrag = 0.12f;
                body.maxAngularVelocity = 18f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        static void PrepareObstacleImpactProbe()
        {
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            var physicsRoot = GameObject.Find(PhysicsRootName);
            var rig = GameObject.Find(RigName);
            if (terrain == null || physicsRoot == null || rig == null)
            {
                throw new InvalidOperationException("Cannot prepare Mesa obstacle impact probe: missing terrain, physics root, or rig.");
            }

            if (!TryFindObstacleImpactSetup(terrain, physicsRoot.transform, rig.transform, out var setup))
            {
                throw new InvalidOperationException("Could not find a reachable Mesa obstacle for impact probe.");
            }

            physicsRoot.transform.SetPositionAndRotation(setup.StartPosition, setup.StartRotation);
            RebindVehicleForMesa(physicsRoot, rig, setup.StartRotation);

            var existing = physicsRoot.GetComponent<VlnMesaTopgearObstacleImpactProbe>();
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
            var probe = physicsRoot.AddComponent<VlnMesaTopgearObstacleImpactProbe>();
            probe.Configure(setup.TargetCollider, setup.TargetPoint, setup.TargetName, setup.InitialDistanceMeters);

            var marker = GameObject.Find("VLN_MesaTopgearVehicle_ImpactTarget");
            if (marker == null)
            {
                marker = new GameObject("VLN_MesaTopgearVehicle_ImpactTarget");
            }
            marker.transform.position = setup.TargetPoint;

            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_OBSTACLE_IMPACT_SETUP target=" + setup.TargetName + " start=" + FormatVector(setup.StartPosition) + " target_point=" + FormatVector(setup.TargetPoint) + " distance=" + setup.InitialDistanceMeters.ToString("F2", CultureInfo.InvariantCulture));
        }

        static void PrepareCliffDropProbe()
        {
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            var physicsRoot = GameObject.Find(PhysicsRootName);
            var rig = GameObject.Find(RigName);
            if (terrain == null || physicsRoot == null || rig == null)
            {
                throw new InvalidOperationException("Cannot prepare Mesa cliff drop probe: missing terrain, physics root, or rig.");
            }

            if (!TryFindCliffDropSetup(terrain, out var setup))
            {
                throw new InvalidOperationException("Could not find a real Mesa cliff edge for cliff drop probe.");
            }

            physicsRoot.transform.SetPositionAndRotation(setup.StartPosition, setup.StartRotation);
            RebindVehicleForMesa(physicsRoot, rig, setup.StartRotation);

            var existing = physicsRoot.GetComponent<VlnMesaTopgearCliffDropProbe>();
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
            var probe = physicsRoot.AddComponent<VlnMesaTopgearCliffDropProbe>();
            probe.Configure(setup.TargetPoint, setup.ExpectedDropMeters, setup.MaxEdgeSlopeDegrees, setup.SampleDistanceMeters);

            var marker = GameObject.Find("VLN_MesaTopgearVehicle_CliffDropTarget");
            if (marker == null)
            {
                marker = new GameObject("VLN_MesaTopgearVehicle_CliffDropTarget");
            }
            marker.transform.position = setup.TargetPoint;

            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_CLIFF_DROP_SETUP start=" + FormatVector(setup.StartPosition) + " target=" + FormatVector(setup.TargetPoint) + " expected_drop=" + setup.ExpectedDropMeters.ToString("F2", CultureInfo.InvariantCulture) + " edge_slope=" + setup.MaxEdgeSlopeDegrees.ToString("F2", CultureInfo.InvariantCulture));
        }

        static void ConfigureSmokeAutoExit(float seconds, bool saveScene = true, bool forceSuccessfulBatchExit = false)
        {
            var smokeObject = GameObject.Find(SmokeControllerName);
            var smoke = smokeObject != null ? smokeObject.GetComponent<VlnMesaTopgearVehicleSmokeTest>() : null;
            if (smoke == null)
            {
                throw new InvalidOperationException("Missing Mesa Topgear smoke test controller.");
            }
            var serialized = new SerializedObject(smoke);
            serialized.FindProperty("m_BatchModeAutoExitAfterSeconds").floatValue = seconds;
            var forceSuccess = serialized.FindProperty("m_ForceSuccessfulBatchExit");
            if (forceSuccess != null)
            {
                forceSuccess.boolValue = forceSuccessfulBatchExit;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (saveScene)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), CandidateScenePath);
            }
        }

        static void ApplyMesaPhysicsMaterials()
        {
            var sand = EnsureMesaSandPhysicMaterial();
            var rock = EnsureMesaRockSlidePhysicMaterial();
            foreach (var terrainCollider in UnityEngine.Object.FindObjectsOfType<TerrainCollider>(true))
            {
                if (sand != null)
                {
                    terrainCollider.sharedMaterial = sand;
                }
            }

            if (rock == null)
            {
                return;
            }
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
            {
                if (collider == null || collider is TerrainCollider || collider.sharedMaterial != null)
                {
                    continue;
                }

                string name = collider.gameObject.name.ToLowerInvariant();
                if (name.Contains("rock") || name.Contains("boulder") || name.Contains("rubble") || name.Contains("cliff") || name.Contains("strate") || name.Contains("stone"))
                {
                    collider.sharedMaterial = rock;
                }
            }
        }

        static PhysicMaterial EnsureMesaSandPhysicMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(MesaSandPhysicMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(MesaSandPhysicMaterialPath)) ?? string.Empty);
                material = new PhysicMaterial("VLN_Mesa_SandTerrain");
                AssetDatabase.CreateAsset(material, MesaSandPhysicMaterialPath);
            }

            material.dynamicFriction = 0.48f;
            material.staticFriction = 0.58f;
            material.bounciness = 0.01f;
            material.frictionCombine = PhysicMaterialCombine.Average;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static PhysicMaterial EnsureMesaRockSlidePhysicMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(MesaRockSlidePhysicMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(MesaRockSlidePhysicMaterialPath)) ?? string.Empty);
                material = new PhysicMaterial("VLN_Mesa_RockCliff_Slide");
                AssetDatabase.CreateAsset(material, MesaRockSlidePhysicMaterialPath);
            }

            material.dynamicFriction = 0.42f;
            material.staticFriction = 0.55f;
            material.bounciness = 0.02f;
            material.frictionCombine = PhysicMaterialCombine.Average;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static SpawnCandidate FindFlatSpawnPoint(Terrain terrain, List<Vector3> routePoints, SpawnSearchContext context)
        {
            var seeds = new List<Vector2>();
            foreach (var point in routePoints.OrderBy(p => p.y).Take(5))
            {
                seeds.Add(new Vector2(point.x, point.z));
            }

            foreach (var cactus in context.CactusPositions)
            {
                Vector2 center = new Vector2(cactus.x, cactus.z);
                seeds.Add(center);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / 8f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    seeds.Add(center + direction * 18f);
                    seeds.Add(center + direction * 42f);
                }
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            for (float nx = 0.18f; nx <= 0.82f; nx += 0.16f)
            {
                for (float nz = 0.18f; nz <= 0.82f; nz += 0.16f)
                {
                    seeds.Add(new Vector2(origin.x + size.x * nx, origin.z + size.z * nz));
                }
            }

            SpawnCandidate best = default;
            bool found = false;
            foreach (var seed in seeds)
            {
                for (float radius = 0f; radius <= 260f; radius += 13f)
                {
                    int samples = radius < 0.1f ? 1 : Mathf.Max(12, Mathf.RoundToInt(radius / 6f));
                    for (int i = 0; i < samples; i++)
                    {
                        float angle = samples == 1 ? 0f : i * Mathf.PI * 2f / samples;
                        Vector2 xz = seed + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        if (!TryEvaluateSpawn(terrain, xz, routePoints, context, requireCactusNearby: true, out var candidate))
                        {
                            continue;
                        }
                        if (!found || candidate.Score < best.Score)
                        {
                            best = candidate;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("Could not find a flat Mesa sand spawn point for Topgear vehicle.");
            }
            return best;
        }

        static bool TryEvaluateSpawn(Terrain terrain, Vector2 xz, List<Vector3> routePoints, SpawnSearchContext context, bool requireCactusNearby, out SpawnCandidate candidate)
        {
            candidate = default;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = (xz.x - origin.x) / size.x;
            float nz = (xz.y - origin.z) / size.z;
            if (nx < 0.06f || nx > 0.94f || nz < 0.06f || nz > 0.94f)
            {
                return false;
            }

            float slope = terrain.terrainData.GetSteepness(nx, nz);
            if (slope > 7.5f)
            {
                return false;
            }

            float centerY = TerrainHeight(terrain, xz);
            float heightRange = HeightRangeAround(terrain, xz, 2.2f);
            if (heightRange > 0.18f)
            {
                return false;
            }

            int obstacleCount = CountBlockingColliders(new Vector3(xz.x, centerY, xz.y), new Vector3(2.2f, 2.0f, 2.2f));
            if (obstacleCount > 0)
            {
                return false;
            }

            float routeDistance = routePoints.Count > 0 ? routePoints.Min(p => Vector2.Distance(new Vector2(p.x, p.z), xz)) : 0f;
            float valleyWallRelief = ValleyWallReliefAround(terrain, xz, 55f);
            if (valleyWallRelief < 8f)
            {
                return false;
            }

            var environment = EvaluateSpawnEnvironment(terrain, context, xz, centerY);
            if (environment.RejectedByWater)
            {
                return false;
            }
            if (environment.NearestWaterDistance < 180f)
            {
                return false;
            }
            if (requireCactusNearby && context.CactusPositions.Count > 0 && (environment.NearestCactusDistance > 75f || environment.NearbyCactusCount < 1))
            {
                return false;
            }

            float lowFloorScore = Mathf.Max(0f, centerY) * 0.55f;
            float valleyBonus = Mathf.Clamp(valleyWallRelief - 8.0f, 0f, 42f) * 1.10f;
            float cactusDistanceScore = Mathf.Min(environment.NearestCactusDistance, 160f) * 0.65f;
            float cactusClusterBonus = Mathf.Min(6, environment.NearbyCactusCount) * 7.5f;
            float waterDrynessBonus = Mathf.Clamp(environment.NearestWaterDistance - 180f, 0f, 120f) * 0.55f;
            float routeScore = Mathf.Min(routeDistance, 450f) * 0.010f;
            float score = lowFloorScore + slope * 3.5f + heightRange * 60f + routeScore + cactusDistanceScore - valleyBonus - cactusClusterBonus - waterDrynessBonus;
            candidate = new SpawnCandidate
            {
                Position = new Vector3(xz.x, centerY, xz.y),
                SlopeDegrees = slope,
                HeightRangeMeters = heightRange,
                ObstacleCount = obstacleCount,
                ValleyWallReliefMeters = valleyWallRelief,
                NearestWaterDistanceMeters = environment.NearestWaterDistance,
                WaterSurfaceClearanceMeters = environment.WaterSurfaceClearanceMeters,
                NearestCactusDistanceMeters = environment.NearestCactusDistance,
                NearbyCactusCount = environment.NearbyCactusCount,
                Score = score,
            };
            return true;
        }

        static SpawnSearchContext BuildSpawnSearchContext()
        {
            var context = new SpawnSearchContext();
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                string path = HierarchyPath(renderer.gameObject).ToLowerInvariant();
                if (IsWaterLikeName(path) || RendererUsesWaterMaterial(renderer))
                {
                    context.WaterBounds.Add(renderer.bounds);
                }
                if (IsCactusLikeName(path))
                {
                    context.CactusPositions.Add(renderer.bounds.center);
                }
            }

            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
            {
                if (collider == null)
                {
                    continue;
                }

                string path = HierarchyPath(collider.gameObject).ToLowerInvariant();
                if (IsWaterLikeName(path))
                {
                    context.WaterBounds.Add(collider.bounds);
                }
                if (IsCactusLikeName(path))
                {
                    context.CactusPositions.Add(collider.bounds.center);
                }
            }

            foreach (var transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (transform == null)
                {
                    continue;
                }
                string path = HierarchyPath(transform.gameObject).ToLowerInvariant();
                if (IsCactusLikeName(path))
                {
                    context.CactusPositions.Add(transform.position);
                }
            }

            Debug.Log("VLN_MESA_SPAWN_CONTEXT water_bounds=" + context.WaterBounds.Count.ToString(CultureInfo.InvariantCulture) + " cactus_positions=" + context.CactusPositions.Count.ToString(CultureInfo.InvariantCulture));
            return context;
        }

        static SpawnEnvironmentMetrics EvaluateSpawnEnvironment(Terrain terrain, SpawnSearchContext context, Vector2 xz, float groundY)
        {
            var metrics = new SpawnEnvironmentMetrics
            {
                NearestWaterDistance = float.PositiveInfinity,
                WaterSurfaceClearanceMeters = float.PositiveInfinity,
                NearestCactusDistance = float.PositiveInfinity,
            };

            foreach (var bounds in context.WaterBounds)
            {
                float dx = 0f;
                if (xz.x < bounds.min.x) dx = bounds.min.x - xz.x;
                else if (xz.x > bounds.max.x) dx = xz.x - bounds.max.x;

                float dz = 0f;
                if (xz.y < bounds.min.z) dz = bounds.min.z - xz.y;
                else if (xz.y > bounds.max.z) dz = xz.y - bounds.max.z;

                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance <= 8f)
                {
                    float surfaceY = bounds.center.y;
                    float clearance = groundY - surfaceY;
                    metrics.WaterSurfaceClearanceMeters = Mathf.Min(metrics.WaterSurfaceClearanceMeters, clearance);
                    if (clearance < 8f)
                    {
                        metrics.RejectedByWater = true;
                    }
                }
            }

            metrics.NearestWaterDistance = Mathf.Min(metrics.NearestWaterDistance, FindNearestVisibleWaterDistance(terrain, context, xz));

            foreach (var cactus in context.CactusPositions)
            {
                float distance = Vector2.Distance(new Vector2(cactus.x, cactus.z), xz);
                metrics.NearestCactusDistance = Mathf.Min(metrics.NearestCactusDistance, distance);
                if (distance <= 140f)
                {
                    metrics.NearbyCactusCount++;
                }
            }

            if (float.IsPositiveInfinity(metrics.NearestWaterDistance))
            {
                metrics.NearestWaterDistance = 9999f;
            }
            if (float.IsPositiveInfinity(metrics.WaterSurfaceClearanceMeters))
            {
                metrics.WaterSurfaceClearanceMeters = 9999f;
            }
            if (float.IsPositiveInfinity(metrics.NearestCactusDistance))
            {
                metrics.NearestCactusDistance = 9999f;
            }
            return metrics;
        }

        static float FindNearestVisibleWaterDistance(Terrain terrain, SpawnSearchContext context, Vector2 xz)
        {
            if (context.WaterBounds.Count == 0)
            {
                return 9999f;
            }

            for (float radius = 0f; radius <= 320f; radius += 16f)
            {
                int samples = radius < 0.1f ? 1 : 24;
                for (int i = 0; i < samples; i++)
                {
                    float angle = samples == 1 ? 0f : i * Mathf.PI * 2f / samples;
                    Vector2 sample = xz + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    foreach (var bounds in context.WaterBounds)
                    {
                        if (!ContainsXZ(bounds, sample, 0f))
                        {
                            continue;
                        }
                        float waterSurfaceY = bounds.center.y;
                        float terrainY = TerrainHeight(terrain, sample);
                        if (terrainY <= waterSurfaceY + 1.2f)
                        {
                            return radius;
                        }
                    }
                }
            }

            return 9999f;
        }

        static bool ContainsXZ(Bounds bounds, Vector2 xz, float padding)
        {
            return xz.x >= bounds.min.x - padding && xz.x <= bounds.max.x + padding &&
                xz.y >= bounds.min.z - padding && xz.y <= bounds.max.z + padding;
        }

        static bool RendererUsesWaterMaterial(Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }
                if (IsWaterLikeName(material.name))
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsWaterLikeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            string lower = value.ToLowerInvariant();
            return lower.Contains("water") ||
                lower.Contains("lake") ||
                lower.Contains("pond") ||
                lower.Contains("pool") ||
                lower.Contains("river") ||
                lower.Contains("stream") ||
                lower.Contains("oasis_pool") ||
                lower.Contains("oasiswater") ||
                lower.Contains("oasis_water");
        }

        static bool IsCactusLikeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            string lower = value.ToLowerInvariant();
            return lower.Contains("cactus") ||
                lower.Contains("cacactus") ||
                lower.Contains("grocactus") ||
                lower.Contains("saguaro") ||
                lower.Contains("opuntia") ||
                lower.Contains("senita") ||
                lower.Contains("yucca") ||
                lower.Contains("agave") ||
                lower.Contains("brittlebush") ||
                lower.Contains("drygrass") ||
                lower.Contains("grasspatch") ||
                lower.Contains("vln_mesa_treeobstacle_");
        }

        static float ValleyWallReliefAround(Terrain terrain, Vector2 xz, float radius)
        {
            float center = TerrainHeight(terrain, xz);
            float max = center;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector2 sample = xz + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                max = Mathf.Max(max, TerrainHeight(terrain, sample));
            }
            return Mathf.Max(0f, max - center);
        }

        static int CountBlockingColliders(Vector3 groundPoint, Vector3 halfExtents)
        {
            int count = 0;
            var hits = Physics.OverlapBox(groundPoint + Vector3.up * 1.0f, halfExtents, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit == null || hit is TerrainCollider || hit.isTrigger)
                {
                    continue;
                }
                count++;
            }
            return count;
        }

        static bool TryFindObstacleImpactSetup(Terrain terrain, Transform vehicleRoot, Transform rigRoot, out ImpactSetup setup)
        {
            setup = default;
            var routePoints = ReadRouteWaypoints();
            var spawnContext = BuildSpawnSearchContext();
            var colliders = UnityEngine.Object.FindObjectsOfType<Collider>(true)
                .Where(c => IsImpactObstacleCandidate(c, terrain, vehicleRoot, rigRoot))
                .ToArray();

            bool found = false;
            float bestScore = float.MaxValue;
            foreach (var collider in colliders)
            {
                Bounds bounds = collider.bounds;
                Vector3 target = bounds.center;
                target.y = TerrainHeight(terrain, new Vector2(target.x, target.z)) + Mathf.Min(0.55f, Mathf.Max(0.25f, bounds.size.y * 0.35f));
                float obstacleRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                float approachDistance = Mathf.Clamp(obstacleRadius + 4.2f, 4.2f, 8.0f);

                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2f / 16f;
                    Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector2 startXZ = new Vector2(bounds.center.x - dir.x * approachDistance, bounds.center.z - dir.z * approachDistance);
                    if (!TryEvaluateSpawn(terrain, startXZ, routePoints, spawnContext, requireCactusNearby: false, out var spawn))
                    {
                        continue;
                    }

                    Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);
                    float routeDistance = routePoints.Count > 0 ? routePoints.Min(p => Vector2.Distance(new Vector2(p.x, p.z), startXZ)) : 0f;
                    float sizeScore = -Mathf.Min(6f, bounds.size.magnitude);
                    float score = routeDistance * 0.02f + spawn.SlopeDegrees * 1.5f + spawn.HeightRangeMeters * 25f + sizeScore;
                    if (!found || score < bestScore)
                    {
                        found = true;
                        bestScore = score;
                        setup = new ImpactSetup
                        {
                            StartPosition = spawn.Position,
                            StartRotation = rotation,
                            TargetCollider = collider,
                            TargetPoint = target,
                            TargetName = HierarchyPath(collider.gameObject),
                            InitialDistanceMeters = Vector3.Distance(new Vector3(spawn.Position.x, 0f, spawn.Position.z), new Vector3(target.x, 0f, target.z)),
                        };
                    }
                }
            }

            return found;
        }

        static bool TryFindCliffDropSetup(Terrain terrain, out CliffDropSetup setup)
        {
            setup = default;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            bool found = false;
            float bestScore = float.MaxValue;

            for (int ix = 5; ix <= 55; ix++)
            {
                float nx = ix / 60f;
                for (int iz = 5; iz <= 55; iz++)
                {
                    float nz = iz / 60f;
                    Vector2 startXZ = new Vector2(origin.x + size.x * nx, origin.z + size.z * nz);
                    float startSlope = terrain.terrainData.GetSteepness(nx, nz);
                    if (startSlope > 12f)
                    {
                        continue;
                    }

                    float startY = TerrainHeight(terrain, startXZ);
                    float seedHeightRange = HeightRangeAround(terrain, startXZ, 1.8f);
                    if (seedHeightRange > 0.65f)
                    {
                        continue;
                    }

                    for (int directionIndex = 0; directionIndex < 24; directionIndex++)
                    {
                        float angle = directionIndex * Mathf.PI * 2f / 24f;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        float minY = startY;
                        float maxEdgeSlope = 0f;
                        float lowDistance = 0f;
                        float edgeStartDistance = 0f;
                        float edgeEndDistance = 0f;
                        float previousY = startY;
                        float previousDistance = 0f;

                        for (float distance = 4f; distance <= 32f; distance += 4f)
                        {
                            Vector2 sample = startXZ + direction * distance;
                            if (!IsInsideTerrain(terrain, sample, 0.08f))
                            {
                                minY = startY;
                                break;
                            }

                            float y = TerrainHeight(terrain, sample);
                            float segmentSlope = Mathf.Atan2(Mathf.Abs(previousY - y), Mathf.Max(0.1f, distance - previousDistance)) * Mathf.Rad2Deg;
                            maxEdgeSlope = Mathf.Max(maxEdgeSlope, segmentSlope);
                            if (previousY - y > 0.75f && segmentSlope >= 20f && segmentSlope >= maxEdgeSlope - 0.001f)
                            {
                                edgeStartDistance = previousDistance;
                                edgeEndDistance = distance;
                            }
                            if (y < minY)
                            {
                                minY = y;
                                lowDistance = distance;
                            }
                            previousY = y;
                            previousDistance = distance;
                        }

                        float drop = startY - minY;
                        if (drop < 3.5f || drop > 24f || maxEdgeSlope < 24f || lowDistance < 8f || edgeEndDistance <= edgeStartDistance)
                        {
                            continue;
                        }

                        float driveStartDistance = Mathf.Max(0f, edgeStartDistance - 3.0f);
                        float targetDistance = Mathf.Clamp(Mathf.Max(lowDistance, edgeEndDistance + 8f), driveStartDistance + 10f, 38f);
                        Vector2 driveStartXZ = startXZ + direction * driveStartDistance;
                        if (!IsInsideTerrain(terrain, driveStartXZ, 0.08f))
                        {
                            continue;
                        }
                        float driveStartSlope = terrain.terrainData.GetSteepness(
                            (driveStartXZ.x - origin.x) / size.x,
                            (driveStartXZ.y - origin.z) / size.z);
                        if (driveStartSlope > 16f)
                        {
                            continue;
                        }
                        float driveStartY = TerrainHeight(terrain, driveStartXZ);
                        float driveStartHeightRange = HeightRangeAround(terrain, driveStartXZ, 1.8f);
                        if (driveStartHeightRange > 0.55f)
                        {
                            continue;
                        }

                        if (CountBlockingColliders(new Vector3(driveStartXZ.x, driveStartY, driveStartXZ.y), new Vector3(2.6f, 2.2f, 2.6f)) > 0)
                        {
                            continue;
                        }

                        Vector2 targetXZ = startXZ + direction * targetDistance;
                        float targetY = TerrainHeight(terrain, targetXZ);
                        float reachableEdgeDistance = Mathf.Max(0f, edgeStartDistance - driveStartDistance);
                        float score = Mathf.Abs(drop - 9f) * 1.8f + driveStartSlope * 2.0f + driveStartHeightRange * 45f + reachableEdgeDistance * 1.5f - maxEdgeSlope * 0.12f;
                        if (!found || score < bestScore)
                        {
                            found = true;
                            bestScore = score;
                            Vector3 forward = new Vector3(direction.x, 0f, direction.y).normalized;
                            setup = new CliffDropSetup
                            {
                                StartPosition = new Vector3(driveStartXZ.x, driveStartY + 0.12f, driveStartXZ.y),
                                StartRotation = Quaternion.LookRotation(forward, Vector3.up),
                                TargetPoint = new Vector3(targetXZ.x, targetY, targetXZ.y),
                                ExpectedDropMeters = driveStartY - targetY,
                                MaxEdgeSlopeDegrees = maxEdgeSlope,
                                SampleDistanceMeters = targetDistance - driveStartDistance,
                            };
                        }
                    }
                }
            }

            return found;
        }

        static bool IsInsideTerrain(Terrain terrain, Vector2 xz, float margin01)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = (xz.x - origin.x) / size.x;
            float nz = (xz.y - origin.z) / size.z;
            return nx >= margin01 && nx <= 1f - margin01 && nz >= margin01 && nz <= 1f - margin01;
        }

        static bool IsImpactObstacleCandidate(Collider collider, Terrain terrain, Transform vehicleRoot, Transform rigRoot)
        {
            if (collider == null || !collider.enabled || collider.isTrigger || collider is TerrainCollider)
            {
                return false;
            }
            if (vehicleRoot != null && collider.transform.IsChildOf(vehicleRoot))
            {
                return false;
            }
            if (rigRoot != null && collider.transform.IsChildOf(rigRoot))
            {
                return false;
            }

            string name = HierarchyPath(collider.gameObject).ToLowerInvariant();
            bool namedLikeObstacle = name.Contains("rock") || name.Contains("boulder") || name.Contains("rubble") || name.Contains("strate") || name.Contains("cliff") || name.Contains("cactus") || name.Contains("tree") || name.Contains("obstacle");
            if (!namedLikeObstacle)
            {
                return false;
            }

            Bounds b = collider.bounds;
            if (b.size.y < 0.35f || b.size.y > 18f || b.size.x > 35f || b.size.z > 35f)
            {
                return false;
            }

            float terrainY = TerrainHeight(terrain, new Vector2(b.center.x, b.center.z));
            return b.max.y > terrainY + 0.30f && b.min.y < terrainY + 1.20f;
        }

        static float HeightRangeAround(Terrain terrain, Vector2 xz, float radius)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            Vector2[] samples =
            {
                xz,
                xz + new Vector2(radius, 0f),
                xz + new Vector2(-radius, 0f),
                xz + new Vector2(0f, radius),
                xz + new Vector2(0f, -radius),
            };
            foreach (var sample in samples)
            {
                float h = TerrainHeight(terrain, sample);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            return max - min;
        }

        static float TerrainHeight(Terrain terrain, Vector2 xz)
        {
            return terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;
        }

        static Quaternion RouteAlignedRotation(List<Vector3> routePoints, Vector3 spawn)
        {
            if (routePoints.Count >= 2)
            {
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < routePoints.Count; i++)
                {
                    float dist = Vector3.SqrMagnitude(new Vector3(routePoints[i].x - spawn.x, 0f, routePoints[i].z - spawn.z));
                    if (dist < best)
                    {
                        best = dist;
                        nearest = i;
                    }
                }
                int next = Mathf.Min(routePoints.Count - 1, nearest + 1);
                if (next == nearest && nearest > 0)
                {
                    next = nearest - 1;
                }
                Vector3 dir = routePoints[next] - routePoints[nearest];
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.1f)
                {
                    return Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
            return Quaternion.identity;
        }

        static List<Vector3> ReadRouteWaypoints()
        {
            var route = new List<Vector3>();
            var root = GameObject.Find("VLN_Mesa_RouteCandidate");
            if (root != null)
            {
                var keyed = new List<KeyValuePair<string, Vector3>>();
                foreach (Transform child in root.transform)
                {
                    if (child.name.StartsWith("VLN_Mesa_RouteWaypoint_", StringComparison.Ordinal))
                    {
                        keyed.Add(new KeyValuePair<string, Vector3>(child.name, child.position));
                    }
                }
                keyed.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                route.AddRange(keyed.Select(pair => pair.Value));
            }

            if (route.Count == 0)
            {
                string configPath = ProjectRootPath("config/mesa_desert_route_candidate.json");
                if (File.Exists(configPath))
                {
                    string text = File.ReadAllText(configPath);
                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, "\\{ \\\"index\\\": \\d+, \\\"x\\\": ([^,]+), \\\"y\\\": ([^,]+), \\\"z\\\": ([^ }]+)"))
                    {
                        route.Add(new Vector3(ParseFloat(match.Groups[1].Value), ParseFloat(match.Groups[2].Value), ParseFloat(match.Groups[3].Value)));
                    }
                }
            }
            return route;
        }

        static GameObject DuplicateRootFromScene(Scene sourceScene, string rootName, Scene targetScene)
        {
            var duplicate = TryDuplicateRootFromScene(sourceScene, rootName, targetScene);
            if (duplicate == null)
            {
                throw new InvalidOperationException("Missing root in source vehicle scene: " + rootName);
            }
            return duplicate;
        }

        static GameObject TryDuplicateRootFromScene(Scene sourceScene, string rootName, Scene targetScene)
        {
            GameObject source = sourceScene.GetRootGameObjects().FirstOrDefault(go => go.name == rootName);
            if (source == null)
            {
                return null;
            }
            GameObject duplicate = UnityEngine.Object.Instantiate(source);
            duplicate.name = rootName;
            SceneManager.MoveGameObjectToScene(duplicate, targetScene);
            return duplicate;
        }

        static void CreateSpawnMarker(Vector3 spawn)
        {
            var marker = new GameObject(SpawnMarkerName);
            marker.transform.position = spawn + Vector3.up * 0.05f;
        }

        static void CreateReviewCamera(Vector3 spawn, Quaternion vehicleRotation)
        {
            var cameraObject = new GameObject(ViewerCameraName);
            Vector3 back = vehicleRotation * new Vector3(-3.6f, 2.1f, -6.2f);
            cameraObject.transform.position = spawn + back + Vector3.up * 1.3f;
            cameraObject.transform.LookAt(spawn + Vector3.up * 0.75f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1200f;
            camera.depth = 20f;
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        static void CreateSmokeController()
        {
            var controller = new GameObject(SmokeControllerName);
            controller.AddComponent<VlnMesaTopgearVehicleSmokeTest>();
        }

        static void RemoveIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        static void EnsureSceneExists(string scenePath)
        {
            if (!File.Exists(ProjectRelativeToAbsolute(scenePath)))
            {
                throw new FileNotFoundException("Missing Unity scene", scenePath);
            }
        }

        static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        static int CountRenderers(GameObject root, string childName)
        {
            Transform child = FindDeepChild(root.transform, childName);
            return child != null ? child.GetComponentsInChildren<Renderer>(true).Length : 0;
        }

        static int CountDeepChildren(GameObject root, string needle)
        {
            if (root == null)
            {
                return 0;
            }
            return root.GetComponentsInChildren<Transform>(true).Count(t => t != root.transform && t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static int CountTopgearSensorCameras(GameObject rig)
        {
            Transform sensorRoot = rig != null ? FindDeepChild(rig.transform, "ScoutWheelGround_TopgearSensorSuite") : null;
            return sensorRoot != null ? sensorRoot.GetComponentsInChildren<Camera>(true).Length : 0;
        }

        static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }
                var result = FindDeepChild(child, childName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        static void WriteConfig(BuildResult result, List<Vector3> routePoints)
        {
            string configPath = ProjectRootPath("config/mesa_topgear_vehicle_candidate.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"stage\": \"mesa_desert_topgear_vehicle_candidate\",");
            sb.AppendLine("  \"scene_path\": \"" + result.ScenePath + "\",");
            sb.AppendLine("  \"world_scene_path\": \"" + result.SourceWorldScenePath + "\",");
            sb.AppendLine("  \"source_vehicle_scene_path\": \"" + result.SourceVehicleScenePath + "\",");
            sb.AppendLine("  \"notes\": \"Topgear vehicle copied from frozen wheel-ground baseline into Mesa first world. Sensor locked pose is not recalculated.\",");
            sb.AppendLine("  \"spawn\": {");
            sb.AppendLine("    \"x\": " + result.SpawnPosition.x.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"y\": " + result.SpawnPosition.y.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"z\": " + result.SpawnPosition.z.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"slope_deg\": " + result.SpawnSlopeDegrees.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"height_range_m\": " + result.SpawnHeightRangeMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"valley_wall_relief_m\": " + result.SpawnValleyWallReliefMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"nearest_water_distance_m\": " + result.SpawnNearestWaterDistanceMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"water_surface_clearance_m\": " + result.SpawnWaterSurfaceClearanceMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"nearest_cactus_distance_m\": " + result.SpawnNearestCactusDistanceMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"nearby_cactus_count\": " + result.SpawnNearbyCactusCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"obstacle_count\": " + result.SpawnObstacleCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("  },");
            sb.AppendLine("  \"copied_vehicle\": {");
            sb.AppendLine("    \"wheel_collider_count\": " + result.WheelColliderCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"rigidbody_count\": " + result.RigidbodyCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"visual_renderer_count\": " + result.VisualRendererCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"sensor_camera_count\": " + result.SensorCameraCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"sensor_lidar_count\": " + result.SensorLidarCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("  },");
            sb.AppendLine("  \"world_physics\": {");
            sb.AppendLine("    \"terrain_collider_count\": " + result.TerrainColliderCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"scene_collider_count\": " + result.SceneColliderCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"terrain_contact_classified_as_sand\": true,");
            sb.AppendLine("    \"mesa_sand_terrain_material\": \"" + MesaSandPhysicMaterialPath + "\",");
            sb.AppendLine("    \"mesa_rock_cliff_slide_material\": \"" + MesaRockSlidePhysicMaterialPath + "\",");
            sb.AppendLine("    \"wheel_visual_vertical_offset_m\": 0.0,");
            sb.AppendLine("    \"relax_stop_damping_on_steep_or_airborne\": true,");
            sb.AppendLine("    \"relax_wheel_friction_on_steep_or_airborne\": true,");
            sb.AppendLine("    \"relaxed_wheel_friction_stiffness_scale\": 0.30,");
            sb.AppendLine("    \"relax_wheel_suspension_on_steep_or_airborne\": true,");
            sb.AppendLine("    \"relaxed_wheel_suspension_spring_scale\": 0.08,");
            sb.AppendLine("    \"relaxed_wheel_suspension_damper_scale\": 0.25,");
            sb.AppendLine("    \"steep_slope_gravity_assist_accel_mps2\": 5.50");
            sb.AppendLine("  },");
            sb.AppendLine("  \"route_reference_waypoint_count\": " + routePoints.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("}");
            File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        static string ProjectRootPath(string relative)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", relative));
        }

        static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F3", CultureInfo.InvariantCulture) + "," + value.y.ToString("F3", CultureInfo.InvariantCulture) + "," + value.z.ToString("F3", CultureInfo.InvariantCulture);
        }

        static string HierarchyPath(GameObject go)
        {
            var names = new List<string>();
            Transform current = go.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        struct SpawnCandidate
        {
            public Vector3 Position;
            public float SlopeDegrees;
            public float HeightRangeMeters;
            public float ValleyWallReliefMeters;
            public float NearestWaterDistanceMeters;
            public float WaterSurfaceClearanceMeters;
            public float NearestCactusDistanceMeters;
            public int NearbyCactusCount;
            public int ObstacleCount;
            public float Score;
        }

        sealed class SpawnSearchContext
        {
            public readonly List<Bounds> WaterBounds = new List<Bounds>();
            public readonly List<Vector3> CactusPositions = new List<Vector3>();
        }

        struct SpawnEnvironmentMetrics
        {
            public bool RejectedByWater;
            public float NearestWaterDistance;
            public float WaterSurfaceClearanceMeters;
            public float NearestCactusDistance;
            public int NearbyCactusCount;
        }

        public sealed class BuildResult
        {
            public string ScenePath;
            public string SourceWorldScenePath;
            public string SourceVehicleScenePath;
            public Vector3 SpawnPosition;
            public float SpawnSlopeDegrees;
            public float SpawnHeightRangeMeters;
            public float SpawnValleyWallReliefMeters;
            public float SpawnNearestWaterDistanceMeters;
            public float SpawnWaterSurfaceClearanceMeters;
            public float SpawnNearestCactusDistanceMeters;
            public int SpawnNearbyCactusCount;
            public int SpawnObstacleCount;
            public float SpawnScore;
            public int WheelColliderCount;
            public int RigidbodyCount;
            public int VisualRendererCount;
            public int SensorCameraCount;
            public int SensorLidarCount;
            public int TerrainColliderCount;
            public int SceneColliderCount;
        }

        struct ImpactSetup
        {
            public Vector3 StartPosition;
            public Quaternion StartRotation;
            public Collider TargetCollider;
            public Vector3 TargetPoint;
            public string TargetName;
            public float InitialDistanceMeters;
        }

        struct CliffDropSetup
        {
            public Vector3 StartPosition;
            public Quaternion StartRotation;
            public Vector3 TargetPoint;
            public float ExpectedDropMeters;
            public float MaxEdgeSlopeDegrees;
            public float SampleDistanceMeters;
        }
    }
}
