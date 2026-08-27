using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnImportedWorldSceneRegistry
    {
        public const string MeadowSourceScenePath = "Assets/NatureManufacture Assets/Meadow Environment Dynamic Nature/Demo Scenes/Unity Standard Demo Scene.unity";
        public const string MeadowCandidateScenePath = "Assets/VLN/Scenes/VLNMeadowDynamicNatureWorldCandidate.unity";
        public const string MeadowAssetRootPath = "Assets/NatureManufacture Assets/Meadow Environment Dynamic Nature";
        const string MeadowParticle01MaterialPath = MeadowAssetRootPath + "/Insects and Particles/Models/Materials/M_meadow_particle_01.mat";
        const string MeadowParticle02MaterialPath = MeadowAssetRootPath + "/Insects and Particles/Models/Materials/M_meadow_particle_02.mat";
        const string MeadowInsect01MaterialPath = MeadowAssetRootPath + "/Insects and Particles/Models/Materials/M_meadow_insects_01.mat";
        const string MeadowLeafMaterialPath = MeadowAssetRootPath + "/Insects and Particles/Models/Materials/M_leaf_particles.mat";
        public const string ForestLakeSourceScenePath = "Assets/ForestLake/Maps/Demo_01.unity";
        public const string ForestLakeCandidateScenePath = "Assets/VLN/Scenes/VLNForestLakeWorldCandidate.unity";
        static readonly HashSet<int> MeadowEditorIconClassIds = new HashSet<int>
        {
            82,  // AudioSource
            108, // Light
            122, // Halo
            123, // LensFlare
            124, // FlareLayer
            182, // WindZone
            198, // ParticleSystem
            215, // ReflectionProbe
            220, // LightProbeGroup
            259, // LightProbeProxyVolume
        };
        static int s_MeadowSceneViewCleanupAttempts;

        static VlnImportedWorldSceneRegistry()
        {
            EditorSceneManager.sceneOpened += OnImportedWorldSceneOpened;
        }

        static void OnImportedWorldSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            if (scene.path == MeadowCandidateScenePath)
            {
                ScheduleMeadowSceneViewIconCleanup();
            }
        }

        [MenuItem("VLN/World Models/Meadow Dynamic Nature/Open Candidate")]
        public static void OpenMeadowForManualReview()
        {
            OpenCandidate(WorldSpec.Meadow());
        }

        [MenuItem("VLN/World Models/ForestLake/Open Candidate")]
        public static void OpenForestLakeForManualReview()
        {
            OpenCandidate(WorldSpec.ForestLake());
        }

        [MenuItem("VLN/World Models/Meadow Dynamic Nature/Build Candidate")]
        public static void BuildMeadowCandidateFromMenu()
        {
            BuildCandidateScene(WorldSpec.Meadow());
        }

        [MenuItem("VLN/World Models/Meadow Dynamic Nature/Fix Dynamic Missing Materials")]
        public static void FixMeadowDynamicMissingMaterialsFromMenu()
        {
            FixMeadowDynamicMissingMaterials(false);
        }

        [MenuItem("VLN/World Models/Meadow Dynamic Nature/Hide Scene View Editor Icons")]
        public static void HideMeadowSceneViewEditorIconsFromMenu()
        {
            ApplyMeadowSceneViewIconCleanup(true);
        }

        public static void HideMeadowSceneViewEditorIconsBatch()
        {
            OpenCandidate(WorldSpec.Meadow());
            ApplyMeadowSceneViewIconCleanup(true);
            EditorApplication.Exit(0);
        }

        public static void FixMeadowDynamicMissingMaterialsBatch()
        {
            bool pass = FixMeadowDynamicMissingMaterials(true);
            EditorApplication.Exit(pass ? 0 : 1);
        }

        [MenuItem("VLN/World Models/ForestLake/Build Candidate")]
        public static void BuildForestLakeCandidateFromMenu()
        {
            BuildCandidateScene(WorldSpec.ForestLake());
        }

        public static void RunMeadowSmokeTest()
        {
            RunSmokeTest(WorldSpec.Meadow());
        }

        public static void RunForestLakeSmokeTest()
        {
            RunSmokeTest(WorldSpec.ForestLake());
        }

        static void OpenCandidate(WorldSpec spec)
        {
            if (!File.Exists(ProjectRelativeToAbsolute(spec.CandidateScenePath)))
            {
                BuildCandidateScene(spec);
            }
            else
            {
                EditorSceneManager.OpenScene(spec.CandidateScenePath, OpenSceneMode.Single);
            }

            if (spec.CleanSceneViewEditorIconsOnOpen)
            {
                ScheduleMeadowSceneViewIconCleanup();
            }

            Debug.Log(spec.LogPrefix + "_OPENED_FOR_MANUAL_REVIEW " + spec.CandidateScenePath);
        }

        static void BuildCandidateScene(WorldSpec spec)
        {
            EnsureSourceScene(spec);
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(spec.CandidateScenePath)) ?? string.Empty);

            if (File.Exists(ProjectRelativeToAbsolute(spec.CandidateScenePath)))
            {
                EditorSceneManager.OpenScene(spec.CandidateScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(spec.SourceScenePath, OpenSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), spec.CandidateScenePath);
                EditorSceneManager.OpenScene(spec.CandidateScenePath, OpenSceneMode.Single);
            }

            EnsureDevelopmentMarker(spec);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), spec.CandidateScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(spec.LogPrefix + "_CANDIDATE_BUILT " + spec.CandidateScenePath);
        }

        static void RunSmokeTest(WorldSpec spec)
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, spec.ResultFileName);

            try
            {
                BuildCandidateScene(spec);
                EditorSceneManager.OpenScene(spec.CandidateScenePath, OpenSceneMode.Single);

                var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
                var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                var colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
                Bounds bounds = CalculateSceneBounds(terrains, renderers);
                int missingMaterialSlots = CountMissingMaterialSlots(renderers);
                int internalErrorMaterials = CountInternalErrorMaterials(renderers);

                string overviewPath = Path.Combine(logRoot, spec.ScreenshotPrefix + "_overview.png");
                string cameraPath = Path.Combine(logRoot, spec.ScreenshotPrefix + "_scene_camera.png");
                string topPath = Path.Combine(logRoot, spec.ScreenshotPrefix + "_top_layout.png");

                SaveView(overviewPath,
                    bounds.center + new Vector3(bounds.extents.x * 0.42f, Mathf.Max(bounds.extents.y * 1.65f, 120f), -bounds.extents.z * 0.62f),
                    bounds.center,
                    42f,
                    Mathf.Max(bounds.size.magnitude * 3f, 1200f),
                    false);
                SaveExistingCameraView(cameraPath, cameras, bounds);
                SaveView(topPath,
                    bounds.center + new Vector3(0f, Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z) * 1.05f, 320f), 0f),
                    bounds.center,
                    52f,
                    Mathf.Max(bounds.size.magnitude * 3f, 1200f),
                    true);

                bool pass = File.Exists(ProjectRelativeToAbsolute(spec.CandidateScenePath)) &&
                            renderers.Length >= spec.MinRendererCount &&
                            terrains.Length >= spec.MinTerrainCount &&
                            missingMaterialSlots == 0 &&
                            internalErrorMaterials == 0 &&
                            File.Exists(overviewPath) && File.Exists(cameraPath) && File.Exists(topPath);

                File.WriteAllText(resultPath,
                    "started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                    "stage=" + spec.StageName + "\n" +
                    "scene_path=" + spec.CandidateScenePath + "\n" +
                    "source_scene_path=" + spec.SourceScenePath + "\n" +
                    "terrain_count=" + terrains.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "camera_count=" + cameras.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "renderer_count=" + renderers.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "collider_count=" + colliders.Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "missing_material_slots=" + missingMaterialSlots.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "internal_error_materials=" + internalErrorMaterials.ToString(CultureInfo.InvariantCulture) + "\n" +
                    "scene_bounds_center=" + FormatVector(bounds.center) + "\n" +
                    "scene_bounds_size=" + FormatVector(bounds.size) + "\n" +
                    "overview_screenshot=" + overviewPath + "\n" +
                    "scene_camera_screenshot=" + cameraPath + "\n" +
                    "top_layout_screenshot=" + topPath + "\n" +
                    "finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" +
                    "success=" + (pass ? "1" : "0") + "\n");

                Debug.Log(spec.LogPrefix + "_SMOKE_RESULT " + resultPath + " pass=" + (pass ? 1 : 0));
                EditorApplication.Exit(pass ? 0 : 1);
            }
            catch (Exception ex)
            {
                File.WriteAllText(resultPath, "success=0\nexception=" + ex + "\n");
                Debug.LogError(spec.LogPrefix + "_SMOKE_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        static bool FixMeadowDynamicMissingMaterials(bool batchMode)
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string reportPath = Path.Combine(logRoot, "vln_meadow_missing_material_audit.txt");
            var report = new StringBuilder();
            report.AppendLine("started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            report.AppendLine("target_scene=" + MeadowCandidateScenePath);
            report.AppendLine("asset_root=" + MeadowAssetRootPath);

            try
            {
                EnsureSourceScene(WorldSpec.Meadow());
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var fallbacks = LoadMeadowMaterialFallbacks();
                var stats = new MaterialFixStats();

                FixMeadowPrefabMaterials(fallbacks, report, stats);
                BuildCandidateScene(WorldSpec.Meadow());
                EditorSceneManager.OpenScene(MeadowCandidateScenePath, OpenSceneMode.Single);

                Renderer[] sceneRenderers = GetActiveSceneRenderers(includeInactive: true);
                stats.SceneRenderersScanned = sceneRenderers.Length;
                int sceneMissingBefore = CountMissingMaterialSlots(sceneRenderers);
                report.AppendLine("scene_missing_slots_before=" + sceneMissingBefore.ToString(CultureInfo.InvariantCulture));
                foreach (var renderer in sceneRenderers)
                {
                    FixRendererMaterials(renderer, "scene", fallbacks, report, stats);
                }

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MeadowCandidateScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                Renderer[] refreshedRenderers = GetActiveSceneRenderers(includeInactive: true);
                int sceneMissingAfterAll = CountMissingMaterialSlots(refreshedRenderers);
                int activeMissingAfterAll = CountMissingMaterialSlots(UnityEngine.Object.FindObjectsOfType<Renderer>());
                int internalErrorMaterials = CountInternalErrorMaterials(UnityEngine.Object.FindObjectsOfType<Renderer>());

                report.AppendLine("prefabs_scanned=" + stats.PrefabsScanned.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("prefabs_touched=" + stats.PrefabsTouched.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("prefab_missing_slots_before=" + stats.PrefabMissingSlotsBefore.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("scene_renderers_scanned=" + stats.SceneRenderersScanned.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("renderers_touched=" + stats.RenderersTouched.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("material_slots_fixed=" + stats.MaterialSlotsFixed.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("unresolved_renderers=" + stats.UnresolvedRenderers.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("scene_missing_slots_after_all=" + sceneMissingAfterAll.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("active_missing_slots_after_all=" + activeMissingAfterAll.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("active_internal_error_materials_after_all=" + internalErrorMaterials.ToString(CultureInfo.InvariantCulture));
                report.AppendLine("finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                bool pass = sceneMissingAfterAll == 0 && activeMissingAfterAll == 0 && internalErrorMaterials == 0 && stats.UnresolvedRenderers == 0;
                report.AppendLine("success=" + (pass ? "1" : "0"));
                File.WriteAllText(reportPath, report.ToString());
                Debug.Log("VLN_MEADOW_DYNAMIC_MATERIAL_FIX_REPORT " + reportPath + " pass=" + (pass ? 1 : 0));
                return pass;
            }
            catch (Exception ex)
            {
                report.AppendLine("exception=" + ex);
                report.AppendLine("success=0");
                File.WriteAllText(reportPath, report.ToString());
                Debug.LogError("VLN_MEADOW_DYNAMIC_MATERIAL_FIX_FAILED " + ex);
                if (batchMode)
                {
                    return false;
                }
                throw;
            }
        }

        static void FixMeadowPrefabMaterials(MeadowMaterialFallbacks fallbacks, StringBuilder report, MaterialFixStats stats)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { MeadowAssetRootPath });
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                stats.PrefabsScanned++;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    Renderer[] renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
                    int missingBefore = CountMissingMaterialSlots(renderers);
                    if (missingBefore == 0)
                    {
                        continue;
                    }

                    stats.PrefabMissingSlotsBefore += missingBefore;
                    int fixedBefore = stats.MaterialSlotsFixed;
                    foreach (var renderer in renderers)
                    {
                        FixRendererMaterials(renderer, prefabPath, fallbacks, report, stats);
                    }

                    int fixedInPrefab = stats.MaterialSlotsFixed - fixedBefore;
                    int missingAfter = CountMissingMaterialSlots(renderers);
                    if (fixedInPrefab > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        stats.PrefabsTouched++;
                        report.AppendLine("PREFAB_FIXED path=" + prefabPath + " before=" + missingBefore.ToString(CultureInfo.InvariantCulture) + " fixed=" + fixedInPrefab.ToString(CultureInfo.InvariantCulture) + " after=" + missingAfter.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (missingAfter > 0)
                    {
                        report.AppendLine("PREFAB_UNRESOLVED path=" + prefabPath + " before=" + missingBefore.ToString(CultureInfo.InvariantCulture) + " after=" + missingAfter.ToString(CultureInfo.InvariantCulture));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        static void FixRendererMaterials(Renderer renderer, string context, MeadowMaterialFallbacks fallbacks, StringBuilder report, MaterialFixStats stats)
        {
            if (renderer == null)
            {
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return;
            }

            bool hasMissing = false;
            Material firstExisting = null;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                {
                    hasMissing = true;
                }
                else if (firstExisting == null)
                {
                    firstExisting = materials[i];
                }
            }

            if (!hasMissing)
            {
                return;
            }

            string rendererPath = GetTransformPath(renderer.transform);
            string key = (context + "/" + rendererPath + "/" + renderer.GetType().Name).ToLowerInvariant();
            Material expectedDynamicMaterial = ResolveMeadowFallbackMaterial(key, fallbacks);
            bool normalizeDynamicParticle = renderer is ParticleSystemRenderer && expectedDynamicMaterial != null;
            bool hasUnexpectedDynamicMaterial = false;
            if (normalizeDynamicParticle)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i] != expectedDynamicMaterial)
                    {
                        hasUnexpectedDynamicMaterial = true;
                        break;
                    }
                }
            }

            if (!hasMissing && !hasUnexpectedDynamicMaterial)
            {
                return;
            }

            Material fallback = expectedDynamicMaterial != null ? expectedDynamicMaterial : firstExisting;
            if (fallback == null)
            {
                stats.UnresolvedRenderers++;
                report.AppendLine("UNRESOLVED_RENDERER context=" + context + " renderer=" + rendererPath + " type=" + renderer.GetType().Name + " slots=" + materials.Length.ToString(CultureInfo.InvariantCulture));
                return;
            }

            int fixedSlots = 0;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || (normalizeDynamicParticle && materials[i] != expectedDynamicMaterial))
                {
                    materials[i] = fallback;
                    fixedSlots++;
                }
            }

            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            stats.RenderersTouched++;
            stats.MaterialSlotsFixed += fixedSlots;
            report.AppendLine("RENDERER_FIXED context=" + context + " renderer=" + rendererPath + " type=" + renderer.GetType().Name + " fixed_slots=" + fixedSlots.ToString(CultureInfo.InvariantCulture) + " fallback=" + fallback.name);
        }

        static MeadowMaterialFallbacks LoadMeadowMaterialFallbacks()
        {
            return new MeadowMaterialFallbacks
            {
                Particle01 = AssetDatabase.LoadAssetAtPath<Material>(MeadowParticle01MaterialPath),
                Particle02 = AssetDatabase.LoadAssetAtPath<Material>(MeadowParticle02MaterialPath),
                Insect = AssetDatabase.LoadAssetAtPath<Material>(MeadowInsect01MaterialPath),
                Leaf = AssetDatabase.LoadAssetAtPath<Material>(MeadowLeafMaterialPath),
            };
        }

        static Material ResolveMeadowFallbackMaterial(string key, MeadowMaterialFallbacks fallbacks)
        {
            if (key.Contains("leaf") || key.Contains("poplar"))
            {
                return fallbacks.Leaf;
            }
            if (key.Contains("meadow_dust 2") || key.Contains("meadow dust 2") || key.Contains("prefab_meadow_dust 2") || key.Contains("particle_02") || key.Contains("meadow_particle_02"))
            {
                return fallbacks.Particle02;
            }
            if (key.Contains("meadow_dust") || key.Contains("meadow dust") || key.Contains("prefab_meadow_dust") || key.Contains("particle_01") || key.Contains("meadow_particle_01"))
            {
                return fallbacks.Particle01;
            }
            if (key.Contains("bee") || key.Contains("butter") || key.Contains("dumbledore"))
            {
                return fallbacks.Insect;
            }
            return null;
        }

        static Renderer[] GetActiveSceneRenderers(bool includeInactive)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var renderers = new List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
            {
                renderers.AddRange(root.GetComponentsInChildren<Renderer>(includeInactive));
            }
            return renderers.ToArray();
        }

        static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            var names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        static void EnsureSourceScene(WorldSpec spec)
        {
            if (!File.Exists(ProjectRelativeToAbsolute(spec.SourceScenePath)))
            {
                throw new FileNotFoundException("Missing imported world source scene", spec.SourceScenePath);
            }
        }

        static void EnsureDevelopmentMarker(WorldSpec spec)
        {
            if (GameObject.Find(spec.MarkerName) != null)
            {
                return;
            }

            var marker = new GameObject(spec.MarkerName);
            marker.transform.position = Vector3.zero;
            marker.transform.rotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
        }

        static void ScheduleMeadowSceneViewIconCleanup()
        {
            s_MeadowSceneViewCleanupAttempts = 0;
            EditorApplication.update -= MeadowSceneViewIconCleanupTick;
            EditorApplication.update += MeadowSceneViewIconCleanupTick;
        }

        static void MeadowSceneViewIconCleanupTick()
        {
            s_MeadowSceneViewCleanupAttempts++;
            int sceneViewsUpdated = ApplyMeadowSceneViewIconCleanup(false);
            if (sceneViewsUpdated > 0 || s_MeadowSceneViewCleanupAttempts >= 120)
            {
                EditorApplication.update -= MeadowSceneViewIconCleanupTick;
            }
        }

        static int ApplyMeadowSceneViewIconCleanup(bool writeReport)
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string reportPath = Path.Combine(logRoot, "vln_meadow_scene_view_icon_cleanup.txt");
            var report = new StringBuilder();
            report.AppendLine("started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            report.AppendLine("target_scene=" + MeadowCandidateScenePath);

            int annotationIconsDisabled = DisableEditorAnnotationIcons(report,
                "ParticleSystem",
                "WindZone",
                "ReflectionProbe",
                "LightProbeGroup",
                "LightProbeProxyVolume",
                "AudioSource",
                "Halo",
                "LensFlare",
                "FlareLayer");
            int sceneViewsUpdated = 0;
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView == null)
                {
                    continue;
                }

                sceneView.drawGizmos = false;
                sceneView.Repaint();
                sceneViewsUpdated++;
            }

            report.AppendLine("annotation_icons_disabled=" + annotationIconsDisabled.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("scene_views_gizmos_disabled=" + sceneViewsUpdated.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("note=These are Unity editor Scene View icons/gizmos, not runtime meshes or material-rendered pixels.");
            report.AppendLine("finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            report.AppendLine("success=1");

            if (writeReport || sceneViewsUpdated > 0 || annotationIconsDisabled > 0)
            {
                File.WriteAllText(reportPath, report.ToString());
            }

            Debug.Log("VLN_MEADOW_SCENE_VIEW_ICON_CLEANUP icons=" + annotationIconsDisabled.ToString(CultureInfo.InvariantCulture) + " sceneViews=" + sceneViewsUpdated.ToString(CultureInfo.InvariantCulture));
            return sceneViewsUpdated;
        }

        static int DisableEditorAnnotationIcons(StringBuilder report, params string[] scriptClassNames)
        {
            Type annotationUtility = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
            if (annotationUtility == null)
            {
                report.AppendLine("annotation_utility_missing=1");
                return 0;
            }

            BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            MethodInfo getAnnotations = annotationUtility.GetMethod("GetAnnotations", flags);
            MethodInfo setIconEnabled = annotationUtility.GetMethod("SetIconEnabled", flags);
            if (getAnnotations == null || setIconEnabled == null)
            {
                report.AppendLine("annotation_methods_missing=1");
                return 0;
            }

            var annotations = getAnnotations.Invoke(null, null) as Array;
            if (annotations == null)
            {
                report.AppendLine("annotation_list_missing=1");
                return 0;
            }

            int disabled = 0;
            foreach (object annotation in annotations)
            {
                if (annotation == null)
                {
                    continue;
                }

                string scriptClass = Convert.ToString(GetAnnotationMember(annotation, "scriptClass"), CultureInfo.InvariantCulture) ?? string.Empty;
                int classId = Convert.ToInt32(GetAnnotationMember(annotation, "classID"), CultureInfo.InvariantCulture);
                if (!MeadowEditorIconClassIds.Contains(classId) && !MatchesAny(scriptClass, scriptClassNames))
                {
                    continue;
                }

                object enabledValue = setIconEnabled.GetParameters()[2].ParameterType == typeof(bool) ? (object)false : 0;
                setIconEnabled.Invoke(null, new object[] { classId, scriptClass, enabledValue });
                report.AppendLine("ICON_DISABLED classID=" + classId.ToString(CultureInfo.InvariantCulture) + " scriptClass=" + scriptClass);
                disabled++;
            }

            return disabled;
        }

        static object GetAnnotationMember(object annotation, string memberName)
        {
            Type type = annotation.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(annotation);
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null ? property.GetValue(annotation, null) : null;
        }

        static bool MatchesAny(string value, params string[] candidates)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (string candidate in candidates)
            {
                if (value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        static Bounds CalculateSceneBounds(Terrain[] terrains, Renderer[] renderers)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            foreach (var terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }
                var terrainBounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
                if (!hasBounds)
                {
                    bounds = terrainBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(terrainBounds);
                }
            }

            if (hasBounds)
            {
                return bounds;
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null || ShouldIgnoreForBounds(renderer))
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return hasBounds ? bounds : new Bounds(Vector3.zero, new Vector3(120f, 80f, 120f));
        }

        static bool ShouldIgnoreForBounds(Renderer renderer)
        {
            string name = renderer.gameObject.name.ToLowerInvariant();
            return name.Contains("sky") || name.Contains("cloud") || name.Contains("background");
        }

        static int CountMissingMaterialSlots(Renderer[] renderers)
        {
            int count = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
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
                if (renderer == null)
                {
                    continue;
                }
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

        static void SaveExistingCameraView(string path, Camera[] cameras, Bounds bounds)
        {
            Camera camera = null;
            foreach (var candidate in cameras)
            {
                if (candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy)
                {
                    camera = candidate;
                    break;
                }
            }

            if (camera == null)
            {
                SaveView(path,
                    bounds.center + new Vector3(bounds.extents.x * 0.28f, Mathf.Max(bounds.extents.y * 0.85f, 24f), -bounds.extents.z * 0.36f),
                    bounds.center,
                    38f,
                    Mathf.Max(bounds.size.magnitude * 2.4f, 900f),
                    false);
                return;
            }

            RenderCameraToPng(camera, path, 1280, 720);
        }

        static void SaveView(string path, Vector3 position, Vector3 target, float fov, float farClip, bool disableFogForScreenshot)
        {
            var cameraObject = new GameObject("VLN_ImportedWorld_ScreenshotCamera");
            bool previousFog = RenderSettings.fog;
            try
            {
                if (disableFogForScreenshot)
                {
                    RenderSettings.fog = false;
                }

                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = farClip;
                camera.fieldOfView = fov;
                cameraObject.transform.position = position;
                cameraObject.transform.LookAt(target);
                RenderCameraToPng(camera, path, 1280, 720);
            }
            finally
            {
                RenderSettings.fog = previousFog;
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static void RenderCameraToPng(Camera camera, string path, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
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

        static string FormatVector(Vector3 value)
        {
            return value.x.ToString("F3", CultureInfo.InvariantCulture) + "," +
                   value.y.ToString("F3", CultureInfo.InvariantCulture) + "," +
                   value.z.ToString("F3", CultureInfo.InvariantCulture);
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        sealed class MeadowMaterialFallbacks
        {
            public Material Particle01;
            public Material Particle02;
            public Material Insect;
            public Material Leaf;
        }

        sealed class MaterialFixStats
        {
            public int PrefabsScanned;
            public int PrefabsTouched;
            public int PrefabMissingSlotsBefore;
            public int SceneRenderersScanned;
            public int RenderersTouched;
            public int MaterialSlotsFixed;
            public int UnresolvedRenderers;
        }

        sealed class WorldSpec
        {
            public string StageName;
            public string LogPrefix;
            public string SourceScenePath;
            public string CandidateScenePath;
            public string MarkerName;
            public string ResultFileName;
            public string ScreenshotPrefix;
            public int MinRendererCount;
            public int MinTerrainCount;
            public bool CleanSceneViewEditorIconsOnOpen;

            public static WorldSpec Meadow()
            {
                return new WorldSpec
                {
                    StageName = "meadow_dynamic_nature_world_candidate",
                    LogPrefix = "VLN_MEADOW_DYNAMIC_NATURE_WORLD",
                    SourceScenePath = MeadowSourceScenePath,
                    CandidateScenePath = MeadowCandidateScenePath,
                    MarkerName = "VLN_MeadowDynamicNature_DevelopmentRoot",
                    ResultFileName = "vln_meadow_dynamic_nature_world_candidate_result.txt",
                    ScreenshotPrefix = "vln_meadow_dynamic_nature_world_candidate",
                    MinRendererCount = 100,
                    MinTerrainCount = 1,
                    CleanSceneViewEditorIconsOnOpen = true,
                };
            }

            public static WorldSpec ForestLake()
            {
                return new WorldSpec
                {
                    StageName = "forest_lake_world_candidate",
                    LogPrefix = "VLN_FOREST_LAKE_WORLD",
                    SourceScenePath = ForestLakeSourceScenePath,
                    CandidateScenePath = ForestLakeCandidateScenePath,
                    MarkerName = "VLN_ForestLake_DevelopmentRoot",
                    ResultFileName = "vln_forest_lake_world_candidate_result.txt",
                    ScreenshotPrefix = "vln_forest_lake_world_candidate",
                    MinRendererCount = 50,
                    MinTerrainCount = 1,
                };
            }
        }
    }
}
