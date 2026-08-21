using System;
using System.Collections.Generic;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnOffroadScoutWheelGroundCandidateSmokeTest : MonoBehaviour
    {
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string TopgearVisualRootName = "ScoutWheelGround_TopgearV2Visual";
        const string TopgearSensorRootName = "ScoutWheelGround_TopgearSensorSuite";

        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] string m_ImageTopic = "/vln/front/image_raw";
        [SerializeField] string m_CameraInfoTopic = "/vln/front/camera_info";
        [SerializeField] string m_PointCloudTopic = "/vln/lidar/points";
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] string m_OdomTopic = "/vln/odom";
        [SerializeField] string m_CameraFrameId = "front_camera_optical_frame";
        [SerializeField] string m_LidarFrameId = "lidar_link";
        [SerializeField] string m_RearImageTopic = "/vln/rear/image_raw";
        [SerializeField] string m_RearCameraInfoTopic = "/vln/rear/camera_info";
        [SerializeField] string m_LeftImageTopic = "/vln/left/image_raw";
        [SerializeField] string m_LeftCameraInfoTopic = "/vln/left/camera_info";
        [SerializeField] string m_RightImageTopic = "/vln/right/image_raw";
        [SerializeField] string m_RightCameraInfoTopic = "/vln/right/camera_info";
        [SerializeField] string m_RearCameraFrameId = "rear_camera_optical_frame";
        [SerializeField] string m_LeftCameraFrameId = "left_camera_optical_frame";
        [SerializeField] string m_RightCameraFrameId = "right_camera_optical_frame";
        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 38f;

        float m_StartRealtime;
        string m_ResultPath;
        Vector3 m_InitialPosition;
        bool m_ScreenshotRequested;
        bool m_TopgearScreenshotRequested;
        bool m_TopgearSensorScreenshotRequested;
        bool m_BridgeScreenshotRequested;
        bool m_ShortRampScreenshotRequested;
        bool m_ChallengeScreenshotRequested;
        bool m_ChallengeGrassScreenshotRequested;
        bool m_ChallengeStoneScreenshotRequested;
        bool m_ChallengeSandScreenshotRequested;
        bool m_FinalSnapshotWritten;

        void Start()
        {
            Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 12);
            Physics.defaultSolverVelocityIterations = Mathf.Max(Physics.defaultSolverVelocityIterations, 6);
            Time.fixedDeltaTime = 0.01f;

            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_candidate_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));

            var physicsRoot = GameObject.Find(PhysicsRootName);
            if (physicsRoot == null)
            {
                throw new InvalidOperationException("Missing Scout wheel-ground physics root in candidate scene.");
            }

            m_InitialPosition = physicsRoot.transform.position;
            File.WriteAllText(m_ResultPath,
                $"started={DateTime.UtcNow:O}\n" +
                "scene=Assets/VLN/Scenes/VLNOffroadScoutWheelGroundCandidate.unity\n" +
                "stage=wheel_ground_real_dynamics_candidate\n" +
                "vehicle=agilex_scout_v2_urdf_visual_plus_unity_wheelcollider_physics\n" +
                "motion_source=wheel_ground_contact_not_kinematic_rig\n" +
                "physics_backend=Unity WheelCollider + Rigidbody\n" +
                "urdf_articulation_candidate_preserved_in_separate_scene=VLNOffroadScoutUrdfCandidate.unity\n" +
                $"image_topic={m_ImageTopic}\n" +
                $"camera_info_topic={m_CameraInfoTopic}\n" +
                $"rear_image_topic={m_RearImageTopic}\n" +
                $"rear_camera_info_topic={m_RearCameraInfoTopic}\n" +
                $"left_image_topic={m_LeftImageTopic}\n" +
                $"left_camera_info_topic={m_LeftCameraInfoTopic}\n" +
                $"right_image_topic={m_RightImageTopic}\n" +
                $"right_camera_info_topic={m_RightCameraInfoTopic}\n" +
                $"pointcloud_topic={m_PointCloudTopic}\n" +
                $"cmd_vel_topic={m_CmdVelTopic}\n" +
                $"odom_topic={m_OdomTopic}\n" +
                $"camera_frame_id={m_CameraFrameId}\n" +
                $"rear_camera_frame_id={m_RearCameraFrameId}\n" +
                $"left_camera_frame_id={m_LeftCameraFrameId}\n" +
                $"right_camera_frame_id={m_RightCameraFrameId}\n" +
                $"lidar_frame_id={m_LidarFrameId}\n" +
                "tf_topic=/tf\n" +
                "tf_tree=map->base_link->front_camera_optical_frame,rear_camera_optical_frame,left_camera_optical_frame,right_camera_optical_frame,lidar_link\n" +
                "odom_type=nav_msgs/msg/Odometry\n" +
                "cmd_vel_type=geometry_msgs/msg/Twist\n" +
                "image_resolution=640x480\n" +
                "camera_count=4\n" +
                "lidar_scan_pattern=VLP-16\n" +
                "lidar_points_per_scan=7200\n" +
                "terrain_geometry_policy=visible_local_physics_no_flattening_no_hidden_bypass\n" +
                BuildPhysicsSummary(physicsRoot));

            var ros = ROSConnection.GetOrCreateInstance();
            ros.RosIPAddress = m_RosIp;
            ros.RosPort = m_RosPort;
            ros.ConnectOnStart = true;
            ros.ShowHud = false;
            ros.listenForTFMessages = false;

            if (!ros.HasConnectionThread)
            {
                ros.Connect(m_RosIp, m_RosPort);
            }

            Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_READY image={m_ImageTopic} points={m_PointCloudTopic} ip={m_RosIp} port={m_RosPort}");
        }

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;
            if (!m_ScreenshotRequested && elapsed >= 5f)
            {
                m_ScreenshotRequested = true;
                string screenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_candidate_screenshot.png");
                SaveViewerCameraScreenshot(screenshotPath);
                File.AppendAllText(m_ResultPath, $"screenshot={screenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_SCREENSHOT {screenshotPath}");
            }

            if (!m_TopgearScreenshotRequested && elapsed >= 5.2f)
            {
                m_TopgearScreenshotRequested = true;
                string topgearScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_topgear_visual_screenshot.png");
                SaveTopgearVisualScreenshot(topgearScreenshotPath);
                File.AppendAllText(m_ResultPath, $"topgear_visual_screenshot={topgearScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_TOPGEAR_VISUAL_SCREENSHOT {topgearScreenshotPath}");
            }

            if (!m_TopgearSensorScreenshotRequested && elapsed >= 5.4f)
            {
                m_TopgearSensorScreenshotRequested = true;
                string topgearSensorScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_topgear_sensor_suite_screenshot.png");
                SaveTopgearSensorSuiteScreenshot(topgearSensorScreenshotPath);
                File.AppendAllText(m_ResultPath, $"topgear_sensor_suite_screenshot={topgearSensorScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_TOPGEAR_SENSOR_SUITE_SCREENSHOT {topgearSensorScreenshotPath}");
            }

            var physicsRoot = GameObject.Find(PhysicsRootName);
            if (!m_BridgeScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= -9.5f && physicsRoot.transform.position.z <= -4.2f)
            {
                m_BridgeScreenshotRequested = true;
                string bridgeScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_bridge_screenshot.png");
                SaveViewerCameraScreenshot(bridgeScreenshotPath);
                File.AppendAllText(m_ResultPath, $"bridge_screenshot={bridgeScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_BRIDGE_SCREENSHOT {bridgeScreenshotPath}");
            }

            if (!m_ShortRampScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= 1.2f && physicsRoot.transform.position.z <= 5.8f)
            {
                m_ShortRampScreenshotRequested = true;
                string shortRampScreenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_offroad_scout_wheel_ground_short_ramp_screenshot.png");
                SaveViewerCameraScreenshot(shortRampScreenshotPath);
                File.AppendAllText(m_ResultPath, $"short_ramp_screenshot={shortRampScreenshotPath}\n");
                Debug.Log($"VLN_OFFROAD_SCOUT_WHEEL_GROUND_SHORT_RAMP_SCREENSHOT {shortRampScreenshotPath}");
            }

            if (!m_ChallengeScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= 24.0f && physicsRoot.transform.position.z <= 49.5f)
            {
                m_ChallengeScreenshotRequested = true;
                SaveNamedScreenshot("challenge", "vln_offroad_scout_wheel_ground_challenge_screenshot.png", "VLN_OFFROAD_SCOUT_WHEEL_GROUND_CHALLENGE_SCREENSHOT");
            }

            if (!m_ChallengeGrassScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= 14.0f && physicsRoot.transform.position.z <= 16.7f)
            {
                m_ChallengeGrassScreenshotRequested = true;
                SaveNamedScreenshot("challenge_grass", "vln_offroad_scout_wheel_ground_challenge_grass_screenshot.png", "VLN_OFFROAD_SCOUT_WHEEL_GROUND_CHALLENGE_GRASS_SCREENSHOT");
            }

            if (!m_ChallengeStoneScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= 22.0f && physicsRoot.transform.position.z <= 27.2f)
            {
                m_ChallengeStoneScreenshotRequested = true;
                SaveNamedScreenshot("challenge_stone", "vln_offroad_scout_wheel_ground_challenge_stone_screenshot.png", "VLN_OFFROAD_SCOUT_WHEEL_GROUND_CHALLENGE_STONE_SCREENSHOT");
            }

            if (!m_ChallengeSandScreenshotRequested && physicsRoot != null && physicsRoot.transform.position.z >= 37.0f && physicsRoot.transform.position.z <= 46.0f)
            {
                m_ChallengeSandScreenshotRequested = true;
                SaveNamedScreenshot("challenge_sand", "vln_offroad_scout_wheel_ground_challenge_sand_screenshot.png", "VLN_OFFROAD_SCOUT_WHEEL_GROUND_CHALLENGE_SAND_SCREENSHOT");
            }

            if (!Application.isBatchMode || elapsed < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            WriteFinalSnapshot();
            Debug.Log("VLN_OFFROAD_SCOUT_WHEEL_GROUND_CANDIDATE_AUTO_EXIT");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        void OnApplicationQuit()
        {
            WriteFinalSnapshot();
        }

        void OnDestroy()
        {
            WriteFinalSnapshot();
        }

        void WriteFinalSnapshot()
        {
            if (m_FinalSnapshotWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return;
            }

            m_FinalSnapshotWritten = true;
            var physicsRoot = GameObject.Find(PhysicsRootName);
            float baseDelta = 0f;
            if (physicsRoot != null)
            {
                baseDelta = Vector3.Distance(m_InitialPosition, physicsRoot.transform.position);
            }

            File.AppendAllText(m_ResultPath,
                $"finished={DateTime.UtcNow:O}\n" +
                $"physics_root_delta_m={baseDelta:F4}\n" +
                $"final_position={(physicsRoot != null ? FormatVector(physicsRoot.transform.position) : "missing")}\n" +
                $"final_yaw_deg={(physicsRoot != null ? physicsRoot.transform.eulerAngles.y.ToString("F3") : "missing")}\n" +
                BuildGrassDeformationSummary());
        }

        static string BuildGrassDeformationSummary()
        {
            var deformers = FindObjectsOfType<VlnChallengeGrassDeformer>();
            int totalBlades = 0;
            int currentDeformed = 0;
            int maxDeformed = 0;
            int maxFreshAffected = 0;
            foreach (var deformer in deformers)
            {
                if (deformer == null)
                {
                    continue;
                }

                totalBlades += deformer.BladeCount;
                currentDeformed += deformer.CurrentDeformedBladeCount;
                maxDeformed += deformer.MaxDeformedBladeCount;
                maxFreshAffected += deformer.MaxFreshAffectedBladeCount;
            }

            float maxDeformedFraction = totalBlades > 0 ? maxDeformed / (float)totalBlades : 0f;
            return
                $"challenge_grass_deformer_final_count={deformers.Length}\n" +
                $"challenge_grass_total_blade_count={totalBlades}\n" +
                $"challenge_grass_current_deformed_blade_count={currentDeformed}\n" +
                $"challenge_grass_max_deformed_blade_count={maxDeformed}\n" +
                $"challenge_grass_max_fresh_affected_blade_count={maxFreshAffected}\n" +
                $"challenge_grass_max_deformed_fraction={maxDeformedFraction:F3}\n";
        }

        static string BuildPhysicsSummary(GameObject physicsRoot)
        {
            var visualRoot = GameObject.Find(VisualRootName);
            var body = physicsRoot.GetComponent<Rigidbody>();
            var colliders = physicsRoot.GetComponentsInChildren<WheelCollider>(true);
            return
                $"rigidbody_count={physicsRoot.GetComponentsInChildren<Rigidbody>(true).Length}\n" +
                $"wheel_collider_count={colliders.Length}\n" +
                $"box_collider_count={physicsRoot.GetComponentsInChildren<BoxCollider>(true).Length}\n" +
                $"broad_physical_trail_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalTrailSurface_")}\n" +
                $"road_physical_slab_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalRoadSlab_")}\n" +
                $"road_seam_transition_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalRoadSeam_")}\n" +
                $"bridge_physics_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalBridge")}\n" +
                $"short_ramp_physics_count={CountGameObjectsByPrefix("ScoutWheelGround_PhysicalShortRamp")}\n" +
                $"challenge_surface_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_")}\n" +
                $"challenge_grass_surface_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_Grass")}\n" +
                $"challenge_stone_surface_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_Stone")}\n" +
                $"challenge_sand_surface_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_Sand")}\n" +
                $"challenge_grass_blade_field_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_GrassBladeField")}\n" +
                $"challenge_grass_deformer_count={FindObjectsOfType<VlnChallengeGrassDeformer>().Length}\n" +
                $"challenge_stone_visual_detail_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_Stone")}\n" +
                $"challenge_stone_chip_field_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_StoneChipField")}\n" +
                $"challenge_sand_visual_detail_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_Sand")}\n" +
                $"challenge_sand_grain_field_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_SandGrainField")}\n" +
                $"challenge_physics_proxy_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_")}\n" +
                $"grass_physics_proxy_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_Grass")}\n" +
                $"stone_physics_proxy_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_Stone")}\n" +
                $"sand_physics_proxy_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_Sand")}\n" +
                $"challenge_physics_proxy_collider_count={CountCollidersByPrefix("ScoutWheelGround_ChallengePhysicsProxy_")}\n" +
                $"challenge_visual_physics_proxy_audit_pass={(ChallengeVisualPhysicsProxyAuditPass() ? 1 : 0)}\n" +
                $"challenge_pbr_albedo_material_count={CountChallengePbrMaterialsWithTexture("_MainTex")}\n" +
                $"challenge_pbr_normal_material_count={CountChallengePbrMaterialsWithTexture("_BumpMap")}\n" +
                $"challenge_pbr_occlusion_material_count={CountChallengePbrMaterialsWithTexture("_OcclusionMap")}\n" +
                $"challenge_obstacle_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_")}\n" +
                $"challenge_obstacle_collider_count={CountCollidersByPrefix("ScoutWheelGround_ChallengeObstacle_")}\n" +
                $"challenge_marker_count={CountGameObjectsByPrefix("ScoutWheelGround_ChallengeMarker_")}\n" +
                $"bridge_visual_detail_count={CountGameObjectsByPrefix("ScoutWheelGround_VisibleBridgeDetail_")}\n" +
                $"bridge_rail_collider_count={CountBridgeRailColliders()}\n" +
                $"short_ramp_visual_detail_count={CountGameObjectsByPrefix("ScoutWheelGround_VisibleShortRampDetail_")}\n" +
                $"road_physical_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_PhysicalRoad"):F3}\n" +
                $"bridge_physical_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_PhysicalBridge"):F3}\n" +
                $"bridge_physical_height_span_m={MaxBoundsHeightByPrefix("ScoutWheelGround_PhysicalBridge"):F3}\n" +
                $"short_ramp_physical_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_PhysicalShortRamp"):F3}\n" +
                $"short_ramp_physical_height_span_m={MaxBoundsHeightByPrefix("ScoutWheelGround_PhysicalShortRamp"):F3}\n" +
                $"challenge_surface_max_width_m={MaxBoundsWidthByPrefix("ScoutWheelGround_ChallengeSurface_"):F3}\n" +
                $"challenge_surface_height_span_m={MaxBoundsHeightByPrefix("ScoutWheelGround_ChallengeSurface_"):F3}\n" +
                $"challenge_obstacle_height_span_m={MaxBoundsHeightByPrefix("ScoutWheelGround_ChallengeObstacle_"):F3}\n" +
                $"challenge_end_wall_z={ObjectWorldZ("Offroad_DistantWall_Target"):F3}\n" +
                $"decorative_trail_collider_count={CountDecorativeTrailColliders()}\n" +
                $"decorative_bridge_renderer_count={CountDecorativeBridgeRenderers()}\n" +
                $"bridge_deck_has_renderer={HasComponentOnObject<Renderer>("ScoutWheelGround_PhysicalBridgeDeck")}\n" +
                $"bridge_deck_has_collider={HasComponentOnObject<Collider>("ScoutWheelGround_PhysicalBridgeDeck")}\n" +
                $"bridge_deck_renderer_collider_top_delta_m={BridgeDeckRendererColliderTopDelta():F4}\n" +
                $"visual_renderer_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true).Length : 0)}\n" +
                $"visual_collider_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<Collider>(true).Length : 0)}\n" +
                $"visual_articulation_body_count={(visualRoot != null ? visualRoot.GetComponentsInChildren<ArticulationBody>(true).Length : 0)}\n" +
                BuildTopgearVisualSummary() +
                BuildTopgearSensorSuiteSummary() +
                $"rigidbody_mass_kg={(body != null ? body.mass.ToString("F2") : "missing")}\n";
        }

        static string BuildTopgearVisualSummary()
        {
            var topgear = GameObject.Find(TopgearVisualRootName);
            if (topgear == null)
            {
                return
                    "topgear_visual_present=0\n" +
                    "topgear_visual_renderer_count=0\n" +
                    "topgear_visual_collider_count=0\n" +
                    "topgear_visual_rigidbody_count=0\n" +
                    "topgear_visual_bounds_size_m=missing\n";
            }

            var bounds = CalculateRendererBounds(topgear);
            return
                "topgear_visual_present=1\n" +
                $"topgear_visual_renderer_count={topgear.GetComponentsInChildren<Renderer>(true).Length}\n" +
                $"topgear_visual_collider_count={topgear.GetComponentsInChildren<Collider>(true).Length}\n" +
                $"topgear_visual_rigidbody_count={topgear.GetComponentsInChildren<Rigidbody>(true).Length}\n" +
                $"topgear_visual_bounds_center_m={FormatVector(bounds.center)}\n" +
                $"topgear_visual_bounds_size_m={FormatVector(bounds.size)}\n";
        }

        static string BuildTopgearSensorSuiteSummary()
        {
            var root = GameObject.Find(TopgearSensorRootName);
            if (root == null)
            {
                return
                    "topgear_sensor_suite_present=0\n" +
                    "topgear_sensor_camera_count=0\n" +
                    "topgear_sensor_lidar_count=0\n" +
                    "topgear_sensor_renderer_count=0\n" +
                    "topgear_sensor_collider_count=0\n" +
                    "topgear_sensor_rigidbody_count=0\n";
            }

            var bounds = CalculateRendererBounds(root);
            return
                "topgear_sensor_suite_present=1\n" +
                $"topgear_sensor_camera_count={CountChildrenByNameContains(root, "RGBCamera")}\n" +
                $"topgear_sensor_lidar_count={CountChildrenByNameContains(root, "LiDAR")}\n" +
                $"topgear_sensor_renderer_count={root.GetComponentsInChildren<Renderer>(true).Length}\n" +
                $"topgear_sensor_vlp16_official_mesh_count={CountChildrenByNameContains(root, "Velodyne_VLP16_OfficialMesh")}\n" +
                $"topgear_sensor_d405_official_stl_count={CountChildrenByNameContains(root, "RealSense_D405_OfficialStlBody")}\n" +
                $"topgear_sensor_procedural_vlp16_rib_count={CountChildrenByNameContains(root, "VLP16_VerticalRib")}\n" +
                $"topgear_sensor_procedural_d405_screw_count={CountChildrenByNameContains(root, "D405_Screw")}\n" +
                $"topgear_sensor_collider_count={root.GetComponentsInChildren<Collider>(true).Length}\n" +
                $"topgear_sensor_rigidbody_count={root.GetComponentsInChildren<Rigidbody>(true).Length}\n" +
                $"topgear_sensor_bounds_center_m={FormatVector(bounds.center)}\n" +
                $"topgear_sensor_bounds_size_m={FormatVector(bounds.size)}\n";
        }

        static int CountChildrenByNameContains(GameObject root, string token)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains(token, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        static int CountGameObjectsByPrefix(string prefix)
        {
            int count = 0;
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        static int CountCollidersByPrefix(string prefix)
        {
            int count = 0;
            foreach (var collider in FindObjectsOfType<Collider>())
            {
                if (collider.gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        static bool ChallengeVisualPhysicsProxyAuditPass()
        {
            return CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_Grass") >= 1 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_Stone") >= 1 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengeSurface_Sand") >= 1 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_GrassBladeField") >= 3 &&
                   FindObjectsOfType<VlnChallengeGrassDeformer>().Length >= 3 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_Stone") >= 55 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengeObstacle_Sand") >= 45 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_Grass") >= 5 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_Stone") >= 7 &&
                   CountGameObjectsByPrefix("ScoutWheelGround_ChallengePhysicsProxy_Sand") >= 10 &&
                   CountCollidersByPrefix("ScoutWheelGround_ChallengePhysicsProxy_") >= 22;
        }

        static int CountChallengePbrMaterialsWithTexture(string textureProperty)
        {
            var materials = new HashSet<Material>();
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !IsChallengePbrMaterial(material.name))
                    {
                        continue;
                    }

                    if (material.HasProperty(textureProperty) && material.GetTexture(textureProperty) != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            return materials.Count;
        }

        static bool IsChallengePbrMaterial(string materialName)
        {
            return materialName.StartsWith("ScoutWheelGround_ChallengeStone", StringComparison.Ordinal) ||
                   materialName.StartsWith("ScoutWheelGround_ChallengeSand", StringComparison.Ordinal);
        }

        static float MaxBoundsWidthByPrefix(string prefix)
        {
            float maxWidth = 0f;
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (!gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    maxWidth = Mathf.Max(maxWidth, renderer.bounds.size.x);
                    continue;
                }

                var collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    maxWidth = Mathf.Max(maxWidth, collider.bounds.size.x);
                }
            }

            return maxWidth;
        }

        static float MaxBoundsHeightByPrefix(string prefix)
        {
            float maxHeight = 0f;
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (!gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    maxHeight = Mathf.Max(maxHeight, renderer.bounds.size.y);
                    continue;
                }

                var collider = gameObject.GetComponent<Collider>();
                if (collider != null)
                {
                    maxHeight = Mathf.Max(maxHeight, collider.bounds.size.y);
                }
            }

            return maxHeight;
        }

        static int CountDecorativeTrailColliders()
        {
            int count = 0;
            foreach (var collider in FindObjectsOfType<Collider>())
            {
                string name = collider.gameObject.name;
                if (name.StartsWith("Offroad_DirtRoad_", StringComparison.Ordinal) || name == "Offroad_ShortRamp")
                {
                    count++;
                }
            }
            return count;
        }

        static int CountBridgeRailColliders()
        {
            int count = 0;
            foreach (var collider in FindObjectsOfType<BoxCollider>())
            {
                string name = collider.gameObject.name;
                if (!name.StartsWith("ScoutWheelGround_VisibleBridgeDetail_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (name.Contains("Rail", StringComparison.Ordinal) || name.Contains("Post", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        static int CountDecorativeBridgeRenderers()
        {
            int count = 0;
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                if (IsDecorativeBridgeTransform(renderer.transform))
                {
                    count++;
                }
            }
            return count;
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

        static int HasComponentOnObject<T>(string objectName) where T : Component
        {
            var gameObject = GameObject.Find(objectName);
            return gameObject != null && gameObject.GetComponent<T>() != null ? 1 : 0;
        }

        static float BridgeDeckRendererColliderTopDelta()
        {
            var gameObject = GameObject.Find("ScoutWheelGround_PhysicalBridgeDeck");
            if (gameObject == null)
            {
                return 999f;
            }

            var renderer = gameObject.GetComponent<Renderer>();
            var collider = gameObject.GetComponent<Collider>();
            if (renderer == null || collider == null)
            {
                return 999f;
            }

            return Mathf.Abs(renderer.bounds.max.y - collider.bounds.max.y);
        }

        static float ObjectWorldZ(string objectName)
        {
            var gameObject = GameObject.Find(objectName);
            return gameObject != null ? gameObject.transform.position.z : -999f;
        }

        static void SaveViewerCameraScreenshot(string path)
        {
            var cameraObject = GameObject.Find("ScoutWheelGroundCandidate_GameCamera") ?? GameObject.Find("Offroad_ViewerCamera");
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                throw new InvalidOperationException("No camera available for Scout wheel-ground candidate screenshot.");
            }

            RenderCameraToPng(camera, path, 1280, 720);
        }

        static void SaveTopgearVisualScreenshot(string path)
        {
            var physicsRoot = GameObject.Find(PhysicsRootName);
            var topgear = GameObject.Find(TopgearVisualRootName);
            if (physicsRoot == null || topgear == null)
            {
                SaveViewerCameraScreenshot(path);
                return;
            }

            var bounds = CalculateRendererBounds(topgear);
            var cameraObject = new GameObject("ScoutWheelGroundCandidate_TopgearVisualScreenshotCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = 20f;
                camera.fieldOfView = 32f;

                var focus = bounds.center + Vector3.up * 0.02f;
                var frontRight = physicsRoot.transform.TransformDirection(new Vector3(1.35f, 0.65f, 1.65f));
                cameraObject.transform.position = focus + frontRight;
                cameraObject.transform.LookAt(focus);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                Destroy(cameraObject);
            }
        }

        static void SaveTopgearSensorSuiteScreenshot(string path)
        {
            var physicsRoot = GameObject.Find(PhysicsRootName);
            var topgear = GameObject.Find(TopgearVisualRootName);
            var sensors = GameObject.Find(TopgearSensorRootName);
            if (physicsRoot == null || topgear == null || sensors == null)
            {
                SaveTopgearVisualScreenshot(path);
                return;
            }

            var bounds = CalculateRendererBounds(topgear);
            var sensorBounds = CalculateRendererBounds(sensors);
            bounds.Encapsulate(sensorBounds.min);
            bounds.Encapsulate(sensorBounds.max);

            var cameraObject = new GameObject("ScoutWheelGroundCandidate_TopgearSensorSuiteScreenshotCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = 20f;
                camera.fieldOfView = 30f;

                var focus = bounds.center + Vector3.up * 0.06f;
                var frontRight = physicsRoot.transform.TransformDirection(new Vector3(1.28f, 0.72f, 1.42f));
                cameraObject.transform.position = focus + frontRight;
                cameraObject.transform.LookAt(focus);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                Destroy(cameraObject);
            }

            string directory = Path.GetDirectoryName(path);
            string stem = Path.GetFileNameWithoutExtension(path);
            SaveTopgearSensorSuiteView(Path.Combine(directory, stem + "_front.png"), physicsRoot.transform, bounds, new Vector3(0f, 0.70f, 1.35f), 28f);
            SaveTopgearSensorSuiteView(Path.Combine(directory, stem + "_rear.png"), physicsRoot.transform, bounds, new Vector3(0f, 0.70f, -1.35f), 28f);
            SaveTopgearSensorSuiteView(Path.Combine(directory, stem + "_left.png"), physicsRoot.transform, bounds, new Vector3(-1.35f, 0.70f, 0f), 28f);
            SaveTopgearSensorSuiteView(Path.Combine(directory, stem + "_right.png"), physicsRoot.transform, bounds, new Vector3(1.35f, 0.70f, 0f), 28f);
            SaveTopgearSensorSuiteView(Path.Combine(directory, stem + "_top.png"), physicsRoot.transform, bounds, new Vector3(0.12f, 1.65f, 0.12f), 24f);
        }

        static void SaveTopgearSensorSuiteView(string path, Transform vehicleFrame, Bounds focusBounds, Vector3 localOffset, float fieldOfView)
        {
            var cameraObject = new GameObject("ScoutWheelGroundCandidate_TopgearSensorSuiteViewCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.56f, 0.66f, 0.76f);
                camera.nearClipPlane = 0.02f;
                camera.farClipPlane = 20f;
                camera.fieldOfView = fieldOfView;

                var focus = focusBounds.center + Vector3.up * 0.05f;
                cameraObject.transform.position = focus + vehicleFrame.TransformDirection(localOffset);
                cameraObject.transform.LookAt(focus);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                Destroy(cameraObject);
            }
        }

        void SaveNamedScreenshot(string resultKey, string fileName, string logMarker)
        {
            string path = Path.Combine(Application.dataPath, "../Logs/" + fileName);
            SaveViewerCameraScreenshot(path);
            File.AppendAllText(m_ResultPath, $"{resultKey}_screenshot={path}\n");
            Debug.Log($"{logMarker} {path}");
        }

        static void RenderCameraToPng(Camera camera, string path, int width, int height)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Destroy(renderTexture);
                Destroy(texture);
            }
        }

        static string FormatVector(Vector3 value)
        {
            return $"{value.x:F3},{value.y:F3},{value.z:F3}";
        }
    }
}
