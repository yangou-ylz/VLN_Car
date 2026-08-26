using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnitySensors.Sensor.Camera;

namespace VLN.Editor
{
    public sealed class VlnTopgearCameraPreviewWindow : EditorWindow
    {
        enum PreviewMode
        {
            All,
            Front,
            Rear,
            Left,
            Right,
        }

        const string FrontCameraName = "Topgear_Front_RGBCamera_UnitySensorsROS";
        const string RearCameraName = "Topgear_Rear_RGBCamera_UnitySensorsROS";
        const string LeftCameraName = "Topgear_Left_RGBCamera_UnitySensorsROS";
        const string RightCameraName = "Topgear_Right_RGBCamera_UnitySensorsROS";

        static readonly Dictionary<PreviewMode, string> Titles = new Dictionary<PreviewMode, string>
        {
            { PreviewMode.All, "全部相机" },
            { PreviewMode.Front, "前相机" },
            { PreviewMode.Rear, "后相机" },
            { PreviewMode.Left, "左相机" },
            { PreviewMode.Right, "右相机" },
        };

        readonly Dictionary<string, RenderTexture> m_RenderTextures = new Dictionary<string, RenderTexture>();
        PreviewMode m_Mode = PreviewMode.Front;

        public static bool IsAllCameraWindowOpen()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<VlnTopgearCameraPreviewWindow>())
            {
                if (window != null && window.m_Mode == PreviewMode.All)
                {
                    return true;
                }
            }

            return false;
        }

        public static void OpenAllCameras()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<VlnTopgearCameraPreviewWindow>())
            {
                if (window != null && window.m_Mode != PreviewMode.All)
                {
                    window.Close();
                }
            }

            Open(PreviewMode.All);
        }

        public static void OpenFrontCamera() => OpenSingle(PreviewMode.Front);
        public static void OpenRearCamera() => OpenSingle(PreviewMode.Rear);
        public static void OpenLeftCamera() => OpenSingle(PreviewMode.Left);
        public static void OpenRightCamera() => OpenSingle(PreviewMode.Right);

        static void OpenSingle(PreviewMode mode)
        {
            if (IsAllCameraWindowOpen())
            {
                return;
            }

            Open(mode);
        }

        static void Open(PreviewMode mode)
        {
            var window = CreateInstance<VlnTopgearCameraPreviewWindow>();
            window.m_Mode = mode;
            window.titleContent = new GUIContent(Titles[mode]);
            window.position = InitialWindowRect(mode);
            window.ShowUtility();
        }

        static Rect InitialWindowRect(PreviewMode mode)
        {
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            float screenWidth = Mathf.Max(960f, mainWindow.width);
            float screenHeight = Mathf.Max(640f, mainWindow.height);
            float singleWidth = screenWidth / 6f;
            float singleHeight = screenHeight / 4f;

            if (mode == PreviewMode.All)
            {
                return new Rect(mainWindow.x + 80f, mainWindow.y + 80f, screenWidth * 0.72f, screenHeight * 0.36f);
            }

            int offset = mode == PreviewMode.Front ? 0 : mode == PreviewMode.Rear ? 32 : mode == PreviewMode.Left ? 64 : 96;
            return new Rect(mainWindow.x + 120f + offset, mainWindow.y + 120f + offset, singleWidth, singleHeight);
        }

        void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.update -= Repaint;
            ReleaseRenderTextures();
        }

        void OnGUI()
        {
            Rect imageRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (m_Mode == PreviewMode.All)
            {
                DrawAllCameras(imageRect);
                return;
            }

            DrawCamera(imageRect, CameraNameForMode(m_Mode));
        }

        void DrawAllCameras(Rect rect)
        {
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            DrawCameraTile(new Rect(rect.x, rect.y, halfWidth, halfHeight), FrontCameraName, "前相机");
            DrawCameraTile(new Rect(rect.x + halfWidth, rect.y, halfWidth, halfHeight), RearCameraName, "后相机");
            DrawCameraTile(new Rect(rect.x, rect.y + halfHeight, halfWidth, halfHeight), LeftCameraName, "左相机");
            DrawCameraTile(new Rect(rect.x + halfWidth, rect.y + halfHeight, halfWidth, halfHeight), RightCameraName, "右相机");
        }

        void DrawCameraTile(Rect rect, string cameraName, string label)
        {
            const float labelHeight = 18f;
            var labelRect = new Rect(rect.x, rect.y, rect.width, labelHeight);
            var cameraRect = new Rect(rect.x, rect.y + labelHeight, rect.width, Mathf.Max(1f, rect.height - labelHeight));

            EditorGUI.DrawRect(labelRect, new Color(0.12f, 0.12f, 0.12f, 0.92f));
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold,
            };
            GUI.Label(labelRect, label, style);
            DrawCamera(cameraRect, cameraName);
        }

        void DrawCamera(Rect rect, string cameraName)
        {
            GameObject cameraObject = GameObject.Find(cameraName);
            if (cameraObject == null)
            {
                DrawStatus(rect, "未找到相机", Color.red);
                return;
            }

            var fisheyeSensor = cameraObject.GetComponent<FisheyeCameraSensor>();
            if (fisheyeSensor != null)
            {
                Texture fisheyeTexture = fisheyeSensor.texture0;
                if (fisheyeTexture == null)
                {
                    DrawStatus(rect, "等待鱼眼图像", Color.white);
                    return;
                }

                GUI.DrawTexture(rect, fisheyeTexture, ScaleMode.ScaleToFit, false);
                return;
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                DrawStatus(rect, "未找到相机", Color.red);
                return;
            }

            int width = Mathf.Max(64, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(48, Mathf.RoundToInt(rect.height));
            RenderTexture texture = GetRenderTexture(cameraName, width, height);

            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }

            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
        }

        static void DrawStatus(Rect rect, string text, Color textColor)
        {
            EditorGUI.DrawRect(rect, Color.black);
            GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                normal = { textColor = textColor },
                fontSize = 12,
            };
            GUI.Label(rect, text, style);
        }

        RenderTexture GetRenderTexture(string key, int width, int height)
        {
            if (m_RenderTextures.TryGetValue(key, out var texture) && texture != null && texture.width == width && texture.height == height)
            {
                return texture;
            }

            if (texture != null)
            {
                texture.Release();
                DestroyImmediate(texture);
            }

            texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = "VLN_" + key + "_PreviewTexture",
            };
            texture.Create();
            m_RenderTextures[key] = texture;
            return texture;
        }

        static string CameraNameForMode(PreviewMode mode)
        {
            switch (mode)
            {
                case PreviewMode.Front:
                    return FrontCameraName;
                case PreviewMode.Rear:
                    return RearCameraName;
                case PreviewMode.Left:
                    return LeftCameraName;
                case PreviewMode.Right:
                    return RightCameraName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        void ReleaseRenderTextures()
        {
            foreach (var texture in m_RenderTextures.Values)
            {
                if (texture == null)
                {
                    continue;
                }

                texture.Release();
                DestroyImmediate(texture);
            }

            m_RenderTextures.Clear();
        }
    }
}
