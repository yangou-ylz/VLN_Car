using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RosMessageTypes.Geometry;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

namespace VLN.ROS2
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VlnMesaTopgearIssueRecorder : MonoBehaviour
    {
        [SerializeField] string m_CmdVelTopic = "/vln/cmd_vel";
        [SerializeField] float m_SampleIntervalSeconds = 0.05f;
        [SerializeField] float m_CommandTimeoutSeconds = 0.45f;
        [SerializeField] KeyCode m_StartRecordingKey = KeyCode.F6;
        [SerializeField] KeyCode m_StopRecordingKey = KeyCode.F7;
        [SerializeField] KeyCode m_MarkIssueKey = KeyCode.F8;
        [SerializeField] KeyCode m_WriteSummaryKey = KeyCode.F9;
        [SerializeField] KeyCode m_ScreenshotKey = KeyCode.F10;
        [SerializeField] bool m_ShowRecordingHud = true;

        Rigidbody m_Body;
        WheelCollider[] m_Wheels = Array.Empty<WheelCollider>();
        Terrain[] m_Terrains = Array.Empty<Terrain>();
        StreamWriter m_SampleWriter;
        string m_RunDirectory;
        string m_SamplePath;
        string m_EventPath;
        string m_MetadataPath;
        string m_SummaryPath;
        bool m_IsRecording;
        bool m_HasRecordingStarted;
        bool m_RecorderClosed;
        int m_RunDirectoryCounter;
        string m_RecordingStartedUtc = "missing";
        string m_RecordingStoppedUtc = "missing";
        float m_RecordingStartRealtime = float.NaN;
        float m_RecordingStopRealtime = float.NaN;
        float m_NextSampleTime;
        float m_LastCommandRealtime = -999f;
        float m_CommandedLinearX;
        float m_CommandedAngularZ;
        int m_CommandCount;
        int m_SampleCount;
        int m_MarkedIssueCount;
        int m_CollisionEnterCount;
        int m_CollisionStayCount;
        int m_StuckSampleCount;
        int m_LongestStuckStreak;
        int m_CurrentStuckStreak;
        float m_MaxTerrainSlopeUnderBody;
        float m_MaxRaycastSlopeUnderBody;
        float m_MaxWheelSlope;
        float m_MaxAbsForwardSlip;
        float m_MaxAbsSidewaysSlip;
        float m_MaxAbsRpm;
        float m_MaxAbsMotorTorque;
        float m_MaxBrakeTorque;
        float m_MinForwardSpeed = float.PositiveInfinity;
        float m_MaxForwardSpeed = float.NegativeInfinity;
        readonly HashSet<string> m_TouchedColliders = new HashSet<string>();
        readonly List<string> m_RecentEvents = new List<string>();
        GUIStyle m_HudTextStyle;
        GUIStyle m_HudSmallTextStyle;

        public bool IsRecording => m_IsRecording;
        public bool HasRecordingStarted => m_HasRecordingStarted;
        public string RunDirectory => m_RunDirectory;
        public int SampleCount => m_SampleCount;
        public int MarkedIssueCount => m_MarkedIssueCount;

        void Start()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Wheels = GetComponentsInChildren<WheelCollider>(true);
            m_Terrains = FindObjectsOfType<Terrain>(true);
            PrepareRunDirectory("recorder_ready");

            try
            {
                ROSConnection.GetOrCreateInstance().Subscribe<TwistMsg>(m_CmdVelTopic, OnCmdVel);
            }
            catch (Exception ex)
            {
                AppendEvent("ROS_SUBSCRIBE_FAILED " + ex.GetType().Name + " " + ex.Message);
            }

            Debug.Log("VLN_MESA_ISSUE_RECORDER_READY dir=" + m_RunDirectory + " start=" + m_StartRecordingKey + " stop=" + m_StopRecordingKey + " mark=" + m_MarkIssueKey + " screenshot=" + m_ScreenshotKey);
        }

        public void BeginRecordingFromMenu()
        {
            StartRecording();
        }

        public void EndRecordingFromMenu()
        {
            StopRecording("unity_menu", captureEndScreenshot: true);
        }

        public void MarkIssueFromMenu()
        {
            MarkIssue("unity_menu");
        }

        public void CaptureScreenshotFromMenu()
        {
            CaptureScreenshot("unity_menu_screenshot");
        }

        public void WriteSummaryFromMenu()
        {
            AppendEvent("WRITE_SUMMARY_MENU time_s=" + Time.time.ToString("F3", CultureInfo.InvariantCulture) + " pos=" + FormatVector(transform.position));
            WriteSummary();
        }

        void PrepareRunDirectory(string reason)
        {
            DisposeSampleWriter();
            string root = Path.Combine(Application.dataPath, "../Logs/mesa_issue_records");
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string runId = "mesa_issue_" + stamp;
            string candidate = Path.Combine(root, runId);
            while (Directory.Exists(candidate))
            {
                m_RunDirectoryCounter++;
                runId = "mesa_issue_" + stamp + "_segment" + m_RunDirectoryCounter.ToString("D2", CultureInfo.InvariantCulture);
                candidate = Path.Combine(root, runId);
            }

            m_RunDirectory = candidate;
            Directory.CreateDirectory(m_RunDirectory);
            m_SamplePath = Path.Combine(m_RunDirectory, "samples.csv");
            m_EventPath = Path.Combine(m_RunDirectory, "events.txt");
            m_MetadataPath = Path.Combine(m_RunDirectory, "metadata.txt");
            m_SummaryPath = Path.Combine(m_RunDirectory, "summary.txt");

            File.WriteAllText(m_MetadataPath,
                "created_utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "create_reason=" + reason + "\n" +
                "stage=mesa_topgear_manual_terrain_issue_segment_recording\n" +
                "scene=Assets/VLN/Scenes/VLNMesaDesertTopgearVehicleCandidate.unity\n" +
                "vehicle_root=" + transform.name + "\n" +
                "cmd_vel_topic=" + m_CmdVelTopic + "\n" +
                "sample_interval_s=" + m_SampleIntervalSeconds.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "wheel_collider_count=" + m_Wheels.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                "terrain_count=" + m_Terrains.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                "start_recording_key=" + m_StartRecordingKey + "\n" +
                "stop_recording_key=" + m_StopRecordingKey + "\n" +
                "mark_issue_key=" + m_MarkIssueKey + "\n" +
                "write_summary_key=" + m_WriteSummaryKey + "\n" +
                "screenshot_key=" + m_ScreenshotKey + "\n" +
                "show_recording_hud=" + (m_ShowRecordingHud ? "1" : "0") + "\n" +
                "usage=Press F6 before entering the bad terrain, drive through the whole problematic segment, press F8 to mark points during recording, press F7 to stop, then run scripts/analyze_mesa_issue_recording.py.\n");
            File.WriteAllText(m_EventPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " RECORDER_READY reason=" + reason + " dir=" + m_RunDirectory + "\n");
        }

        void OpenSampleWriter()
        {
            DisposeSampleWriter();
            m_SampleWriter = new StreamWriter(m_SamplePath, append: false);
            m_SampleWriter.WriteLine("sample,time_s,recording_time_s,utc,pos_x,pos_y,pos_z,yaw_deg,pitch_deg,roll_deg,speed_mps,forward_speed_mps,lateral_speed_mps,vertical_speed_mps,yaw_rate_radps,cmd_linear_x,cmd_angular_z,cmd_age_s,command_active,wheel_contact_count,terrain_wheel_contact_count,other_wheel_contact_count,max_wheel_slope_deg,avg_wheel_slope_deg,avg_forward_slip,max_abs_forward_slip,avg_sideways_slip,max_abs_sideways_slip,avg_wheel_rpm,max_abs_wheel_rpm,avg_motor_torque_nm,max_abs_motor_torque_nm,avg_brake_torque_nm,max_brake_torque_nm,terrain_height_under_body,terrain_slope_under_body_deg,terrain_clearance_from_root_m,raycast_collider,raycast_distance_m,raycast_slope_deg,raycast_clearance_from_root_m,stuck_signal,collider_names");
            m_SampleWriter.Flush();
        }

        void DisposeSampleWriter()
        {
            if (m_SampleWriter == null)
            {
                return;
            }
            m_SampleWriter.Flush();
            m_SampleWriter.Dispose();
            m_SampleWriter = null;
        }

        void ResetRecordingStats()
        {
            m_CommandCount = 0;
            m_SampleCount = 0;
            m_MarkedIssueCount = 0;
            m_CollisionEnterCount = 0;
            m_CollisionStayCount = 0;
            m_StuckSampleCount = 0;
            m_LongestStuckStreak = 0;
            m_CurrentStuckStreak = 0;
            m_MaxTerrainSlopeUnderBody = 0f;
            m_MaxRaycastSlopeUnderBody = 0f;
            m_MaxWheelSlope = 0f;
            m_MaxAbsForwardSlip = 0f;
            m_MaxAbsSidewaysSlip = 0f;
            m_MaxAbsRpm = 0f;
            m_MaxAbsMotorTorque = 0f;
            m_MaxBrakeTorque = 0f;
            m_MinForwardSpeed = float.PositiveInfinity;
            m_MaxForwardSpeed = float.NegativeInfinity;
            m_LastCommandRealtime = -999f;
            m_CommandedLinearX = 0f;
            m_CommandedAngularZ = 0f;
            m_TouchedColliders.Clear();
            m_NextSampleTime = Time.realtimeSinceStartup;
        }

        void StartRecording()
        {
            if (m_IsRecording)
            {
                AppendEvent("RECORDING_START_IGNORED already_recording=1 dir=" + m_RunDirectory);
                return;
            }
            if (m_HasRecordingStarted)
            {
                PrepareRunDirectory("manual_restart_after_previous_segment");
            }

            ResetRecordingStats();
            OpenSampleWriter();
            m_IsRecording = true;
            m_HasRecordingStarted = true;
            m_RecorderClosed = false;
            m_RecordingStartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            m_RecordingStoppedUtc = "missing";
            m_RecordingStartRealtime = Time.realtimeSinceStartup;
            m_RecordingStopRealtime = float.NaN;
            AppendEvent("RECORDING_STARTED time_s=" + Time.time.ToString("F3", CultureInfo.InvariantCulture) + " pos=" + FormatVector(transform.position) + " dir=" + m_RunDirectory);
        }

        void StopRecording(string reason, bool captureEndScreenshot)
        {
            if (!m_IsRecording)
            {
                AppendEvent("RECORDING_STOP_IGNORED reason=" + reason + " recording=0 dir=" + m_RunDirectory);
                WriteSummary();
                return;
            }

            WriteSample();
            m_IsRecording = false;
            m_RecordingStoppedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            m_RecordingStopRealtime = Time.realtimeSinceStartup;
            AppendEvent("RECORDING_STOPPED reason=" + reason + " duration_s=" + RecordingDurationSeconds().ToString("F3", CultureInfo.InvariantCulture) + " samples=" + m_SampleCount.ToString(CultureInfo.InvariantCulture) + " pos=" + FormatVector(transform.position));
            if (captureEndScreenshot)
            {
                CaptureScreenshot("recording_end");
            }
            WriteSummary();
            DisposeSampleWriter();
        }

        void OnCmdVel(TwistMsg msg)
        {
            m_CommandedLinearX = (float)msg.linear.x;
            m_CommandedAngularZ = (float)msg.angular.z;
            m_LastCommandRealtime = Time.realtimeSinceStartup;
            m_CommandCount++;
        }

        void FixedUpdate()
        {
            if (!m_IsRecording)
            {
                return;
            }
            if (Time.realtimeSinceStartup < m_NextSampleTime)
            {
                return;
            }
            m_NextSampleTime = Time.realtimeSinceStartup + Mathf.Max(0.01f, m_SampleIntervalSeconds);
            WriteSample();
        }

        void Update()
        {
            if (Input.GetKeyDown(m_StartRecordingKey))
            {
                StartRecording();
            }
            if (Input.GetKeyDown(m_StopRecordingKey))
            {
                StopRecording("stop_key", captureEndScreenshot: true);
            }
            if (Input.GetKeyDown(m_MarkIssueKey))
            {
                MarkIssue("hotkey");
            }
            if (Input.GetKeyDown(m_ScreenshotKey))
            {
                CaptureScreenshot("manual_screenshot");
            }
            if (Input.GetKeyDown(m_WriteSummaryKey))
            {
                AppendEvent("WRITE_SUMMARY_KEY time_s=" + Time.time.ToString("F3", CultureInfo.InvariantCulture) + " pos=" + FormatVector(transform.position));
                WriteSummary();
            }
        }

        void OnGUI()
        {
            if (!m_ShowRecordingHud)
            {
                return;
            }

            EnsureHudStyles();
            string status = m_IsRecording ? "录制中" : (m_HasRecordingStarted ? "已停止" : "待命");
            string actionHint = m_IsRecording ? "F7 停止 / F8 标记 / F10 截图" : "F6 开始录制问题路段";
            string directory = string.IsNullOrWhiteSpace(m_RunDirectory) ? "目录未就绪" : m_RunDirectory;
            if (directory.Length > 68)
            {
                directory = "..." + directory.Substring(directory.Length - 65);
            }

            var previousColor = GUI.color;
            GUI.color = m_IsRecording ? new Color(0.05f, 0.55f, 0.18f, 0.88f) : new Color(0.18f, 0.18f, 0.18f, 0.82f);
            GUI.Box(new Rect(12f, 12f, 430f, 112f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 20f, 390f, 24f), "Mesa 问题轨迹：" + status, m_HudTextStyle);
            GUI.Label(new Rect(24f, 48f, 390f, 20f), actionHint, m_HudSmallTextStyle);
            GUI.Label(new Rect(24f, 70f, 390f, 20f), "样本 " + m_SampleCount.ToString(CultureInfo.InvariantCulture) + " / 标记 " + m_MarkedIssueCount.ToString(CultureInfo.InvariantCulture), m_HudSmallTextStyle);
            GUI.Label(new Rect(24f, 92f, 400f, 20f), directory, m_HudSmallTextStyle);
            GUI.color = previousColor;
        }

        void EnsureHudStyles()
        {
            if (m_HudTextStyle != null && m_HudSmallTextStyle != null)
            {
                return;
            }
            m_HudTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            m_HudSmallTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }

        void MarkIssue(string source)
        {
            if (!m_IsRecording)
            {
                AppendEvent("MARK_ISSUE_IGNORED source=" + source + " recording=0 press_start_key=" + m_StartRecordingKey + " pos=" + FormatVector(transform.position));
                CaptureScreenshot("mark_ignored_not_recording");
                return;
            }
            m_MarkedIssueCount++;
            AppendEvent("MARK_ISSUE source=" + source + " index=" + m_MarkedIssueCount.ToString(CultureInfo.InvariantCulture) + " time_s=" + Time.time.ToString("F3", CultureInfo.InvariantCulture) + " pos=" + FormatVector(transform.position));
            CaptureScreenshot("marked_issue_" + m_MarkedIssueCount.ToString("D2", CultureInfo.InvariantCulture));
            WriteSummary();
        }

        void OnCollisionEnter(Collision collision)
        {
            RecordCollision(collision, true);
        }

        void OnCollisionStay(Collision collision)
        {
            RecordCollision(collision, false);
        }

        void OnApplicationQuit()
        {
            CloseRecording();
        }

        void OnDestroy()
        {
            CloseRecording();
        }

        void WriteSample()
        {
            if (m_SampleWriter == null || m_Body == null)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(m_Body.velocity);
            float forwardSpeed = localVelocity.z;
            float lateralSpeed = localVelocity.x;
            float verticalSpeed = m_Body.velocity.y;
            float yawRate = Vector3.Dot(m_Body.angularVelocity, Vector3.up);
            float cmdAge = Time.realtimeSinceStartup - m_LastCommandRealtime;
            bool commandActive = m_CommandCount > 0 && cmdAge <= Mathf.Max(0.05f, m_CommandTimeoutSeconds);

            int wheelContacts = 0;
            int terrainWheelContacts = 0;
            int otherWheelContacts = 0;
            float slopeSum = 0f;
            float maxWheelSlope = 0f;
            float forwardSlipSum = 0f;
            float sidewaysSlipSum = 0f;
            float maxForwardSlip = 0f;
            float maxSidewaysSlip = 0f;
            float rpmSum = 0f;
            float maxAbsRpm = 0f;
            float motorTorqueSum = 0f;
            float maxAbsMotorTorque = 0f;
            float brakeTorqueSum = 0f;
            float maxBrakeTorque = 0f;
            var colliderNames = new List<string>();

            foreach (var wheel in m_Wheels)
            {
                if (wheel == null)
                {
                    continue;
                }

                rpmSum += wheel.rpm;
                maxAbsRpm = Mathf.Max(maxAbsRpm, Mathf.Abs(wheel.rpm));
                motorTorqueSum += wheel.motorTorque;
                maxAbsMotorTorque = Mathf.Max(maxAbsMotorTorque, Mathf.Abs(wheel.motorTorque));
                brakeTorqueSum += wheel.brakeTorque;
                maxBrakeTorque = Mathf.Max(maxBrakeTorque, wheel.brakeTorque);

                if (!wheel.GetGroundHit(out WheelHit hit))
                {
                    continue;
                }

                wheelContacts++;
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                slopeSum += slope;
                maxWheelSlope = Mathf.Max(maxWheelSlope, slope);
                forwardSlipSum += hit.forwardSlip;
                sidewaysSlipSum += hit.sidewaysSlip;
                maxForwardSlip = Mathf.Max(maxForwardSlip, Mathf.Abs(hit.forwardSlip));
                maxSidewaysSlip = Mathf.Max(maxSidewaysSlip, Mathf.Abs(hit.sidewaysSlip));

                if (hit.collider is TerrainCollider)
                {
                    terrainWheelContacts++;
                }
                else if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                {
                    otherWheelContacts++;
                }

                if (hit.collider != null)
                {
                    string path = HierarchyPath(hit.collider.gameObject);
                    if (!colliderNames.Contains(path))
                    {
                        colliderNames.Add(path);
                    }
                    m_TouchedColliders.Add(path);
                }
            }

            SampleTerrainUnderBody(out float terrainHeight, out float terrainSlope, out float terrainClearance);
            SampleRaycastUnderBody(out string raycastCollider, out float raycastDistance, out float raycastSlope, out float raycastClearance);

            bool stuck = commandActive && Mathf.Abs(m_CommandedLinearX) >= 0.20f && Mathf.Sign(m_CommandedLinearX) * forwardSpeed < 0.12f && wheelContacts >= 2;
            if (stuck)
            {
                m_StuckSampleCount++;
                m_CurrentStuckStreak++;
                m_LongestStuckStreak = Mathf.Max(m_LongestStuckStreak, m_CurrentStuckStreak);
            }
            else
            {
                m_CurrentStuckStreak = 0;
            }

            m_MaxTerrainSlopeUnderBody = Mathf.Max(m_MaxTerrainSlopeUnderBody, terrainSlope);
            m_MaxRaycastSlopeUnderBody = Mathf.Max(m_MaxRaycastSlopeUnderBody, raycastSlope);
            m_MaxWheelSlope = Mathf.Max(m_MaxWheelSlope, maxWheelSlope);
            m_MaxAbsForwardSlip = Mathf.Max(m_MaxAbsForwardSlip, maxForwardSlip);
            m_MaxAbsSidewaysSlip = Mathf.Max(m_MaxAbsSidewaysSlip, maxSidewaysSlip);
            m_MaxAbsRpm = Mathf.Max(m_MaxAbsRpm, maxAbsRpm);
            m_MaxAbsMotorTorque = Mathf.Max(m_MaxAbsMotorTorque, maxAbsMotorTorque);
            m_MaxBrakeTorque = Mathf.Max(m_MaxBrakeTorque, maxBrakeTorque);
            m_MinForwardSpeed = Mathf.Min(m_MinForwardSpeed, forwardSpeed);
            m_MaxForwardSpeed = Mathf.Max(m_MaxForwardSpeed, forwardSpeed);

            m_SampleCount++;
            float wheelDenominator = Mathf.Max(1, m_Wheels.Length);
            float contactDenominator = Mathf.Max(1, wheelContacts);
            Vector3 euler = transform.eulerAngles;
            m_SampleWriter.WriteLine(string.Join(",", new[]
            {
                m_SampleCount.ToString(CultureInfo.InvariantCulture),
                Time.time.ToString("F3", CultureInfo.InvariantCulture),
                RecordingElapsedSeconds().ToString("F3", CultureInfo.InvariantCulture),
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                transform.position.x.ToString("F3", CultureInfo.InvariantCulture),
                transform.position.y.ToString("F3", CultureInfo.InvariantCulture),
                transform.position.z.ToString("F3", CultureInfo.InvariantCulture),
                euler.y.ToString("F3", CultureInfo.InvariantCulture),
                NormalizeAngle(euler.x).ToString("F3", CultureInfo.InvariantCulture),
                NormalizeAngle(euler.z).ToString("F3", CultureInfo.InvariantCulture),
                m_Body.velocity.magnitude.ToString("F3", CultureInfo.InvariantCulture),
                forwardSpeed.ToString("F3", CultureInfo.InvariantCulture),
                lateralSpeed.ToString("F3", CultureInfo.InvariantCulture),
                verticalSpeed.ToString("F3", CultureInfo.InvariantCulture),
                yawRate.ToString("F3", CultureInfo.InvariantCulture),
                m_CommandedLinearX.ToString("F3", CultureInfo.InvariantCulture),
                m_CommandedAngularZ.ToString("F3", CultureInfo.InvariantCulture),
                cmdAge.ToString("F3", CultureInfo.InvariantCulture),
                commandActive ? "1" : "0",
                wheelContacts.ToString(CultureInfo.InvariantCulture),
                terrainWheelContacts.ToString(CultureInfo.InvariantCulture),
                otherWheelContacts.ToString(CultureInfo.InvariantCulture),
                maxWheelSlope.ToString("F3", CultureInfo.InvariantCulture),
                (slopeSum / contactDenominator).ToString("F3", CultureInfo.InvariantCulture),
                (forwardSlipSum / contactDenominator).ToString("F3", CultureInfo.InvariantCulture),
                maxForwardSlip.ToString("F3", CultureInfo.InvariantCulture),
                (sidewaysSlipSum / contactDenominator).ToString("F3", CultureInfo.InvariantCulture),
                maxSidewaysSlip.ToString("F3", CultureInfo.InvariantCulture),
                (rpmSum / wheelDenominator).ToString("F3", CultureInfo.InvariantCulture),
                maxAbsRpm.ToString("F3", CultureInfo.InvariantCulture),
                (motorTorqueSum / wheelDenominator).ToString("F3", CultureInfo.InvariantCulture),
                maxAbsMotorTorque.ToString("F3", CultureInfo.InvariantCulture),
                (brakeTorqueSum / wheelDenominator).ToString("F3", CultureInfo.InvariantCulture),
                maxBrakeTorque.ToString("F3", CultureInfo.InvariantCulture),
                terrainHeight.ToString("F3", CultureInfo.InvariantCulture),
                terrainSlope.ToString("F3", CultureInfo.InvariantCulture),
                terrainClearance.ToString("F3", CultureInfo.InvariantCulture),
                Csv(raycastCollider),
                raycastDistance.ToString("F3", CultureInfo.InvariantCulture),
                raycastSlope.ToString("F3", CultureInfo.InvariantCulture),
                raycastClearance.ToString("F3", CultureInfo.InvariantCulture),
                stuck ? "1" : "0",
                Csv(string.Join(" | ", colliderNames))
            }));

            if (m_SampleCount % 10 == 0)
            {
                m_SampleWriter.Flush();
            }
        }

        void SampleTerrainUnderBody(out float height, out float slope, out float clearance)
        {
            height = float.NaN;
            slope = 0f;
            clearance = float.NaN;
            foreach (var terrain in m_Terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 local = transform.position - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                {
                    continue;
                }

                float nx = Mathf.Clamp01(local.x / Mathf.Max(0.001f, size.x));
                float nz = Mathf.Clamp01(local.z / Mathf.Max(0.001f, size.z));
                height = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
                slope = terrain.terrainData.GetSteepness(nx, nz);
                clearance = transform.position.y - height;
                return;
            }
        }

        void SampleRaycastUnderBody(out string colliderName, out float distance, out float slope, out float clearance)
        {
            colliderName = "none";
            distance = float.NaN;
            slope = 0f;
            clearance = float.NaN;
            Vector3 origin = transform.position + Vector3.up * 4f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                colliderName = HierarchyPath(hit.collider.gameObject);
                distance = hit.distance;
                slope = Vector3.Angle(hit.normal, Vector3.up);
                clearance = transform.position.y - hit.point.y;
                return;
            }
        }

        void RecordCollision(Collision collision, bool enter)
        {
            if (!m_IsRecording)
            {
                return;
            }
            if (collision == null || collision.collider == null || collision.collider.isTrigger || collision.collider.transform.IsChildOf(transform))
            {
                return;
            }
            if (enter)
            {
                m_CollisionEnterCount++;
            }
            else
            {
                m_CollisionStayCount++;
            }
            string path = HierarchyPath(collision.collider.gameObject);
            m_TouchedColliders.Add(path);
            if (enter || m_CollisionStayCount % 50 == 0)
            {
                AppendEvent((enter ? "COLLISION_ENTER" : "COLLISION_STAY") + " time_s=" + Time.time.ToString("F3", CultureInfo.InvariantCulture) + " collider=" + path + " relative_velocity=" + collision.relativeVelocity.magnitude.ToString("F3", CultureInfo.InvariantCulture));
            }
        }

        void CaptureScreenshot(string label)
        {
            string safeLabel = string.IsNullOrWhiteSpace(label) ? "screenshot" : label.Replace(' ', '_');
            string path = Path.Combine(m_RunDirectory, safeLabel + "_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".png");
            ScreenCapture.CaptureScreenshot(path);
            AppendEvent("SCREENSHOT label=" + safeLabel + " path=" + path);
        }

        void AppendEvent(string line)
        {
            string fullLine = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + line;
            m_RecentEvents.Add(fullLine);
            if (m_RecentEvents.Count > 80)
            {
                m_RecentEvents.RemoveAt(0);
            }
            if (!string.IsNullOrEmpty(m_EventPath))
            {
                File.AppendAllText(m_EventPath, fullLine + "\n");
            }
            Debug.Log("VLN_MESA_ISSUE_RECORDER_EVENT " + line);
        }

        void WriteSummary()
        {
            if (string.IsNullOrEmpty(m_SummaryPath))
            {
                return;
            }
            if (m_SampleWriter != null)
            {
                m_SampleWriter.Flush();
            }
            File.WriteAllText(m_SummaryPath,
                "updated=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                "run_directory=" + m_RunDirectory + "\n" +
                "is_recording=" + (m_IsRecording ? "1" : "0") + "\n" +
                "has_recording_started=" + (m_HasRecordingStarted ? "1" : "0") + "\n" +
                "recording_started_utc=" + m_RecordingStartedUtc + "\n" +
                "recording_stopped_utc=" + m_RecordingStoppedUtc + "\n" +
                "recording_duration_s=" + RecordingDurationSeconds().ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "start_recording_key=" + m_StartRecordingKey + "\n" +
                "stop_recording_key=" + m_StopRecordingKey + "\n" +
                "mark_issue_key=" + m_MarkIssueKey + "\n" +
                "screenshot_key=" + m_ScreenshotKey + "\n" +
                "sample_count=" + m_SampleCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "command_count=" + m_CommandCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "marked_issue_count=" + m_MarkedIssueCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "stuck_sample_count=" + m_StuckSampleCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "longest_stuck_streak_samples=" + m_LongestStuckStreak.ToString(CultureInfo.InvariantCulture) + "\n" +
                "max_terrain_slope_under_body_deg=" + m_MaxTerrainSlopeUnderBody.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_raycast_slope_under_body_deg=" + m_MaxRaycastSlopeUnderBody.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_wheel_slope_deg=" + m_MaxWheelSlope.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_abs_forward_slip=" + m_MaxAbsForwardSlip.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_abs_sideways_slip=" + m_MaxAbsSidewaysSlip.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_abs_wheel_rpm=" + m_MaxAbsRpm.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_abs_motor_torque_nm=" + m_MaxAbsMotorTorque.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "max_brake_torque_nm=" + m_MaxBrakeTorque.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "min_forward_speed_mps=" + (float.IsInfinity(m_MinForwardSpeed) ? "missing" : m_MinForwardSpeed.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                "max_forward_speed_mps=" + (float.IsInfinity(m_MaxForwardSpeed) ? "missing" : m_MaxForwardSpeed.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                "collision_enter_count=" + m_CollisionEnterCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "collision_stay_count=" + m_CollisionStayCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                "unique_touched_collider_count=" + m_TouchedColliders.Count.ToString(CultureInfo.InvariantCulture) + "\n" +
                "touched_colliders=" + string.Join(" | ", m_TouchedColliders) + "\n" +
                "samples_csv=" + m_SamplePath + "\n" +
                "events_txt=" + m_EventPath + "\n");
        }

        void CloseRecording()
        {
            if (m_RecorderClosed)
            {
                return;
            }
            if (m_IsRecording)
            {
                StopRecording("application_close", captureEndScreenshot: false);
            }
            else
            {
                WriteSummary();
                DisposeSampleWriter();
                AppendEvent("RECORDER_CLOSED recording_started=" + (m_HasRecordingStarted ? "1" : "0") + " samples=" + m_SampleCount.ToString(CultureInfo.InvariantCulture));
            }
            m_RecorderClosed = true;
        }

        float RecordingElapsedSeconds()
        {
            if (!m_HasRecordingStarted || float.IsNaN(m_RecordingStartRealtime))
            {
                return 0f;
            }
            return Mathf.Max(0f, Time.realtimeSinceStartup - m_RecordingStartRealtime);
        }

        float RecordingDurationSeconds()
        {
            if (!m_HasRecordingStarted || float.IsNaN(m_RecordingStartRealtime))
            {
                return 0f;
            }
            float end = float.IsNaN(m_RecordingStopRealtime) ? Time.realtimeSinceStartup : m_RecordingStopRealtime;
            return Mathf.Max(0f, end - m_RecordingStartRealtime);
        }

        static float NormalizeAngle(float angle)
        {
            return Mathf.DeltaAngle(0f, angle);
        }

        static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F3", CultureInfo.InvariantCulture) + "," + value.y.ToString("F3", CultureInfo.InvariantCulture) + "," + value.z.ToString("F3", CultureInfo.InvariantCulture);
        }

        static string HierarchyPath(GameObject go)
        {
            if (go == null)
            {
                return "none";
            }
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

        static string Csv(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
