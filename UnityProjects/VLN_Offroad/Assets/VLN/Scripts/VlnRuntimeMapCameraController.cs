using UnityEngine;

namespace VLN.ROS2
{
    public sealed class VlnRuntimeMapCameraController : MonoBehaviour
    {
        [SerializeField] string m_TargetName = "Offroad_SensorRig_StaticVehiclePlaceholder";
        [SerializeField] Vector3 m_TargetOffset = new(0f, 1.0f, 0f);
        [SerializeField] float m_MinDistance = 2.0f;
        [SerializeField] float m_MaxDistance = 80.0f;
        [SerializeField] float m_RotateSensitivity = 0.18f;
        [SerializeField] float m_PanSensitivity = 0.0022f;
        [SerializeField] float m_ZoomSensitivity = 0.12f;
        [SerializeField] float m_MoveSpeed = 12.0f;

        Vector3 m_Pivot;
        float m_Distance;
        float m_Yaw;
        float m_Pitch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallControllersForOpenScenes()
        {
            ConfigureCameraIfPresent("Offroad_ViewerCamera", "Offroad_SensorRig_StaticVehiclePlaceholder", new Vector3(0f, 1.1f, 0f), 2.0f, 90.0f);
            ConfigureCameraIfPresent("VehicleCandidate_GameCamera", "HuskyVisual_Root", new Vector3(0f, 0.55f, 0f), 1.2f, 45.0f);
        }

        static void ConfigureCameraIfPresent(string cameraName, string targetName, Vector3 targetOffset, float minDistance, float maxDistance)
        {
            var cameraObject = GameObject.Find(cameraName);
            if (cameraObject == null || cameraObject.GetComponent<Camera>() == null)
            {
                return;
            }

            var controller = cameraObject.GetComponent<VlnRuntimeMapCameraController>();
            if (controller == null)
            {
                controller = cameraObject.AddComponent<VlnRuntimeMapCameraController>();
            }

            controller.Configure(targetName, targetOffset, minDistance, maxDistance);
        }

        public void Configure(string targetName, Vector3 targetOffset, float minDistance = 2.0f, float maxDistance = 80.0f)
        {
            m_TargetName = targetName;
            m_TargetOffset = targetOffset;
            m_MinDistance = minDistance;
            m_MaxDistance = maxDistance;
        }

        void Start()
        {
            var target = GameObject.Find(m_TargetName);
            m_Pivot = target != null ? target.transform.position + m_TargetOffset : transform.position + transform.forward * 10.0f;
            m_Distance = Mathf.Clamp(Vector3.Distance(transform.position, m_Pivot), m_MinDistance, m_MaxDistance);
            m_Yaw = transform.eulerAngles.y;
            m_Pitch = NormalizePitch(transform.eulerAngles.x);
            ApplyCameraTransform();
        }

        void Update()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            bool changed = false;
            changed |= HandleOrbitDrag();
            changed |= HandlePanDrag();
            changed |= HandleZoom();
            changed |= HandleKeyboardMove();

            if (changed)
            {
                ApplyCameraTransform();
            }
        }

        bool HandleOrbitDrag()
        {
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                return false;
            }

            m_Yaw += Input.GetAxis("Mouse X") * m_RotateSensitivity * 60.0f;
            m_Pitch -= Input.GetAxis("Mouse Y") * m_RotateSensitivity * 60.0f;
            m_Pitch = Mathf.Clamp(m_Pitch, -5.0f, 82.0f);
            return true;
        }

        bool HandlePanDrag()
        {
            if (!Input.GetMouseButton(2))
            {
                return false;
            }

            float scale = Mathf.Max(m_Distance, 1.0f) * m_PanSensitivity;
            m_Pivot += (-transform.right * Input.GetAxis("Mouse X") - transform.up * Input.GetAxis("Mouse Y")) * scale * 60.0f;
            return true;
        }

        bool HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.001f)
            {
                return false;
            }

            m_Distance = Mathf.Clamp(m_Distance * (1.0f - scroll * m_ZoomSensitivity), m_MinDistance, m_MaxDistance);
            return true;
        }

        bool HandleKeyboardMove()
        {
            if (!Input.GetMouseButton(1))
            {
                return false;
            }

            Vector3 movement = Vector3.zero;
            movement += transform.forward * Axis(KeyCode.W, KeyCode.S);
            movement += transform.right * Axis(KeyCode.D, KeyCode.A);
            movement += Vector3.up * Axis(KeyCode.E, KeyCode.Q);

            if (movement.sqrMagnitude < 0.001f)
            {
                return false;
            }

            float speed = m_MoveSpeed * Mathf.Max(m_Distance / 12.0f, 0.35f) * Time.unscaledDeltaTime;
            m_Pivot += movement.normalized * speed;
            return true;
        }

        void ApplyCameraTransform()
        {
            var rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            transform.SetPositionAndRotation(m_Pivot - rotation * Vector3.forward * m_Distance, rotation);
        }

        static float Axis(KeyCode positive, KeyCode negative)
        {
            float value = 0f;
            if (Input.GetKey(positive))
            {
                value += 1f;
            }
            if (Input.GetKey(negative))
            {
                value -= 1f;
            }
            return value;
        }

        static float NormalizePitch(float pitch)
        {
            return pitch > 180.0f ? pitch - 360.0f : pitch;
        }
    }
}
