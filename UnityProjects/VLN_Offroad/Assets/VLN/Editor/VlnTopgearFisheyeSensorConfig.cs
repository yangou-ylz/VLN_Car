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
using UnitySensors.DataType.LiDAR;
using UnitySensors.Sensor.Camera;
using UnitySensors.Sensor.LiDAR;
using VLN.ROS2;
using CameraInfoMsgPublisher = UnitySensors.ROS.Publisher.Camera.CameraInfoMsgPublisher;
using ImageMsgPublisher = UnitySensors.ROS.Publisher.Sensor.ImageMsgPublisher;
using LiDARPointCloud2MsgPublisher = UnitySensors.ROS.Publisher.Sensor.LiDARPointCloud2MsgPublisher;

namespace VLN.Editor
{
    public static class VlnTopgearFisheyeSensorConfig
    {
        public const float TopgearCameraFovDeg = 90f;
        public const float TopgearFisheyeViewAngleDeg = 190f;
        public const float TopgearCameraFrequencyHz = 20f;
        public const float TopgearLidarFrequencyHz = 18f;
        public const float TopgearLidarMaxRangeMeters = 90f;
        public const int TopgearLidarPointsPerScan = 57600;
        public const int TopgearFisheyeCubemapResolution = 256;
        public static readonly Vector2Int TopgearCameraResolution = new(640, 640);

        const string SensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        const string LensDistortionVolumeName = "VLN_Topgear_LensDistortion_PostProcessVolume";
        const string PackageFisheyeMaterialGuid = "54d1a4a7d74dd4945ad614037e38fbb0";
        const string PackageFisheyeMaterialPath = "Packages/com.frj.unity-sensors/Samples~/Runtime/Materials/CustomMaterials/UnitySensors_FisheyeCamera.mat";
        const string ProjectFisheyeMaterialPath = "Assets/VLN/Materials/VLN_UnitySensors_FisheyeCamera.mat";
        const string PerCameraFisheyeMaterialDirectory = "Assets/VLN/Materials/TopgearFisheye";
        const string PreviewDirectoryRelativePath = "UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/topgear_fisheye_previews";
        const string ResultRelativePath = "UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/vln_topgear_fisheye_sensor_config_result.txt";

        [MenuItem("VLN/Topgear 传感器/应用鱼眼视角与高频发布到当前场景", priority = 301)]
        public static void ApplyCurrentSceneFromMenu()
        {
            var result = ApplyCurrentSceneAndCapturePreviews(saveScene: true);
            EditorUtility.DisplayDialog(
                result.Success ? "Topgear 传感器配置完成" : "Topgear 传感器配置失败",
                result.Summary,
                "确定");
        }

        [MenuItem("VLN/Topgear 传感器/应用到 Mesa Topgear 场景并截图", priority = 302)]
        public static void ApplyMesaTopgearSceneFromMenu()
        {
            var result = ApplyMesaTopgearSceneAndCapturePreviews();
            EditorUtility.DisplayDialog(
                result.Success ? "Mesa Topgear 传感器配置完成" : "Mesa Topgear 传感器配置失败",
                result.Summary,
                "确定");
        }

