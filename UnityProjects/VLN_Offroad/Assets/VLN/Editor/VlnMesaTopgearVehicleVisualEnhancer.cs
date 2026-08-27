using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VLN.Editor
{
    public static class VlnMesaTopgearVehicleVisualEnhancer
    {
        public const string CandidateScenePath = "Assets/VLN/Scenes/VLNMesaTopgearVehicleVisualCandidate.unity";

        const string BaseScenePath = VlnMesaTopgearVehicleCandidateBuilder.CandidateScenePath;
        const string PhysicsRootName = "ScoutWheelGround_PhysicsRoot";
        const string SensorRootName = "ScoutWheelGround_TopgearSensorSuite";
        const string VisualRootName = "ScoutWheelGround_VisualUrdf";
        const string TopgearVisualName = "ScoutWheelGround_TopgearV2Visual";
        const string EnhancementRootName = "VLN_MesaTopgearVehicleVisualEnhancement";
        const string ReviewCameraName = "VLN_MesaTopgearVehicleVisual_ReviewCamera";
        const string MaterialDirectory = "Assets/VLN/Materials/VehicleVisualRealism";
        const string TextureDirectory = MaterialDirectory + "/Textures";
        const string QuadMeshPath = MaterialDirectory + "/VLN_VehicleVisual_DetailQuad.asset";
        const string DustDecalMaterialPath = MaterialDirectory + "/VLN_VehicleVisual_SubtleSandDustDecal.mat";
        const string ResultRelativePath = "UnityProjects/VLN_Offroad_LargeAssetSandbox/Logs/vln_mesa_topgear_vehicle_visual_result.txt";

        static int s_FocusSceneViewAttempts;

        enum VehicleRole
        {
            None,
            Tire,
            Chassis,
            UpperHousing,
            UpperMetal,
            UpperScreen,
            UpperGps
        }

        [MenuItem("VLN/Mesa Desert/Build Topgear Vehicle Visual Candidate", priority = 373)]
        public static void BuildCandidateFromMenu()
        {
            var result = BuildCandidateScene();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_VISUAL_CANDIDATE_BUILT " + CandidateScenePath + " material_slots=" + result.VehicleMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
        }

        [MenuItem("VLN/Mesa Desert/Open Topgear Vehicle Visual Candidate", priority = 374)]
        public static void OpenCandidateForManualReview()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(CandidateScenePath)))
            {
                BuildCandidateScene();
            }
            else
            {
                EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
                ApplySavedSensorStateAndFisheye();
                SaveActiveSceneOnly();
            }

            ScheduleFocusSceneViewOnVehicle();
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_VISUAL_CANDIDATE_OPENED " + CandidateScenePath);
        }

        public static void RunBuildAndVisualSmokeTest()
        {
            try
            {
                var result = BuildCandidateScene();
                var smoke = CaptureAndAudit(result);
                EditorApplication.Exit(smoke.Success ? 0 : 1);
            }
            catch (Exception ex)
            {
                string resultPath = ProjectRootPath(ResultRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? string.Empty);
                File.WriteAllText(resultPath, "success=0\nexception=" + ex + "\n", Encoding.UTF8);
                Debug.LogError("VLN_MESA_TOPGEAR_VEHICLE_VISUAL_SMOKE_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        public static BuildResult BuildCandidateScene()
        {
            EnsureBaseScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(CandidateScenePath)) ?? string.Empty);
            Directory.CreateDirectory(ProjectRelativeToAbsolute(MaterialDirectory));
            Directory.CreateDirectory(ProjectRelativeToAbsolute(TextureDirectory));

            string baseAbsolute = ProjectRelativeToAbsolute(BaseScenePath);
            string baseShaBefore = Sha256File(baseAbsolute);

            var scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
            ApplySavedSensorStateAndFisheye();
            if (!EditorSceneManager.SaveScene(scene, CandidateScenePath, saveAsCopy: false))
            {
                throw new InvalidOperationException("Could not save vehicle visual candidate scene: " + CandidateScenePath);
            }
            scene = EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);

            RemoveIfExists(EnhancementRootName);
            RemoveIfExists(ReviewCameraName);
            ApplySavedSensorStateAndFisheye();

            var result = new BuildResult
            {
                ScenePath = CandidateScenePath,
                BaseScenePath = BaseScenePath,
                BaseSceneSha256Before = baseShaBefore
            };

            ApplyVehicleMaterials(result);
            CreateVehicleLocalLighting(result);
            CreateVehicleSurfaceDetails(result);
            CreateReviewCamera(result);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CandidateScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            result.BaseSceneSha256After = Sha256File(baseAbsolute);
            result.CandidateSceneSha256 = Sha256File(ProjectRelativeToAbsolute(CandidateScenePath));
            WriteConfig(result);
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_VISUAL_READY scene=" + CandidateScenePath + " material_slots=" + result.VehicleMaterialSlotCount.ToString(CultureInfo.InvariantCulture) + " lights=" + result.LocalLightCount.ToString(CultureInfo.InvariantCulture));
            return result;
        }

        static void EnsureBaseScene()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(BaseScenePath)))
            {
                VlnMesaTopgearVehicleCandidateBuilder.BuildCandidateScene();
            }
            if (!File.Exists(ProjectRelativeToAbsolute(BaseScenePath)))
            {
                throw new FileNotFoundException("Missing Mesa Topgear base scene", BaseScenePath);
            }
        }

        static void ApplySavedSensorStateAndFisheye()
        {
            VlnTopgearCameraDataPoseTuner.EnsureDecoupledIfSavedStateRequiresIt(saveScene: false, showDialog: false);
            VlnTopgearUpperAssemblyTuner.ApplySavedAssemblyIfPresent(saveScene: false, showDialog: false);
            VlnTopgearCameraDataPoseTuner.ApplySavedCameraDataPosesIfPresent(saveScene: false, showDialog: false);
            var sensorConfig = VlnTopgearFisheyeSensorConfig.ApplyCurrentSceneSensorConfig(saveScene: false);
            if (!sensorConfig.Success)
            {
                throw new InvalidOperationException("Vehicle visual candidate sensor config failed: " + sensorConfig.Summary);
            }
        }

        static void ApplyVehicleMaterials(BuildResult result)
        {
            var root = GameObject.Find(PhysicsRootName);
            if (root == null)
            {
                throw new InvalidOperationException("Missing vehicle physics root: " + PhysicsRootName);
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                VehicleRole role = ClassifyRenderer(renderer);
                if (role == VehicleRole.None)
                {
                    if (IsOfficialSensorRenderer(renderer))
                    {
                        result.SkippedOfficialSensorRendererCount++;
                    }
                    continue;
                }

                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    VehicleRole slotRole = RoleForMaterialSlot(role, materials[i].name, i);
                    materials[i] = EnsureMaterialVariant(materials[i], slotRole);
                    changed = true;
                    result.VehicleMaterialSlotCount++;
                    IncrementRole(result, slotRole);
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        static VehicleRole ClassifyRenderer(Renderer renderer)
        {
            if (IsOfficialSensorRenderer(renderer))
            {
                return VehicleRole.None;
            }

            string path = TransformPath(renderer.transform).ToLowerInvariant();
            string name = renderer.name.ToLowerInvariant();

            if (HasPathSegment(path, "front_left_wheel_link") || HasPathSegment(path, "front_right_wheel_link") ||
                HasPathSegment(path, "rear_left_wheel_link") || HasPathSegment(path, "rear_right_wheel_link") ||
                name.Contains("wheel_type") || name.Equals("wheel") || name.Contains("tire") || name.Contains("tyre"))
            {
                return VehicleRole.Tire;
            }

            if (HasPathSegment(path, TopgearVisualName.ToLowerInvariant()) || path.Contains("topgear_v2"))
            {
                return VehicleRole.UpperHousing;
            }

            if ((HasPathSegment(path, VisualRootName.ToLowerInvariant()) && HasPathSegment(path, "base_link")) ||
                name.Equals("base_link") || name.Contains("chassis") || name.Contains("body"))
            {
                return VehicleRole.Chassis;
            }

            return VehicleRole.None;
        }

        static VehicleRole RoleForMaterialSlot(VehicleRole objectRole, string materialName, int materialIndex)
        {
            string name = (materialName ?? string.Empty).ToLowerInvariant();
            if (objectRole == VehicleRole.UpperHousing)
            {
                if (name.Contains("screen") || name.Contains("glass"))
                {
                    return VehicleRole.UpperScreen;
                }
                if (name.Contains("iron") || name.Contains("metal") || name.Contains("plugin") || name.Contains("bolt"))
                {
                    return VehicleRole.UpperMetal;
                }
                if (name.Contains("gps"))
                {
                    return VehicleRole.UpperGps;
                }
                return VehicleRole.UpperHousing;
            }

            if (objectRole == VehicleRole.Tire)
            {
                return VehicleRole.Tire;
            }

            return objectRole;
        }

        static Material EnsureMaterialVariant(Material source, VehicleRole role)
        {
            string targetPath = MaterialDirectory + "/VLN_VehicleVisual_" + RoleName(role) + "_" + SafeAssetName(source.name) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (material == null)
            {
                material = new Material(source) { name = Path.GetFileNameWithoutExtension(targetPath) };
                AssetDatabase.CreateAsset(material, targetPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
            }

            ConfigureMaterial(material, source, role);
            ApplyRoleTextureMaps(material, role);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureMaterial(Material material, Material source, VehicleRole role)
        {
            Color sourceColor = source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : Color.gray;
            switch (role)
            {
                case VehicleRole.Tire:
                    ApplyStandardLook(material, Color.Lerp(sourceColor, new Color(0.042f, 0.039f, 0.034f, 1f), 0.74f), 0f, 0.075f, 1.35f);
                    break;
                case VehicleRole.Chassis:
                    ApplyStandardLook(material, Color.Lerp(sourceColor, new Color(0.47f, 0.40f, 0.31f, 1f), 0.18f), 0.07f, 0.19f, 1.08f);
                    break;
                case VehicleRole.UpperHousing:
                    ApplyStandardLook(material, Color.Lerp(sourceColor, new Color(0.13f, 0.12f, 0.105f, 1f), 0.52f), 0.02f, 0.16f, 1.08f);
                    break;
                case VehicleRole.UpperMetal:
                    ApplyStandardLook(material, Color.Lerp(sourceColor, new Color(0.32f, 0.30f, 0.26f, 1f), 0.20f), 0.36f, 0.25f, 1.02f);
                    break;
                case VehicleRole.UpperScreen:
                    ApplyStandardLook(material, new Color(0.025f, 0.032f, 0.034f, 1f), 0f, 0.52f, 0.90f);
                    break;
                case VehicleRole.UpperGps:
                    ApplyStandardLook(material, Color.Lerp(sourceColor, new Color(0.78f, 0.74f, 0.66f, 1f), 0.26f), 0f, 0.16f, 0.85f);
                    break;
            }
        }

        static void ApplyRoleTextureMaps(Material material, VehicleRole role)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_MainTex"))
            {
                string albedoPath = TextureDirectory + "/VLN_VehicleVisual_" + RoleName(role) + "_albedo_noise.png";
                Texture2D albedo = EnsureProceduralTexture(albedoPath, role, normalMap: false);
                if (albedo != null)
                {
                    material.SetTexture("_MainTex", albedo);
                    material.SetTextureScale("_MainTex", role == VehicleRole.Tire ? new Vector2(3.5f, 5.5f) : new Vector2(1.8f, 1.8f));
                }
            }

            if (material.HasProperty("_BumpMap") && (role == VehicleRole.Tire || role == VehicleRole.Chassis || role == VehicleRole.UpperHousing))
            {
                string normalPath = TextureDirectory + "/VLN_VehicleVisual_" + RoleName(role) + "_normal_noise.png";
                Texture2D normal = EnsureProceduralTexture(normalPath, role, normalMap: true);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
            }
        }

        static Texture2D EnsureProceduralTexture(string assetPath, VehicleRole role, bool normalMap)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, normalMap ? TextureFormat.RGB24 : TextureFormat.RGBA32, mipChain: true, linear: normalMap);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    float grain = ValueNoise01(x, y, 31 + (int)role * 97);
                    float fine = ValueNoise01(x * 3, y * 3, 173 + (int)role * 43);
                    float stripe = Mathf.Abs(Mathf.Sin((v * 18f + fine * 1.8f) * Mathf.PI));

                    if (normalMap)
                    {
                        float height = RoleHeightSignal(role, u, v, grain, fine, stripe);
                        float hx = RoleHeightSignal(role, Mathf.Clamp01(u + 1f / size), v, ValueNoise01(x + 1, y, 31 + (int)role * 97), fine, stripe) - height;
                        float hy = RoleHeightSignal(role, u, Mathf.Clamp01(v + 1f / size), grain, ValueNoise01(x * 3, y * 3 + 1, 173 + (int)role * 43), stripe) - height;
                        Vector3 normal = new Vector3(-hx * 7f, -hy * 7f, 1f).normalized;
                        texture.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
                    }
                    else
                    {
                        texture.SetPixel(x, y, RoleAlbedoSignal(role, grain, fine, stripe));
                    }
                }
            }
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            string absolutePath = ProjectRelativeToAbsolute(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normalMap;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = !normalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static Color RoleAlbedoSignal(VehicleRole role, float grain, float fine, float stripe)
        {
            switch (role)
            {
                case VehicleRole.Tire:
                    {
                        float tread = Mathf.Lerp(0.74f, 1.20f, Mathf.SmoothStep(0.50f, 0.98f, stripe));
                        float dust = Mathf.Lerp(0.0f, 0.13f, grain * fine);
                        return new Color(0.035f * tread + dust, 0.033f * tread + dust * 0.86f, 0.030f * tread + dust * 0.62f, 1f);
                    }
                case VehicleRole.Chassis:
                    {
                        float dust = Mathf.Lerp(0.88f, 1.18f, grain * 0.65f + fine * 0.35f);
                        return new Color(0.86f * dust, 0.80f * dust, 0.69f * dust, 1f);
                    }
                case VehicleRole.UpperHousing:
                    {
                        float dust = Mathf.Lerp(0.0f, 0.06f, Mathf.Pow(grain, 1.4f));
                        return new Color(0.090f + dust, 0.084f + dust * 0.86f, 0.074f + dust * 0.66f, 1f);
                    }
                case VehicleRole.UpperMetal:
                    {
                        float tone = Mathf.Lerp(0.82f, 1.12f, grain);
                        return new Color(0.34f * tone, 0.32f * tone, 0.29f * tone, 1f);
                    }
                case VehicleRole.UpperScreen:
                    return new Color(0.020f, 0.034f + fine * 0.025f, 0.038f + grain * 0.03f, 1f);
                case VehicleRole.UpperGps:
                    {
                        float tone = Mathf.Lerp(0.92f, 1.05f, grain);
                        return new Color(0.82f * tone, 0.80f * tone, 0.73f * tone, 1f);
                    }
                default:
                    return Color.white;
            }
        }

        static float RoleHeightSignal(VehicleRole role, float u, float v, float grain, float fine, float stripe)
        {
            float baseNoise = grain * 0.65f + fine * 0.35f;
            if (role == VehicleRole.Tire)
            {
                return baseNoise * 0.20f + Mathf.SmoothStep(0.58f, 0.94f, stripe) * 0.80f;
            }
            if (role == VehicleRole.Chassis)
            {
                return baseNoise * 0.45f + Mathf.Sin((u + v) * Mathf.PI * 5f) * 0.035f;
            }
            return baseNoise * 0.30f;
        }

        static float ValueNoise01(int x, int y, int seed)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 1442695041;
                n = (n ^ (n >> 13)) * 1274126177;
                n ^= n >> 16;
                return (n & 0x7fffffff) / 2147483647f;
            }
        }

        static void ApplyStandardLook(Material material, Color color, float metallic, float glossiness, float bumpScale)
        {
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", glossiness);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", glossiness);
            }
            if (material.HasProperty("_BumpScale"))
            {
                material.SetFloat("_BumpScale", bumpScale);
            }
            if (material.HasProperty("_OcclusionStrength"))
            {
                material.SetFloat("_OcclusionStrength", 1f);
            }
        }

        static void CreateVehicleLocalLighting(BuildResult result)
        {
            var vehicle = GameObject.Find(PhysicsRootName);
            if (vehicle == null)
            {
                throw new InvalidOperationException("Missing vehicle root for visual lighting.");
            }

            var root = EnsureEnhancementRoot(vehicle.transform);

            CreateSpotLight(root.transform, "VLN_TopgearVehicle_KeySoftbox", new Vector3(2.6f, 3.2f, -3.8f), new Vector3(0.0f, 0.8f, 0.2f), new Color(1.0f, 0.82f, 0.58f, 1f), 0.72f, 7.2f, 54f, true);
            CreateSpotLight(root.transform, "VLN_TopgearVehicle_CoolRim", new Vector3(-2.4f, 2.4f, 2.9f), new Vector3(0.0f, 0.9f, 0.0f), new Color(0.58f, 0.70f, 1.0f, 1f), 0.30f, 5.6f, 48f, false);
            CreatePointLight(root.transform, "VLN_TopgearVehicle_LowDustFill", new Vector3(0f, 0.95f, -2.2f), new Color(1.0f, 0.64f, 0.34f, 1f), 0.18f, 4.0f);
            result.LocalLightCount = root.GetComponentsInChildren<Light>(true).Length;
        }

        static void CreateVehicleSurfaceDetails(BuildResult result)
        {
            var vehicle = GameObject.Find(PhysicsRootName);
            if (vehicle == null)
            {
                throw new InvalidOperationException("Missing vehicle root for surface details.");
            }

            var root = EnsureEnhancementRoot(vehicle.transform);
            Material dust = EnsureDustDecalMaterial();
            Mesh quad = EnsureDetailQuadMesh();

            if (TryGetRoleLocalBounds(vehicle.transform, VehicleRole.Chassis, out Bounds chassis))
            {
                float zSize = Mathf.Clamp(chassis.size.z * 0.70f, 0.45f, 1.55f);
                float xSize = Mathf.Clamp(chassis.size.x * 0.74f, 0.42f, 1.20f);
                float sideHeight = Mathf.Clamp(chassis.size.y * 0.42f, 0.10f, 0.38f);
                float yMid = Mathf.Lerp(chassis.min.y, chassis.max.y, 0.55f);
                float topY = chassis.max.y + 0.008f;
                float leftX = chassis.min.x - 0.006f;
                float rightX = chassis.max.x + 0.006f;
                float frontZ = chassis.max.z + 0.006f;
                float rearZ = chassis.min.z - 0.006f;

                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Chassis_TopDeck", new Vector3(chassis.center.x, topY, chassis.center.z), Vector3.up, Vector3.forward, xSize, zSize * 0.55f);
                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Chassis_LeftLower", new Vector3(leftX, yMid, chassis.center.z), Vector3.left, Vector3.up, zSize, sideHeight);
                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Chassis_RightLower", new Vector3(rightX, yMid, chassis.center.z), Vector3.right, Vector3.up, zSize, sideHeight);
                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Chassis_FrontLip", new Vector3(chassis.center.x, yMid, frontZ), Vector3.forward, Vector3.up, xSize * 0.72f, sideHeight * 0.70f);
                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Chassis_RearLip", new Vector3(chassis.center.x, yMid, rearZ), Vector3.back, Vector3.up, xSize * 0.72f, sideHeight * 0.70f);
            }

            if (TryGetRoleLocalBounds(vehicle.transform, VehicleRole.UpperHousing, out Bounds upper))
            {
                float zSize = Mathf.Clamp(upper.size.z * 0.62f, 0.16f, 0.70f);
                float xSize = Mathf.Clamp(upper.size.x * 0.64f, 0.16f, 0.70f);
                float sideHeight = Mathf.Clamp(upper.size.y * 0.30f, 0.08f, 0.34f);
                float yMid = Mathf.Lerp(upper.min.y, upper.max.y, 0.38f);

                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Upper_LeftEdge", new Vector3(upper.min.x - 0.005f, yMid, upper.center.z), Vector3.left, Vector3.up, zSize, sideHeight);
                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Upper_RightEdge", new Vector3(upper.max.x + 0.005f, yMid, upper.center.z), Vector3.right, Vector3.up, zSize, sideHeight);
                AddDetailQuad(root.transform, quad, dust, "VLN_Dust_Upper_BackEdge", new Vector3(upper.center.x, yMid, upper.min.z - 0.005f), Vector3.back, Vector3.up, xSize, sideHeight);
            }

            result.SurfaceDetailQuadCount = root.GetComponentsInChildren<MeshRenderer>(true).Count(renderer => renderer != null && renderer.name.StartsWith("VLN_Dust_", StringComparison.Ordinal));
        }

        static GameObject EnsureEnhancementRoot(Transform vehicle)
        {
            var existing = GameObject.Find(EnhancementRootName);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(EnhancementRootName);
            root.transform.SetParent(vehicle, worldPositionStays: false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        static void AddDetailQuad(Transform parent, Mesh mesh, Material material, string name, Vector3 localPosition, Vector3 localNormal, Vector3 localUp, float width, float height)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, worldPositionStays: false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.LookRotation(localNormal.normalized, localUp.normalized);
            obj.transform.localScale = new Vector3(width, height, 1f);
            var filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static bool TryGetRoleLocalBounds(Transform vehicleRoot, VehicleRole role, out Bounds localBounds)
        {
            bool initialized = false;
            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var renderer in vehicleRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || ClassifyRenderer(renderer) != role)
                {
                    continue;
                }

                Bounds world = renderer.bounds;
                for (int ix = 0; ix <= 1; ix++)
                {
                    for (int iy = 0; iy <= 1; iy++)
                    {
                        for (int iz = 0; iz <= 1; iz++)
                        {
                            Vector3 corner = new Vector3(ix == 0 ? world.min.x : world.max.x, iy == 0 ? world.min.y : world.max.y, iz == 0 ? world.min.z : world.max.z);
                            Vector3 local = vehicleRoot.InverseTransformPoint(corner);
                            if (!initialized)
                            {
                                localBounds = new Bounds(local, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(local);
                            }
                        }
                    }
                }
            }
            return initialized;
        }

        static Material EnsureDustDecalMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DustDecalMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = Path.GetFileNameWithoutExtension(DustDecalMaterialPath) };
                AssetDatabase.CreateAsset(material, DustDecalMaterialPath);
            }

            Texture2D texture = EnsureDustDecalTexture();
            ConfigureTransparentMaterial(material, texture, new Color(0.86f, 0.68f, 0.46f, 0.24f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D EnsureDustDecalTexture()
        {
            string assetPath = TextureDirectory + "/VLN_VehicleVisual_sand_dust_decal.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    float edgeFade = Mathf.SmoothStep(0f, 0.18f, u) * Mathf.SmoothStep(0f, 0.18f, 1f - u) * Mathf.SmoothStep(0f, 0.16f, v) * Mathf.SmoothStep(0f, 0.16f, 1f - v);
                    float streak = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v - 0.48f - (ValueNoise01(x / 7, 3, 911) - 0.5f) * 0.10f) * 5.2f), 1.6f);
                    float grain = ValueNoise01(x, y, 611) * 0.65f + ValueNoise01(x * 3, y * 3, 719) * 0.35f;
                    float alpha = Mathf.Clamp01(edgeFade * (0.08f + streak * 0.58f + Mathf.Pow(grain, 3.2f) * 0.42f));
                    texture.SetPixel(x, y, new Color(1f, 0.88f, 0.68f, alpha));
                }
            }
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            string absolutePath = ProjectRelativeToAbsolute(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? string.Empty);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static Mesh EnsureDetailQuadMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(QuadMeshPath);
            if (mesh != null)
            {
                return mesh;
            }

            mesh = new Mesh { name = "VLN_VehicleVisual_DetailQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, QuadMeshPath);
            return mesh;
        }

        static void ConfigureTransparentMaterial(Material material, Texture2D texture, Color color)
        {
            material.shader = Shader.Find("Standard");
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (texture != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
        }

        static void CreateSpotLight(Transform parent, string name, Vector3 localPosition, Vector3 localTarget, Color color, float intensity, float range, float spotAngle, bool shadows)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, worldPositionStays: false);
            obj.transform.localPosition = localPosition;
            obj.transform.LookAt(parent.TransformPoint(localTarget));
            var light = obj.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.58f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = shadows ? 0.32f : 0f;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        static void CreatePointLight(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, worldPositionStays: false);
            obj.transform.localPosition = localPosition;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        static void CreateReviewCamera(BuildResult result)
        {
            var vehicle = GameObject.Find(PhysicsRootName);
            if (vehicle == null)
            {
                return;
            }

            var cameraObject = new GameObject(ReviewCameraName);
            Vector3 forward = vehicle.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            Vector3 right = vehicle.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            cameraObject.transform.position = vehicle.transform.position - forward * 5.2f + right * 3.1f + Vector3.up * 2.45f;
            cameraObject.transform.LookAt(vehicle.transform.position + Vector3.up * 0.95f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 900f;
            camera.depth = 25f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            result.ReviewCameraCount = 1;
        }

        static SmokeResult CaptureAndAudit(BuildResult build)
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = ProjectRootPath(ResultRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? string.Empty);

            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            var wheelColliders = UnityEngine.Object.FindObjectsOfType<WheelCollider>(true);
            var rigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>(true);
            int fisheyeSensorCount = CountComponentTypeName("FisheyeCameraSensor");
            int imagePublisherCount = CountComponentTypeName("ImageMsgPublisher");
            int missingMaterialSlots = CountMissingMaterialSlots(renderers);
            int internalErrorMaterials = CountInternalErrorMaterials(renderers);
            int visualDetailColliderCount = CountVisualDetailColliders();

            string frontPath = SaveVehicleView(logRoot, "front_three_quarter", new Vector3(3.2f, 2.4f, -5.2f), 42f);
            string sidePath = SaveVehicleView(logRoot, "side_profile", new Vector3(5.8f, 2.0f, 0.2f), 38f);
            string wheelPath = SaveVehicleView(logRoot, "wheel_close", new Vector3(2.2f, 0.9f, -2.3f), 31f);
            string upperPath = SaveVehicleView(logRoot, "upper_sensor_module", new Vector3(2.1f, 2.25f, -2.4f), 30f, Vector3.up * 1.45f);

            bool baseSceneUnchanged = string.Equals(build.BaseSceneSha256Before, build.BaseSceneSha256After, StringComparison.OrdinalIgnoreCase);
            bool pass = File.Exists(ProjectRelativeToAbsolute(CandidateScenePath)) && baseSceneUnchanged &&
                        build.VehicleMaterialSlotCount >= 6 && build.LocalLightCount >= 2 && build.SurfaceDetailQuadCount >= 6 && visualDetailColliderCount == 0 &&
                        wheelColliders.Length == 4 && rigidbodies.Length >= 1 &&
                        fisheyeSensorCount >= 4 && imagePublisherCount >= 4 && missingMaterialSlots == 0 && internalErrorMaterials == 0 &&
                        File.Exists(frontPath) && File.Exists(sidePath) && File.Exists(wheelPath) && File.Exists(upperPath);

            var sb = new StringBuilder();
            sb.AppendLine("success=" + (pass ? "1" : "0"));
            sb.AppendLine("stage=mesa_topgear_vehicle_visual_candidate");
            sb.AppendLine("scene_path=" + CandidateScenePath);
            sb.AppendLine("base_scene_path=" + BaseScenePath);
            sb.AppendLine("base_scene_sha256_before=" + build.BaseSceneSha256Before);
            sb.AppendLine("base_scene_sha256_after=" + build.BaseSceneSha256After);
            sb.AppendLine("base_scene_unchanged=" + (baseSceneUnchanged ? "1" : "0"));
            sb.AppendLine("candidate_scene_sha256=" + build.CandidateSceneSha256);
            sb.AppendLine("vehicle_material_variant_slot_count=" + build.VehicleMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("tire_material_slot_count=" + build.TireMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("chassis_material_slot_count=" + build.ChassisMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("upper_housing_material_slot_count=" + build.UpperHousingMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("upper_metal_material_slot_count=" + build.UpperMetalMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("upper_screen_material_slot_count=" + build.UpperScreenMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("upper_gps_material_slot_count=" + build.UpperGpsMaterialSlotCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("skipped_official_sensor_renderer_count=" + build.SkippedOfficialSensorRendererCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("local_vehicle_light_count=" + build.LocalLightCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("surface_detail_quad_count=" + build.SurfaceDetailQuadCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("surface_detail_collider_count=" + visualDetailColliderCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("review_camera_count=" + build.ReviewCameraCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("renderer_count=" + renderers.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("rigidbody_count=" + rigidbodies.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("wheel_collider_count=" + wheelColliders.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("fisheye_sensor_count=" + fisheyeSensorCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("image_publisher_count=" + imagePublisherCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("missing_material_slots=" + missingMaterialSlots.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("internal_error_materials=" + internalErrorMaterials.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("terrain_visual_changes=0");
            sb.AppendLine("terrain_physics_changes=0");
            sb.AppendLine("vehicle_physics_changes=0");
            sb.AppendLine("vehicle_front_screenshot=" + frontPath);
            sb.AppendLine("vehicle_side_screenshot=" + sidePath);
            sb.AppendLine("wheel_close_screenshot=" + wheelPath);
            sb.AppendLine("upper_sensor_module_screenshot=" + upperPath);
            sb.AppendLine("finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            File.WriteAllText(resultPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("VLN_MESA_TOPGEAR_VEHICLE_VISUAL_RESULT " + resultPath + " pass=" + (pass ? 1 : 0));
            return new SmokeResult { Success = pass };
        }

        static string SaveVehicleView(string logRoot, string viewName, Vector3 localCameraOffset, float fov, Vector3? targetOffset = null)
        {
            var vehicle = GameObject.Find(PhysicsRootName);
            Vector3 vehiclePosition = vehicle != null ? vehicle.transform.position : Vector3.zero;
            Quaternion vehicleRotation = vehicle != null ? vehicle.transform.rotation : Quaternion.identity;
            Vector3 position = vehiclePosition + vehicleRotation * localCameraOffset;
            Vector3 target = vehiclePosition + vehicleRotation * (targetOffset ?? Vector3.up * 0.8f);
            return SaveView(logRoot, viewName, position, target, fov, 500f);
        }

        static string SaveView(string logRoot, string viewName, Vector3 position, Vector3 target, float fov, float farClip)
        {
            string path = Path.Combine(logRoot, "vln_mesa_topgear_vehicle_visual_" + viewName + ".png");
            var cameraObject = new GameObject("MesaTopgearVehicleVisual_" + viewName + "_ScreenshotCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = 0.04f;
                camera.farClipPlane = farClip;
                camera.fieldOfView = fov;
                camera.allowHDR = true;
                cameraObject.transform.position = position;
                cameraObject.transform.LookAt(target);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            return path;
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
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void WriteConfig(BuildResult result)
        {
            string path = ProjectRootPath("config/mesa_topgear_vehicle_visual_candidate.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"stage\": \"mesa_topgear_vehicle_visual_candidate\",");
            sb.AppendLine("  \"scene_path\": \"" + CandidateScenePath + "\",");
            sb.AppendLine("  \"base_scene_path\": \"" + BaseScenePath + "\",");
            sb.AppendLine("  \"notes\": \"Vehicle-only visual candidate. It keeps the original Mesa world, vehicle dynamics, sensors and ROS2 topics unchanged.\",");
            sb.AppendLine("  \"base_scene_unchanged\": " + (string.Equals(result.BaseSceneSha256Before, result.BaseSceneSha256After, StringComparison.OrdinalIgnoreCase) ? "true" : "false") + ",");
            sb.AppendLine("  \"vehicle_material_variant_slots\": " + result.VehicleMaterialSlotCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"local_vehicle_lights\": " + result.LocalLightCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"surface_detail_quads\": " + result.SurfaceDetailQuadCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        static int CountVisualDetailColliders()
        {
            var root = GameObject.Find(EnhancementRootName);
            if (root == null)
            {
                return 0;
            }
            return root.GetComponentsInChildren<Collider>(true).Length;
        }

        static bool IsOfficialSensorRenderer(Renderer renderer)
        {
            string path = TransformPath(renderer.transform).ToLowerInvariant();
            return path.Contains(SensorRootName.ToLowerInvariant()) || path.Contains("realsense") || path.Contains("d405") ||
                   path.Contains("velodyne") || path.Contains("vlp16") || path.Contains("lidar") || path.Contains("rgbcamera") ||
                   path.Contains("fisheye") || path.Contains("camera_visual") || path.Contains("cameravisual");
        }

        static bool HasPathSegment(string path, string segment)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(segment))
            {
                return false;
            }
            foreach (string part in path.Split('/'))
            {
                if (string.Equals(part, segment, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        static string TransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }
            var names = new System.Collections.Generic.List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static void IncrementRole(BuildResult result, VehicleRole role)
        {
            switch (role)
            {
                case VehicleRole.Tire:
                    result.TireMaterialSlotCount++;
                    break;
                case VehicleRole.Chassis:
                    result.ChassisMaterialSlotCount++;
                    break;
                case VehicleRole.UpperHousing:
                    result.UpperHousingMaterialSlotCount++;
                    break;
                case VehicleRole.UpperMetal:
                    result.UpperMetalMaterialSlotCount++;
                    break;
                case VehicleRole.UpperScreen:
                    result.UpperScreenMaterialSlotCount++;
                    break;
                case VehicleRole.UpperGps:
                    result.UpperGpsMaterialSlotCount++;
                    break;
            }
        }

        static string RoleName(VehicleRole role)
        {
            return role.ToString().ToLowerInvariant();
        }

        static int CountMissingMaterialSlots(Renderer[] renderers)
        {
            int count = 0;
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        static int CountInternalErrorMaterials(Renderer[] renderers)
        {
            int count = 0;
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null && material.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        static int CountComponentTypeName(string typeName)
        {
            int count = 0;
            foreach (var component in UnityEngine.Object.FindObjectsOfType<Component>(true))
            {
                if (component != null && component.GetType().Name.Contains(typeName, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        static string Sha256File(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return string.Empty;
            }
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        static string SafeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "material";
            }
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }
            return sb.ToString().Trim('_');
        }

        static void RemoveIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        static void SaveActiveSceneOnly()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scene.path);
                AssetDatabase.SaveAssets();
            }
        }

        static string ProjectRelativeToAbsolute(string path)
        {
            return Path.Combine(Application.dataPath, "..", path);
        }

        static string ProjectRootPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", relativePath));
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
            GameObject target = GameObject.Find(PhysicsRootName) ?? GameObject.Find(ReviewCameraName);
            bool hasSceneView = SceneView.sceneViews != null && SceneView.sceneViews.Count > 0;
            if (target != null && hasSceneView)
            {
                Selection.activeGameObject = target;
                foreach (SceneView sceneView in SceneView.sceneViews)
                {
                    if (sceneView != null)
                    {
                        sceneView.FrameSelected();
                        sceneView.Repaint();
                    }
                }
                EditorApplication.update -= FocusSceneViewOnVehicleTick;
                return;
            }
            if (s_FocusSceneViewAttempts >= 180)
            {
                EditorApplication.update -= FocusSceneViewOnVehicleTick;
            }
        }

        public sealed class BuildResult
        {
            public string ScenePath;
            public string BaseScenePath;
            public string BaseSceneSha256Before;
            public string BaseSceneSha256After;
            public string CandidateSceneSha256;
            public int VehicleMaterialSlotCount;
            public int TireMaterialSlotCount;
            public int ChassisMaterialSlotCount;
            public int UpperHousingMaterialSlotCount;
            public int UpperMetalMaterialSlotCount;
            public int UpperScreenMaterialSlotCount;
            public int UpperGpsMaterialSlotCount;
            public int SkippedOfficialSensorRendererCount;
            public int LocalLightCount;
            public int SurfaceDetailQuadCount;
            public int ReviewCameraCount;
        }

        public sealed class SmokeResult
        {
            public bool Success;
        }
    }
}
