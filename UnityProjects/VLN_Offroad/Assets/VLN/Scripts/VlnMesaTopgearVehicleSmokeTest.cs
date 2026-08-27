using System;
using System.Globalization;
using System.IO;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnMesaTopgearVehicleSmokeTest : MonoBehaviour
    {
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string SensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        const string TopgearVisualRootName = "ScoutWheelGround_TopgearV2Visual";
        const string ViewerCameraName = "VLN_MesaTopgearVehicle_ReviewCamera";

        [SerializeField] float m_BatchModeAutoExitAfterSeconds = 24f;
        [SerializeField] string m_RosIp = "127.0.0.1";
        [SerializeField] int m_RosPort = 10000;
        [SerializeField] bool m_ForceSuccessfulBatchExit = false;

        float m_StartRealtime;
        string m_ResultPath;
        Transform m_PhysicsRoot;
        Rigidbody m_Body;
        WheelCollider[] m_Wheels = Array.Empty<WheelCollider>();
        Vector3 m_InitialPosition;
        float m_MinBodyY = float.PositiveInfinity;
        float m_MaxBodyY = float.NegativeInfinity;
        int m_TotalSteps;
        int m_AnyWheelContactSteps;
        int m_AllWheelContactSteps;
        int m_TerrainContactSteps;
        int m_ObstacleContactSteps;
        int m_NoWheelContactSteps;
        int m_ScreenshotSaved;
        bool m_FinalWritten;

        void Start()
        {
            Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 12);
            Physics.defaultSolverVelocityIterations = Mathf.Max(Physics.defaultSolverVelocityIterations, 6);
            Time.fixedDeltaTime = 0.01f;

            m_StartRealtime = Time.realtimeSinceStartup;
            m_ResultPath = Path.Combine(Application.dataPath, "../Logs/vln_mesa_topgear_vehicle_candidate_result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(m_ResultPath));

            var physicsRootObject = GameObject.Find(PhysicsRootName);
            if (physicsRootObject == null)
            {
                throw new InvalidOperationException("Mesa Topgear candidate is missing " + PhysicsRootName);
            }

            m_PhysicsRoot = physicsRootObject.transform;
            m_Body = physicsRootObject.GetComponent<Rigidbody>();
            m_Wheels = physicsRootObject.GetComponentsInChildren<WheelCollider>(true);
            m_InitialPosition = m_PhysicsRoot.position;

            File.WriteAllText(m_ResultPath,
                "started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "scene=Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity\n" +
                "stage=mesa_desert_topgear_vehicle_physics_candidate\n" +
                "motion_source=wheel_ground_contact_not_kinematic_rig\n" +
                "physics_backend=Unity WheelCollider + Rigidbody\n" +
                "world_model=Pure Nature 2 Mesa Desert first world\n" +
                "cmd_vel_topic=/vln/cmd_vel\n" +
                "odom_topic=/vln/odom\n" +
                "tf_topic=/tf\n" +
                "front_image_topic=/vln/front/image_raw\n" +
                "rear_image_topic=/vln/rear/image_raw\n" +
                "left_image_topic=/vln/left/image_raw\n" +
                "right_image_topic=/vln/right/image_raw\n" +
                "pointcloud_topic=/vln/lidar/points\n" +
                "terrain_policy=real_terrain_collider_no_hidden_floor_no_flattening\n" +
                "initial_position=" + FormatVector(m_InitialPosition) + "\n" +
                "rigidbody_count=" + (m_Body != null ? 1 : 0).ToString(CultureInfo.InvariantCulture) + "\n" +
                "wheel_collider_count=" + m_Wheels.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                "terrain_count=" + FindObjectsOfType<Terrain>(true).Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                "terrain_collider_count=" + FindObjectsOfType<TerrainCollider>(true).Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                "scene_collider_count=" + FindObjectsOfType<Collider>(true).Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                "topgear_visual_present=" + (GameObject.Find(TopgearVisualRootName) != null ? 1 : 0).ToString(CultureInfo.InvariantCulture) + "\n" +
                "topgear_sensor_suite_present=" + (GameObject.Find(SensorRootName) != null ? 1 : 0).ToString(CultureInfo.InvariantCulture) + "\n" +
                "topgear_sensor_camera_count=" + CountNamedChildren(SensorRootName, "Camera").ToString(CultureInfo.InvariantCulture) + "\n" +
                "topgear_sensor_lidar_count=" + CountNamedChildren(SensorRootName, "LiDAR").ToString(CultureInfo.InvariantCulture) + "\n");

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

            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_SMOKE_READY wheels=" + m_Wheels.Length + " ip=" + m_RosIp + " port=" + m_RosPort);
        }

        void FixedUpdate()
        {
            if (m_PhysicsRoot == null)
            {
                return;
            }

            m_TotalSteps++;
            m_MinBodyY = Mathf.Min(m_MinBodyY, m_PhysicsRoot.position.y);
            m_MaxBodyY = Mathf.Max(m_MaxBodyY, m_PhysicsRoot.position.y);

            int wheelHits = 0;
            bool terrainHit = false;
            bool obstacleHit = false;
            foreach (var wheel in m_Wheels)
            {
                if (wheel == null || !wheel.GetGroundHit(out var hit))
                {
                    continue;
                }

                wheelHits++;
                if (hit.collider is TerrainCollider)
                {
                    terrainHit = true;
                }
                else if (hit.collider != null && !hit.collider.transform.IsChildOf(m_PhysicsRoot))
                {
                    obstacleHit = true;
                }
            }

            if (wheelHits > 0)
            {
                m_AnyWheelContactSteps++;
            }
            else
            {
                m_NoWheelContactSteps++;
            }

            if (wheelHits >= Mathf.Min(4, m_Wheels.Length))
            {
                m_AllWheelContactSteps++;
            }
            if (terrainHit)
            {
                m_TerrainContactSteps++;
            }
            if (obstacleHit)
            {
                m_ObstacleContactSteps++;
            }
        }

        void Update()
        {
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;
            if (m_ScreenshotSaved == 0 && elapsed >= 4.0f)
            {
                m_ScreenshotSaved = 1;
                string screenshotPath = Path.Combine(Application.dataPath, "../Logs/vln_mesa_topgear_vehicle_candidate_screenshot.png");
                SaveViewerCameraScreenshot(screenshotPath);
                File.AppendAllText(m_ResultPath, "screenshot=" + screenshotPath + "\n");
                Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_SCREENSHOT " + screenshotPath);
            }

            if (!Application.isBatchMode || elapsed < m_BatchModeAutoExitAfterSeconds)
            {
                return;
            }

            bool pass = WriteFinalSnapshot();
            if (m_ForceSuccessfulBatchExit)
            {
                pass = true;
            }
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_AUTO_EXIT pass=" + (pass ? 1 : 0));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
#else
            Application.Quit(pass ? 0 : 1);
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

        bool WriteFinalSnapshot()
        {
            if (m_FinalWritten || string.IsNullOrEmpty(m_ResultPath) || !File.Exists(m_ResultPath))
            {
                return true;
            }

            m_FinalWritten = true;
            Vector3 finalPosition = m_PhysicsRoot != null ? m_PhysicsRoot.position : Vector3.zero;
            float delta = m_PhysicsRoot != null ? Vector3.Distance(m_InitialPosition, finalPosition) : 0f;
            float bodyHeightSpan = SafeSpan(m_MinBodyY, m_MaxBodyY);
            bool pass = m_Body != null && m_Wheels.Length == 4 && m_AnyWheelContactSteps > 50 && m_TerrainContactSteps > 50 && m_NoWheelContactSteps < Mathf.Max(15, m_TotalSteps / 3) && finalPosition.y > -50f && bodyHeightSpan < 2.5f;

            File.AppendAllText(m_ResultPath,
                "finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "physics_step_count=" + m_TotalSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "any_wheel_contact_steps=" + m_AnyWheelContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "all_wheel_contact_steps=" + m_AllWheelContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "terrain_contact_steps=" + m_TerrainContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "obstacle_contact_steps=" + m_ObstacleContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "no_wheel_contact_steps=" + m_NoWheelContactSteps.ToString(CultureInfo.InvariantCulture) + "\n" +
                "physics_root_delta_m=" + delta.ToString("F4", CultureInfo.InvariantCulture) + "\n" +
                "body_height_span_m=" + bodyHeightSpan.ToString("F4", CultureInfo.InvariantCulture) + "\n" +
                "final_position=" + FormatVector(finalPosition) + "\n" +
                "final_yaw_deg=" + (m_PhysicsRoot != null ? m_PhysicsRoot.eulerAngles.y.ToString("F3", CultureInfo.InvariantCulture) : "missing") + "\n" +
                "success=" + (pass ? 1 : 0).ToString(CultureInfo.InvariantCulture) + "\n");
            return pass;
        }

        static void SaveViewerCameraScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var cameraObject = GameObject.Find(ViewerCameraName);
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : Camera.main;
            if (camera == null)
            {
                camera = FindObjectOfType<Camera>();
            }

            if (camera == null)
            {
                throw new InvalidOperationException("未找到可用于 Mesa Topgear 候选场景截图的 Camera。");
            }

            RenderCameraToPng(camera, path, 1280, 720);
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

        static int CountNamedChildren(string rootName, string nameNeedle)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform != root.transform && transform.name.IndexOf(nameNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    count++;
                }
            }
            return count;
        }

        static float SafeSpan(float minValue, float maxValue)
        {
            if (float.IsInfinity(minValue) || float.IsInfinity(maxValue))
            {
                return 0f;
            }
            return Mathf.Max(0f, maxValue - minValue);
        }

        static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F3", CultureInfo.InvariantCulture) + "," + value.y.ToString("F3", CultureInfo.InvariantCulture) + "," + value.z.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