        public static void ApplyMesaTopgearSceneBatch()
        {
            var result = ApplyMesaTopgearSceneAndCapturePreviews();
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Summary);
            }
        }

        public static ApplyResult ApplyMesaTopgearSceneAndCapturePreviews()
        {
            string scenePath = VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath;
            string absoluteScenePath = ProjectRelativeToAbsolute(scenePath);
            if (!File.Exists(absoluteScenePath))
            {
                throw new FileNotFoundException("Mesa Topgear scene is missing", absoluteScenePath);
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            VlnTopgearCameraDataPoseTuner.EnsureDecoupledIfSavedStateRequiresIt(saveScene: false, showDialog: false);
            VlnTopgearUpperAssemblyTuner.ApplySavedAssemblyIfPresent(saveScene: false, showDialog: false);
            VlnTopgearCameraDataPoseTuner.ApplySavedCameraDataPosesIfPresent(saveScene: false, showDialog: false);
            return ApplyCurrentSceneAndCapturePreviews(saveScene: true);
        }

        public static ApplyResult ApplyCurrentSceneAndCapturePreviews(bool saveScene)
        {
            return ApplyCurrentScene(saveScene, capturePreviews: true);
        }

        public static ApplyResult ApplyCurrentSceneSensorConfig(bool saveScene)
        {
            return ApplyCurrentScene(saveScene, capturePreviews: false);
        }

        static ApplyResult ApplyCurrentScene(bool saveScene, bool capturePreviews)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var result = new ApplyResult { ScenePath = scene.path };
            bool writeResultFile = saveScene || capturePreviews;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                {
                    return result.Fail("当前没有打开已保存的 Unity 场景。");
                }

                var sensorRoot = GameObject.Find(SensorRootName);
                if (sensorRoot == null)
                {
                    return result.Fail("当前场景缺少 " + SensorRootName + "，没有找到 Topgear 传感器套件。");
                }

                var cameras = sensorRoot.GetComponentsInChildren<UnityEngine.Camera>(true)
                    .Where(camera => camera.name.Contains("Topgear_", StringComparison.Ordinal) && camera.name.Contains("RGBCamera", StringComparison.Ordinal))
                    .OrderBy(camera => CameraSortKey(camera.name), StringComparer.Ordinal)
                    .ToArray();
                var lidarSensors = sensorRoot.GetComponentsInChildren<RaycastLiDARSensor>(true)
                    .Where(sensor => sensor.name.Contains("LiDAR", StringComparison.Ordinal) || sensor.transform.name.Contains("LiDAR", StringComparison.Ordinal))
                    .ToArray();

                Material fisheyeMaterial = EnsureFisheyeMaterial(result);
                DisableLegacyLensDistortionVolume(result);

                foreach (var camera in cameras)
                {
                    ConfigureCameraObject(camera, result, fisheyeMaterial);
                }
                foreach (var lidar in lidarSensors)
                {
                    ConfigureLidarObject(lidar, result);
                }

                result.CameraCount = cameras.Length;
                result.LidarCount = lidarSensors.Length;

                if (saveScene)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    var saveResult = VlnWorldModelManualSaveWindow.SaveCurrentWorld(showDialog: false);
                    result.ManualSaveSuccess = saveResult.Success;
                    result.ManualSaveMessage = saveResult.Message;
                    if (!saveResult.Success)
                    {
                        return result.Fail("传感器参数已设置，但世界保存校验失败：" + saveResult.Message);
                    }
                }

                if (capturePreviews)
                {
                    CaptureCameraPreviews(cameras, result);
                }
                if (writeResultFile)
                {
                    WriteResult(result);
                }

                bool ok = result.CameraCount == 4 && result.LidarCount >= 1 &&
                          result.CameraFieldOfViewSetCount == 4 && result.FisheyeSensorConfiguredCount == 4 &&
                          result.RgbSensorDisabledCount == 4 && result.UnitySensorsImagePublisherConfiguredCount == 4 &&
                          result.LegacyVlnFisheyeImagePublisherRemovedCount >= 0 && result.ImagePublisherFrequencySetCount == 4 &&
                          result.ImagePublisherFisheyeSourceCount == 4 &&
                          result.CameraInfoPublisherFrequencySetCount == 4 &&
                          result.FisheyeCameraInfoPublisherConfiguredCount == 4 && result.LegacyCameraInfoPublisherDisabledCount == 4 &&
                          result.PostProcessLayerDisabledCount == 4 && result.FisheyeMaterialConfigured &&
                          result.LidarSensorFrequencySetCount >= 1 && result.LidarMaxRangeSetCount >= 1 &&
                          result.LidarPointsPerScanSetCount >= 1 &&
                          result.LidarPublisherFrequencySetCount >= 1 &&
                          (!capturePreviews || result.PreviewFiles.Count >= 4);
                if (!ok)
                {
                    return result.Fail("传感器配置数量不完整，请检查结果文件：" + ProjectRootPath(ResultRelativePath));
                }

                result.Success = true;
                result.Summary = "Topgear 四路相机已切换为 UnitySensors FisheyeCameraSensor，等距鱼眼 " +
                                 TopgearFisheyeViewAngleDeg.ToString("F0", CultureInfo.InvariantCulture) + "°、" +
                                 TopgearCameraResolution.x.ToString(CultureInfo.InvariantCulture) + "x" + TopgearCameraResolution.y.ToString(CultureInfo.InvariantCulture) + "、" +
                                 TopgearCameraFrequencyHz.ToString("F0", CultureInfo.InvariantCulture) +
                                 "Hz；图像由 UnitySensors 官方 ImageMsgPublisher 直接发布 FisheyeCameraSensor.texture0；CameraInfo 发布 equidistant 标定；已停用旧 Lens Distortion 后处理；LiDAR 已设为 " +
                                 TopgearLidarFrequencyHz.ToString("F0", CultureInfo.InvariantCulture) +
                                 "Hz、最大距离 " + TopgearLidarMaxRangeMeters.ToString("F0", CultureInfo.InvariantCulture) +
                                 "m。" + (capturePreviews ? "预览图目录：" + ProjectRootPath(PreviewDirectoryRelativePath) : "未导出预览图。");
                if (writeResultFile)
                {
                    WriteResult(result);
                }
                Debug.Log("VLN_TOPGEAR_FISHEYE_SENSOR_CONFIG_OK " + result.Summary);
                return result;
            }
            catch (Exception ex)
            {
                result.Fail(ex.ToString());
                if (writeResultFile)
                {
                    WriteResult(result);
                }
                Debug.LogError("VLN_TOPGEAR_FISHEYE_SENSOR_CONFIG_FAILED " + ex);
                return result;
            }
        }

        static void ConfigureCameraObject(UnityEngine.Camera camera, ApplyResult result, Material fisheyeMaterial)
        {
            camera.usePhysicalProperties = false;
            camera.fieldOfView = TopgearCameraFovDeg;
            camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, 0.035f);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 120f);
            camera.targetTexture = null;
            EditorUtility.SetDirty(camera);
            result.CameraFieldOfViewSetCount++;
            result.CameraNames.Add(camera.name);

            DisablePostProcessLayer(camera, result);

            var fisheyeSensor = camera.GetComponent<FisheyeCameraSensor>();
            if (fisheyeSensor == null)
            {
                fisheyeSensor = camera.gameObject.AddComponent<FisheyeCameraSensor>();
            }
            Material cameraFisheyeMaterial = EnsurePerCameraFisheyeMaterial(fisheyeMaterial, camera.name, result);
            ConfigureFisheyeSensor(fisheyeSensor, cameraFisheyeMaterial, result, camera.name);

            var rgbSensor = camera.GetComponent<RGBCameraSensor>();
            if (rgbSensor != null)
            {
                UnityEngine.Object.DestroyImmediate(rgbSensor, allowDestroyingAssets: true);
                result.RgbSensorDisabledCount++;
            }
            else
            {
                result.RgbSensorDisabledCount++;
            }

            string imageTopic = ImageTopicFromName(camera.name);
            var imagePublishers = camera.GetComponents<ImageMsgPublisher>();
            if (imagePublishers.Length == 0)
            {
                imagePublishers = new[] { camera.gameObject.AddComponent<ImageMsgPublisher>() };
            }
            for (int i = 0; i < imagePublishers.Length; i++)
            {
                var publisher = imagePublishers[i];
                if (i == 0)
                {
                    ConfigureUnitySensorsImagePublisher(publisher, fisheyeSensor, imageTopic, CameraFrameIdFromName(camera.name), result, camera.name);
                }
                else
                {
                    publisher.enabled = false;
                    EditorUtility.SetDirty(publisher);
                    result.ExtraImagePublisherDisabledCount++;
                }
            }

            foreach (var legacyPublisher in camera.GetComponents<VlnFisheyeImagePublisher>())
            {
                UnityEngine.Object.DestroyImmediate(legacyPublisher, allowDestroyingAssets: true);
                result.LegacyVlnFisheyeImagePublisherRemovedCount++;
            }

            string cameraInfoTopic = CameraInfoTopicFromName(camera.name);
            string frameId = CameraFrameIdFromName(camera.name);
            foreach (var publisher in camera.GetComponents<CameraInfoMsgPublisher>())
            {
                var serializedPublisher = new SerializedObject(publisher);
                var topicProperty = serializedPublisher.FindProperty("_topicName");
                if (topicProperty != null && !string.IsNullOrEmpty(topicProperty.stringValue))
                {
                    cameraInfoTopic = topicProperty.stringValue;
                }
                var serializer = serializedPublisher.FindProperty("_serializer");
                var header = serializer != null ? serializer.FindPropertyRelative("_header") : null;
                var frameProperty = header != null ? header.FindPropertyRelative("_frame_id") : null;
                if (frameProperty != null && !string.IsNullOrEmpty(frameProperty.stringValue))
                {
                    frameId = frameProperty.stringValue;
                }
                bool cameraInfoFrequencySet = SetFloat(serializedPublisher, "_frequency", TopgearCameraFrequencyHz, result, "camera_info_frequency", camera.name);
                serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
                publisher.enabled = false;
                EditorUtility.SetDirty(publisher);
                if (cameraInfoFrequencySet)
                {
                    result.CameraInfoPublisherFrequencySetCount++;
                }
                result.LegacyCameraInfoPublisherDisabledCount++;
            }

            var fisheyeInfoPublisher = camera.GetComponent<VlnFisheyeCameraInfoPublisher>();
            if (fisheyeInfoPublisher == null)
            {
                fisheyeInfoPublisher = camera.gameObject.AddComponent<VlnFisheyeCameraInfoPublisher>();
            }
            ConfigureFisheyeCameraInfoPublisher(fisheyeInfoPublisher, cameraInfoTopic, frameId, result, camera.name);
        }

        static void ConfigureFisheyeSensor(FisheyeCameraSensor sensor, Material fisheyeMaterial, ApplyResult result, string objectName)
        {
            sensor.enabled = true;
            var serializedSensor = new SerializedObject(sensor);
            bool frequencySet = SetFloat(serializedSensor, "_frequency", TopgearCameraFrequencyHz, result, "fisheye_frequency", objectName);
            SetFloat(serializedSensor, "_fov", TopgearCameraFovDeg, result, "fisheye_camera_fov", objectName);
            SetVector2Int(serializedSensor, "_resolution", TopgearCameraResolution, result, "fisheye_resolution", objectName);
            SetObject(serializedSensor, "_fisheyeMat", fisheyeMaterial, result, "fisheye_material", objectName);
            SetInt(serializedSensor, "_cubemapResolution", TopgearFisheyeCubemapResolution, result, "fisheye_cubemap_resolution", objectName);
            SetFloat(serializedSensor, "_viewAngle", TopgearFisheyeViewAngleDeg, result, "fisheye_view_angle", objectName);
            SetEnum(serializedSensor, "_cameraModel", (int)FisheyeCameraSensor.CameraModel.Equidistant, result, "fisheye_camera_model", objectName);
            SetVector4(serializedSensor, "_kb4", Vector4.zero, result, "fisheye_kb4", objectName);
            SetVector2(serializedSensor, "_focalLength", FisheyeFocalLengthPixels(), result, "fisheye_focal_length", objectName);
            SetVector2(serializedSensor, "_principalPoint", new Vector2(TopgearCameraResolution.x * 0.5f, TopgearCameraResolution.y * 0.5f), result, "fisheye_principal_point", objectName);
            serializedSensor.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sensor);
            if (frequencySet && fisheyeMaterial != null)
            {
                result.FisheyeSensorConfiguredCount++;
            }
        }

        static void ConfigureUnitySensorsImagePublisher(ImageMsgPublisher publisher, FisheyeCameraSensor fisheyeSensor, string topicName, string frameId, ApplyResult result, string objectName)
        {
            publisher.enabled = true;
            var serializedPublisher = new SerializedObject(publisher);
            SetString(serializedPublisher, "_topicName", topicName, result, "unity_sensors_image_topic", objectName);
            bool imageFrequencySet = SetFloat(serializedPublisher, "_frequency", TopgearCameraFrequencyHz, result, "unity_sensors_image_frequency", objectName);
            var serializer = serializedPublisher.FindProperty("_serializer");
            if (serializer != null)
            {
                var source = serializer.FindPropertyRelative("_source");
                if (source != null)
                {
                    source.objectReferenceValue = fisheyeSensor;
                    result.ImagePublisherFisheyeSourceCount++;
                }
                var sourceTexture = serializer.FindPropertyRelative("_sourceTexture");
                if (sourceTexture != null)
                {
                    sourceTexture.enumValueIndex = 0;
                }
                var encoding = serializer.FindPropertyRelative("_encoding");
                if (encoding != null)
                {
                    encoding.enumValueIndex = 0;
                }
                var header = serializer.FindPropertyRelative("_header");
                if (header != null)
                {
                    var headerSource = header.FindPropertyRelative("_source");
                    if (headerSource != null)
                    {
                        headerSource.objectReferenceValue = fisheyeSensor;
                    }
                    var frameProperty = header.FindPropertyRelative("_frame_id");
                    if (frameProperty != null)
                    {
                        frameProperty.stringValue = frameId;
                    }
                }
            }
            else
            {
                result.Warnings.Add(objectName + " missing ImageMsgSerializer on UnitySensors ImageMsgPublisher.");
            }
            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(publisher);
            if (imageFrequencySet)
            {
                result.ImagePublisherFrequencySetCount++;
            }
            result.UnitySensorsImagePublisherConfiguredCount++;
        }

        static void ConfigureFisheyeCameraInfoPublisher(VlnFisheyeCameraInfoPublisher publisher, string topicName, string frameId, ApplyResult result, string objectName)
        {
            publisher.enabled = true;
            var serializedPublisher = new SerializedObject(publisher);
            SetString(serializedPublisher, "m_TopicName", topicName, result, "fisheye_camera_info_topic", objectName);
            SetString(serializedPublisher, "m_FrameId", frameId, result, "fisheye_camera_info_frame_id", objectName);
            SetFloat(serializedPublisher, "m_PublishFrequencyHz", TopgearCameraFrequencyHz, result, "fisheye_camera_info_frequency", objectName);
            SetInt(serializedPublisher, "m_Width", TopgearCameraResolution.x, result, "fisheye_camera_info_width", objectName);
            SetInt(serializedPublisher, "m_Height", TopgearCameraResolution.y, result, "fisheye_camera_info_height", objectName);
            SetFloat(serializedPublisher, "m_ViewAngleDeg", TopgearFisheyeViewAngleDeg, result, "fisheye_camera_info_view_angle", objectName);
            SetString(serializedPublisher, "m_DistortionModel", "equidistant", result, "fisheye_camera_info_distortion_model", objectName);
            serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(publisher);
            result.FisheyeCameraInfoPublisherConfiguredCount++;
        }

        static void DisablePostProcessLayer(UnityEngine.Camera camera, ApplyResult result)
        {
            var postProcessLayer = camera.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().FullName == "UnityEngine.Rendering.PostProcessing.PostProcessLayer");
            if (postProcessLayer != null)
            {
                if (postProcessLayer is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
                EditorUtility.SetDirty(postProcessLayer);
            }
            result.PostProcessLayerDisabledCount++;
        }

        static Material EnsureFisheyeMaterial(ApplyResult result)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PackageFisheyeMaterialPath);
            if (material == null)
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(PackageFisheyeMaterialGuid);
                if (!string.IsNullOrEmpty(guidPath))
                {
                    material = AssetDatabase.LoadAssetAtPath<Material>(guidPath);
                }
            }

            if (material != null)
            {
                result.FisheyeMaterialPath = AssetDatabase.GetAssetPath(material);
                result.FisheyeMaterialConfigured = true;
                return material;
            }

            material = AssetDatabase.LoadAssetAtPath<Material>(ProjectFisheyeMaterialPath);
            if (material != null && material.shader != null && material.shader.name == "UnitySensors/FisheyeCamera")
            {
                result.FisheyeMaterialPath = ProjectFisheyeMaterialPath;
                result.FisheyeMaterialConfigured = true;
                result.Warnings.Add("Unity AssetDatabase cannot load Samples~ fisheye material directly; using project material backed by the UnitySensors/FisheyeCamera shader copy.");
                return material;
            }

            throw new FileNotFoundException("未找到 UnitySensors FisheyeCamera 材质或项目内官方 shader 副本材质，不能改用自建鱼眼外观或自写发布器。", PackageFisheyeMaterialPath + " | " + ProjectFisheyeMaterialPath);
        }

        static Material EnsurePerCameraFisheyeMaterial(Material baseMaterial, string cameraName, ApplyResult result)
        {
            if (baseMaterial == null)
            {
                throw new ArgumentNullException(nameof(baseMaterial));
            }

            string viewName = CameraViewName(cameraName);
            string assetPath = PerCameraFisheyeMaterialDirectory + "/VLN_UnitySensors_FisheyeCamera_" + viewName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Directory.CreateDirectory(ProjectRelativeToAbsolute(PerCameraFisheyeMaterialDirectory));
                material = new Material(baseMaterial)
                {
                    name = "VLN_UnitySensors_FisheyeCamera_" + viewName
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != baseMaterial.shader)
            {
                material.shader = baseMaterial.shader;
            }

            material.SetFloat("_Angle", TopgearFisheyeViewAngleDeg);
            material.SetFloat("_CameraModel", (int)FisheyeCameraSensor.CameraModel.Equidistant);
            material.SetVector("_kb4", Vector4.zero);
            material.SetFloat("_fx", FisheyeFocalLengthPixels().x / TopgearCameraResolution.x);
            material.SetFloat("_fy", FisheyeFocalLengthPixels().y / TopgearCameraResolution.y);
            material.SetFloat("_cx", 0.5f);
            material.SetFloat("_cy", 0.5f);
            material.SetFloat("_resolutionX", TopgearCameraResolution.x);
            material.SetFloat("_resolutionY", TopgearCameraResolution.y);
            EditorUtility.SetDirty(material);
            if (!result.PerCameraFisheyeMaterialPaths.Contains(assetPath))
            {
                result.PerCameraFisheyeMaterialPaths.Add(assetPath);
            }
            return material;
        }

        static void DisableLegacyLensDistortionVolume(ApplyResult result)
        {
            var volumeObject = GameObject.Find(LensDistortionVolumeName);
            if (volumeObject == null)
            {
                result.LensDistortionVolumeConfigured = false;
                return;
            }

            var volume = volumeObject.GetComponents<Component>()
                .FirstOrDefault(component => component != null && component.GetType().FullName == "UnityEngine.Rendering.PostProcessing.PostProcessVolume");
            if (volume != null)
            {
                var weightProperty = volume.GetType().GetProperty("weight");
                if (weightProperty != null && weightProperty.CanWrite)
                {
                    weightProperty.SetValue(volume, 0f);
                }
                if (volume is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
                EditorUtility.SetDirty(volume);
            }
            result.LensDistortionVolumeConfigured = false;
            EditorUtility.SetDirty(volumeObject);
        }

        static Vector2 FisheyeFocalLengthPixels()
        {
            float halfAngleRad = Mathf.Deg2Rad * TopgearFisheyeViewAngleDeg * 0.5f;
            float radiusPixels = Mathf.Min(TopgearCameraResolution.x, TopgearCameraResolution.y) * 0.5f;
            float focal = radiusPixels / Mathf.Max(halfAngleRad, 1e-6f);
            return new Vector2(focal, focal);
        }

        static string CameraInfoTopicFromName(string cameraName)
        {
            if (cameraName.Contains("Front", StringComparison.Ordinal)) return "/vln/front/camera_info";
            if (cameraName.Contains("Rear", StringComparison.Ordinal)) return "/vln/rear/camera_info";
            if (cameraName.Contains("Left", StringComparison.Ordinal)) return "/vln/left/camera_info";
            if (cameraName.Contains("Right", StringComparison.Ordinal)) return "/vln/right/camera_info";
            return "/vln/front/camera_info";
        }

        static string ImageTopicFromName(string cameraName)
        {
            if (cameraName.Contains("Front", StringComparison.Ordinal)) return "/vln/front/image_raw";
            if (cameraName.Contains("Rear", StringComparison.Ordinal)) return "/vln/rear/image_raw";
            if (cameraName.Contains("Left", StringComparison.Ordinal)) return "/vln/left/image_raw";
            if (cameraName.Contains("Right", StringComparison.Ordinal)) return "/vln/right/image_raw";
            return "/vln/front/image_raw";
        }

        static string CameraFrameIdFromName(string cameraName)
        {
            if (cameraName.Contains("Front", StringComparison.Ordinal)) return "front_camera_optical_frame";
            if (cameraName.Contains("Rear", StringComparison.Ordinal)) return "rear_camera_optical_frame";
            if (cameraName.Contains("Left", StringComparison.Ordinal)) return "left_camera_optical_frame";
            if (cameraName.Contains("Right", StringComparison.Ordinal)) return "right_camera_optical_frame";
            return "front_camera_optical_frame";
        }

        static void ConfigureLidarObject(RaycastLiDARSensor lidarSensor, ApplyResult result)
        {
            var serializedSensor = new SerializedObject(lidarSensor);
            var scanPatternProperty = serializedSensor.FindProperty("_scanPattern");
            var scanPattern = scanPatternProperty != null ? scanPatternProperty.objectReferenceValue as ScanPattern : null;
            int targetPointsPerScan = scanPattern != null && scanPattern.size > 0
                ? Mathf.Min(TopgearLidarPointsPerScan, scanPattern.size)
                : TopgearLidarPointsPerScan;
            bool lidarFrequencySet = SetFloat(serializedSensor, "_frequency", TopgearLidarFrequencyHz, result, "lidar_frequency", lidarSensor.name);
            bool lidarMaxRangeSet = SetFloat(serializedSensor, "_maxRange", TopgearLidarMaxRangeMeters, result, "lidar_max_range", lidarSensor.name);
            bool lidarPointsPerScanSet = SetInt(serializedSensor, "_pointsNumPerScan", targetPointsPerScan, result, "lidar_points_per_scan", lidarSensor.name);
            serializedSensor.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lidarSensor);
            if (lidarFrequencySet)
            {
                result.LidarSensorFrequencySetCount++;
            }
            if (lidarMaxRangeSet)
            {
                result.LidarMaxRangeSetCount++;
            }
            if (lidarPointsPerScanSet)
            {
                result.LidarPointsPerScanSetCount++;
                result.LidarPointsPerScan = targetPointsPerScan;
                result.LidarScanPatternSize = scanPattern != null ? scanPattern.size : 0;
            }
            result.LidarNames.Add(lidarSensor.name);

            foreach (var publisher in lidarSensor.GetComponents<LiDARPointCloud2MsgPublisher>())
            {
                var serializedPublisher = new SerializedObject(publisher);
                bool lidarPublisherFrequencySet = SetFloat(serializedPublisher, "_frequency", TopgearLidarFrequencyHz, result, "lidar_pointcloud_frequency", lidarSensor.name);
                serializedPublisher.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(publisher);
                if (lidarPublisherFrequencySet)
                {
                    result.LidarPublisherFrequencySetCount++;
                }
            }
        }

        static void CaptureCameraPreviews(UnityEngine.Camera[] cameras, ApplyResult result)
        {
            string outputDirectory = ProjectRootPath(PreviewDirectoryRelativePath);
            Directory.CreateDirectory(outputDirectory);
            foreach (var camera in cameras)
            {
                string viewName = CameraViewName(camera.name);
                string outputPath = Path.Combine(outputDirectory, "topgear_fisheye_" + viewName + ".png");
                CaptureFisheyeCamera(camera, outputPath, TopgearCameraResolution.x, TopgearCameraResolution.y, result);
                result.PreviewFiles.Add(outputPath);
            }
        }

        static void CaptureFisheyeCamera(UnityEngine.Camera camera, string outputPath, int width, int height, ApplyResult result)
        {
            var previousActive = RenderTexture.active;
            var fisheyeSensor = camera.GetComponent<FisheyeCameraSensor>();
            Material sensorMaterial = GetFisheyeMaterial(fisheyeSensor);
            if (sensorMaterial == null)
            {
                result.Warnings.Add(camera.name + " missing fisheye material; preview falls back to raw camera render.");
                CaptureRawCamera(camera, outputPath, width, height);
                return;
            }

            int cubemapResolution = GetSerializedInt(fisheyeSensor, "_cubemapResolution", TopgearFisheyeCubemapResolution);
            var cubemap = new RenderTexture(cubemapResolution, cubemapResolution, 0, RenderTextureFormat.ARGB32)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Cube
            };
            var renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var material = new Material(sensorMaterial);
            try
            {
                camera.RenderToCubemap(cubemap);
                ConfigureFisheyePreviewMaterial(material, camera.transform);
                Graphics.Blit(cubemap, renderTexture, material);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false);
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(material);
                cubemap.Release();
                UnityEngine.Object.DestroyImmediate(cubemap);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        static void CaptureRawCamera(UnityEngine.Camera camera, string outputPath, int width, int height)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false);
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        static void ConfigureFisheyePreviewMaterial(Material material, Transform cameraTransform)
        {
            material.SetFloat("_CameraModel", (int)FisheyeCameraSensor.CameraModel.Equidistant);
            material.SetFloat("_Angle", TopgearFisheyeViewAngleDeg);
            material.SetVector("_kb4", Vector4.zero);
            material.SetFloat("_fx", FisheyeFocalLengthPixels().x / TopgearCameraResolution.x);
            material.SetFloat("_fy", FisheyeFocalLengthPixels().y / TopgearCameraResolution.y);
            material.SetFloat("_cx", 0.5f);
            material.SetFloat("_cy", 0.5f);
            material.SetFloat("_resolutionX", TopgearCameraResolution.x);
            material.SetFloat("_resolutionY", TopgearCameraResolution.y);
            material.SetMatrix("_WorldTransform", Matrix4x4.TRS(Vector3.zero, cameraTransform.rotation, Vector3.one));
        }

        static Material GetFisheyeMaterial(FisheyeCameraSensor sensor)
        {
            if (sensor == null)
            {
                return null;
            }

            var serializedSensor = new SerializedObject(sensor);
            var property = serializedSensor.FindProperty("_fisheyeMat");
            return property != null ? property.objectReferenceValue as Material : null;
        }

        static int GetSerializedInt(UnityEngine.Object target, string propertyName, int fallback)
        {
            if (target == null)
            {
                return fallback;
            }
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            return property != null ? property.intValue : fallback;
        }

        static bool SetFloat(SerializedObject serializedObject, string propertyName, float value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized float " + label + "(" + propertyName + ")");
                return false;
            }
            property.floatValue = value;
            return true;
        }

        static bool SetInt(SerializedObject serializedObject, string propertyName, int value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized int " + label + "(" + propertyName + ")");
                return false;
            }
            property.intValue = value;
            return true;
        }

        static void SetEnum(SerializedObject serializedObject, string propertyName, int value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized enum " + label + "(" + propertyName + ")");
                return;
            }
            property.enumValueIndex = value;
        }

        static void SetString(SerializedObject serializedObject, string propertyName, string value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized string " + label + "(" + propertyName + ")");
                return;
            }
            property.stringValue = value;
        }

        static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized object " + label + "(" + propertyName + ")");
                return;
            }
            property.objectReferenceValue = value;
        }

        static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized Vector2 " + label + "(" + propertyName + ")");
                return;
            }
            property.vector2Value = value;
        }

        static void SetVector4(SerializedObject serializedObject, string propertyName, Vector4 value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized Vector4 " + label + "(" + propertyName + ")");
                return;
            }
            property.vector4Value = value;
        }

        static void SetVector2Int(SerializedObject serializedObject, string propertyName, Vector2Int value, ApplyResult result, string label, string objectName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                result.Warnings.Add(objectName + " missing serialized Vector2Int " + label + "(" + propertyName + ")");
                return;
            }
            property.vector2IntValue = value;
        }

        static void WriteResult(ApplyResult result)
        {
            string path = ProjectRootPath(ResultRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            var sb = new StringBuilder();
            sb.AppendLine("success=" + (result.Success ? "1" : "0"));
            sb.AppendLine("scene_path=" + result.ScenePath);
            sb.AppendLine("camera_sensor_type=UnitySensors.Sensor.Camera.FisheyeCameraSensor");
            sb.AppendLine("camera_image_publisher_type=UnitySensors.ROS.Publisher.Sensor.ImageMsgPublisher");
            sb.AppendLine("camera_projection_source=UnitySensors official FisheyeCameraSensor texture0");
            sb.AppendLine("camera_projection_model=equidistant");
            sb.AppendLine("camera_projection_formula=r=f*theta");
            sb.AppendLine("camera_target_fov_deg=" + TopgearCameraFovDeg.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("camera_fisheye_view_angle_deg=" + TopgearFisheyeViewAngleDeg.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("camera_fisheye_cubemap_resolution=" + TopgearFisheyeCubemapResolution.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("camera_target_frequency_hz=" + TopgearCameraFrequencyHz.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_target_frequency_hz=" + TopgearLidarFrequencyHz.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_target_max_range_m=" + TopgearLidarMaxRangeMeters.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_target_points_per_scan=" + TopgearLidarPointsPerScan.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_applied_points_per_scan=" + result.LidarPointsPerScan.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_scan_pattern_size=" + result.LidarScanPatternSize.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lens_distortion_enabled=0");
            sb.AppendLine("lens_distortion_note=legacy_postprocess_disabled_real_fisheye_sensor_used");
            sb.AppendLine("lens_distortion_layer_index=" + result.LensDistortionLayerIndex.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("post_process_layer_disabled_count=" + result.PostProcessLayerDisabledCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lens_distortion_volume_enabled=" + (result.LensDistortionVolumeConfigured ? "1" : "0"));
            sb.AppendLine("fisheye_material_configured=" + (result.FisheyeMaterialConfigured ? "1" : "0"));
            sb.AppendLine("fisheye_material_path=" + result.FisheyeMaterialPath);
            sb.AppendLine("per_camera_fisheye_material_count=" + result.PerCameraFisheyeMaterialPaths.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string materialPath in result.PerCameraFisheyeMaterialPaths)
            {
                sb.AppendLine("per_camera_fisheye_material_path=" + materialPath);
            }
            sb.AppendLine("camera_resolution=" + TopgearCameraResolution.x.ToString(CultureInfo.InvariantCulture) + "x" + TopgearCameraResolution.y.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("camera_fisheye_focal_px=" + FisheyeFocalLengthPixels().x.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("camera_fisheye_principal_point_px=" + (TopgearCameraResolution.x * 0.5f).ToString("F3", CultureInfo.InvariantCulture) + "," + (TopgearCameraResolution.y * 0.5f).ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("camera_count=" + result.CameraCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_count=" + result.LidarCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("camera_field_of_view_set_count=" + result.CameraFieldOfViewSetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("fisheye_sensor_configured_count=" + result.FisheyeSensorConfiguredCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("rgb_sensor_disabled_count=" + result.RgbSensorDisabledCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("image_publisher_fisheye_source_count=" + result.ImagePublisherFisheyeSourceCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("unity_sensors_image_publisher_configured_count=" + result.UnitySensorsImagePublisherConfiguredCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("extra_image_publisher_disabled_count=" + result.ExtraImagePublisherDisabledCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("legacy_vln_fisheye_image_publisher_removed_count=" + result.LegacyVlnFisheyeImagePublisherRemovedCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("image_publisher_frequency_set_count=" + result.ImagePublisherFrequencySetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("camera_info_publisher_frequency_set_count=" + result.CameraInfoPublisherFrequencySetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("legacy_camera_info_publisher_disabled_count=" + result.LegacyCameraInfoPublisherDisabledCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("fisheye_camera_info_publisher_configured_count=" + result.FisheyeCameraInfoPublisherConfiguredCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_sensor_frequency_set_count=" + result.LidarSensorFrequencySetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_max_range_set_count=" + result.LidarMaxRangeSetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_points_per_scan_set_count=" + result.LidarPointsPerScanSetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lidar_publisher_frequency_set_count=" + result.LidarPublisherFrequencySetCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("manual_save_success=" + (result.ManualSaveSuccess ? "1" : "0"));
            sb.AppendLine("manual_save_message=" + result.ManualSaveMessage.Replace("\n", " | "));
            sb.AppendLine("camera_names=" + string.Join(",", result.CameraNames));
            sb.AppendLine("lidar_names=" + string.Join(",", result.LidarNames));
            sb.AppendLine("preview_file_count=" + result.PreviewFiles.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < result.PreviewFiles.Count; i++)
            {
                sb.AppendLine("preview_file_" + i.ToString(CultureInfo.InvariantCulture) + "=" + result.PreviewFiles[i]);
            }
            sb.AppendLine("warning_count=" + result.Warnings.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string warning in result.Warnings)
            {
                sb.AppendLine("warning=" + warning);
            }
            sb.AppendLine("summary=" + result.Summary.Replace("\n", " | "));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        static string CameraViewName(string cameraName)
        {
            if (cameraName.Contains("Front", StringComparison.Ordinal)) return "front";
            if (cameraName.Contains("Rear", StringComparison.Ordinal)) return "rear";
            if (cameraName.Contains("Left", StringComparison.Ordinal)) return "left";
            if (cameraName.Contains("Right", StringComparison.Ordinal)) return "right";
            return cameraName.ToLowerInvariant().Replace(' ', '_');
        }

        static string CameraSortKey(string cameraName)
        {
            if (cameraName.Contains("Front", StringComparison.Ordinal)) return "0_front";
            if (cameraName.Contains("Left", StringComparison.Ordinal)) return "1_left";
            if (cameraName.Contains("Right", StringComparison.Ordinal)) return "2_right";
            if (cameraName.Contains("Rear", StringComparison.Ordinal)) return "3_rear";
            return "9_" + cameraName;
        }

        static string ProjectRootPath(string relativePath)
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")), relativePath);
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        public sealed class ApplyResult
        {
            public bool Success;
            public string ScenePath = string.Empty;
            public int CameraCount;
            public int LidarCount;
            public int CameraFieldOfViewSetCount;
            public int FisheyeSensorConfiguredCount;
            public int RgbSensorDisabledCount;
            public int ImagePublisherFisheyeSourceCount;
            public int UnitySensorsImagePublisherConfiguredCount;
            public int ExtraImagePublisherDisabledCount;
            public int LegacyVlnFisheyeImagePublisherRemovedCount;
            public int ImagePublisherFrequencySetCount;
            public int CameraInfoPublisherFrequencySetCount;
            public int LegacyCameraInfoPublisherDisabledCount;
            public int FisheyeCameraInfoPublisherConfiguredCount;
            public int PostProcessLayerSetCount;
            public int PostProcessLayerDisabledCount;
            public int LidarSensorFrequencySetCount;
            public int LidarMaxRangeSetCount;
            public int LidarPointsPerScanSetCount;
            public int LidarPublisherFrequencySetCount;
            public int LensDistortionLayerIndex = -1;
            public float LensDistortionIntensity;
            public float LensDistortionScale;
            public bool LensDistortionVolumeConfigured;
            public bool FisheyeMaterialConfigured;
            public string FisheyeMaterialPath = string.Empty;
            public readonly List<string> PerCameraFisheyeMaterialPaths = new();
            public bool ManualSaveSuccess;
            public string ManualSaveMessage = string.Empty;
            public string Summary = string.Empty;
            public int LidarPointsPerScan;
            public int LidarScanPatternSize;
            public readonly List<string> CameraNames = new();
            public readonly List<string> LidarNames = new();
            public readonly List<string> PreviewFiles = new();
            public readonly List<string> Warnings = new();

            public ApplyResult Fail(string message)
            {
                Success = false;
                Summary = message;
                return this;
            }
        }
    }
}
