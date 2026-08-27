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

namespace VLN.Editor
{
    public static class VlnPureNatureMesaOasisStitchBuilder
    {
        public const string MesaCandidateScenePath = "Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity";
        public const string OasisDayScenePath = "Assets/BK/PureNature_Oasis/Scenes/Scene_Oasis_Day.unity";
        public const string StitchedScenePath = "Assets/VLN/Scenes/VLNMesaOasisStitchedRouteCandidate.unity";

        const string OasisRootName = "VLN_Oasis_StitchedRoot";
        const string SeamRouteRootName = "VLN_MesaOasis_StitchRouteCandidate";
        const float TerrainSeamOverlapMeters = 90.0f;
        const float StitchMaxAcceptableProfileDeltaMeters = 18.0f;
        const float OasisGateHalfWidthMeters = 250.0f;
        const float OasisGateDepthMeters = 520.0f;
        const float OasisGateOutsidePaddingMeters = 90.0f;
        const float MountainGateCorridorHalfWidthMeters = 180f;
        const float MountainGateEndpointPadMeters = 95f;
        const string ForceRebuildEnvironmentVariable = "VLN_FORCE_REBUILD_STITCHED_WORLD";

        [MenuItem("VLN/Mesa Oasis/Build Stitched Route Candidate Scene")]
        public static void BuildStitchedFromMenu()
        {
            if (!ConfirmOrRejectManualSaveOverwrite("手动菜单重建 Mesa+Oasis 拼接场景", interactive: true))
            {
                return;
            }

            var result = BuildStitchedScene();
            Debug.Log("VLN_MESA_OASIS_STITCHED_SCENE_BUILT " + StitchedScenePath + " offset=" + FormatVector(result.OasisOffset));
        }

        public static void OpenStitchedForManualReview()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(StitchedScenePath)))
            {
                BuildStitchedScene();
            }
            else
            {
                EditorSceneManager.OpenScene(StitchedScenePath, OpenSceneMode.Single);
            }

            if (VlnWorldModelManualSaveWindow.HasManualSavedStitchedWorld())
            {
                Debug.Log("VLN_WORLD_MODEL_MANUAL_SAVE_ACTIVE opening_saved_scene_without_rebuild scene=" + StitchedScenePath + " manifest=" + VlnWorldModelManualSaveWindow.ManualSaveManifestPath);
            }
            Debug.Log("VLN_MESA_OASIS_STITCHED_OPENED_FOR_MANUAL_REVIEW " + StitchedScenePath);
        }

        public static void RunBuildAndSmokeTest()
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_result.txt");

            try
            {
                bool manualSavedWorldActive = VlnWorldModelManualSaveWindow.HasManualSavedStitchedWorld();
                bool skippedRebuildForManualSave = manualSavedWorldActive && !ForceRebuildRequested();
                StitchResult result = null;

                if (skippedRebuildForManualSave)
                {
                    EditorSceneManager.OpenScene(StitchedScenePath, OpenSceneMode.Single);
                    Debug.LogWarning("VLN_MESA_OASIS_STITCHED_REBUILD_SKIPPED_MANUAL_SAVE_PROTECTED scene=" + StitchedScenePath + " manifest=" + VlnWorldModelManualSaveWindow.ManualSaveManifestPath);
                }
                else
                {
                    result = BuildStitchedScene();
                }
                EditorSceneManager.OpenScene(StitchedScenePath, OpenSceneMode.Single);

                var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
                var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                var colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
                int missingMaterialSlots = CountMissingMaterialSlots(renderers);
                int internalErrorMaterials = CountInternalErrorMaterials(renderers);
                var bounds = CalculateSceneBounds(terrains, renderers);

                SaveView(logRoot, "overview", bounds.center + new Vector3(bounds.extents.x * 0.36f, Mathf.Max(bounds.extents.y * 1.9f, 260f), -bounds.extents.z * 0.55f), bounds.center, 44f, bounds.size.magnitude * 2.6f, false);
                if (result != null)
                {
                    SaveView(logRoot, "seam", result.SeamCameraPosition, result.SeamTarget, 36f, 2400f, false);
                    SaveView(logRoot, "mesa_side", result.MesaCameraPosition, result.MesaTarget, 38f, 2200f, false);
                    SaveView(logRoot, "oasis_side", result.OasisCameraPosition, result.OasisTarget, 38f, 2200f, false);
                }
                else
                {
                    SaveView(logRoot, "seam", bounds.center + new Vector3(bounds.extents.x * 0.15f, Mathf.Max(bounds.extents.y * 1.15f, 180f), -bounds.extents.z * 0.18f), bounds.center, 38f, bounds.size.magnitude * 2.0f, false);
                    SaveView(logRoot, "mesa_side", bounds.center + new Vector3(bounds.extents.x * 0.42f, Mathf.Max(bounds.extents.y * 1.05f, 180f), -bounds.extents.z * 0.42f), bounds.center, 40f, bounds.size.magnitude * 2.0f, false);
                    SaveView(logRoot, "oasis_side", bounds.center + new Vector3(-bounds.extents.x * 0.42f, Mathf.Max(bounds.extents.y * 1.05f, 180f), bounds.extents.z * 0.42f), bounds.center, 40f, bounds.size.magnitude * 2.0f, false);
                }
                SaveView(logRoot, "top_layout", bounds.center + new Vector3(0f, Mathf.Max(bounds.size.x, bounds.size.z) * 1.05f, 0f), bounds.center, 52f, bounds.size.magnitude * 3f, true);

                bool pass = terrains.Length >= 2 && GameObject.Find(OasisRootName) != null &&
                            missingMaterialSlots == 0 && internalErrorMaterials == 0;
                if (result != null)
                {
                    pass = pass && result.OasisMovedRootCount > 0 &&
                           result.SeamProfileMeanDeltaMeters <= 5.0f &&
                           result.SeamProfileMaxDeltaMeters <= StitchMaxAcceptableProfileDeltaMeters &&
                           result.OasisGateRemovedObstacleCount > 0;
                }

                File.WriteAllText(resultPath,
                    "started=" + DateTime.UtcNow.ToString("O") + "\n" +
                    "stage=pure_nature_mesa_oasis_stitched_route_candidate\n" +
                    "scene_path=" + StitchedScenePath + "\n" +
                    "mesa_source_scene=" + MesaCandidateScenePath + "\n" +
                    "oasis_source_scene=" + OasisDayScenePath + "\n" +
                    "manual_saved_world_active=" + (manualSavedWorldActive ? "1" : "0") + "\n" +
                    "rebuild_skipped_for_manual_save=" + (skippedRebuildForManualSave ? "1" : "0") + "\n" +
                    "manual_save_manifest=" + VlnWorldModelManualSaveWindow.ManualSaveManifestPath + "\n" +
                    "selected_mesa_edge=" + ResultText(result, r => r.MesaEdge.Label) + "\n" +
                    "selected_oasis_edge=" + ResultText(result, r => r.OasisEdge.Label) + "\n" +
                    "oasis_offset=" + ResultText(result, r => FormatVector(r.OasisOffset)) + "\n" +
                    "seam_height_delta_m=" + ResultText(result, r => r.SeamHeightDeltaMeters.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                    "seam_profile_mean_delta_m=" + ResultText(result, r => r.SeamProfileMeanDeltaMeters.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                    "seam_profile_max_delta_m=" + ResultText(result, r => r.SeamProfileMaxDeltaMeters.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                    "seam_profile_sample_count=" + ResultText(result, r => r.SeamProfileSampleCount.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "seam_pair_score=" + ResultText(result, r => r.PairScore.ToString("F3", CultureInfo.InvariantCulture)) + "\n" +
                    "terrain_count=" + terrains.Length + "\n" +
                    "camera_count=" + cameras.Length + "\n" +
                    "renderer_count=" + renderers.Length + "\n" +
                    "collider_count=" + colliders.Length + "\n" +
                    "oasis_moved_root_count=" + ResultText(result, r => r.OasisMovedRootCount.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "oasis_removed_camera_light_root_count=" + ResultText(result, r => r.OasisRemovedCameraLightRootCount.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "oasis_gate_removed_obstacle_count=" + ResultText(result, r => r.OasisGateRemovedObstacleCount.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "mountain_gate_removed_renderer_count=" + ResultText(result, r => r.MountainGateRemovedRendererCount.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "mountain_gate_removed_collider_count=" + ResultText(result, r => r.MountainGateRemovedColliderCount.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "stitch_route_waypoint_count=" + ResultText(result, r => r.RoutePoints.Count.ToString(CultureInfo.InvariantCulture)) + "\n" +
                    "missing_material_slots=" + missingMaterialSlots + "\n" +
                    "internal_error_materials=" + internalErrorMaterials + "\n" +
                    "scene_bounds_center=" + FormatVector(bounds.center) + "\n" +
                    "scene_bounds_size=" + FormatVector(bounds.size) + "\n" +
                    "route_config=" + ProjectRootPath("config/mesa_oasis_stitched_route_candidate.json") + "\n" +
                    "overview_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_overview.png") + "\n" +
                    "seam_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_seam.png") + "\n" +
                    "mesa_side_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_mesa_side.png") + "\n" +
                    "oasis_side_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_oasis_side.png") + "\n" +
                    "top_layout_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_top_layout.png") + "\n" +
                    "finished=" + DateTime.UtcNow.ToString("O") + "\n" +
                    "success=" + (pass ? "1" : "0") + "\n");

                Debug.Log("VLN_MESA_OASIS_STITCHED_RESULT " + resultPath);
                EditorApplication.Exit(pass ? 0 : 1);
            }
            catch (Exception ex)
            {
                File.WriteAllText(resultPath, "success=0\nexception=" + ex + "\n");
                Debug.LogError("VLN_MESA_OASIS_STITCHED_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        static bool ConfirmOrRejectManualSaveOverwrite(string action, bool interactive)
        {
            if (!VlnWorldModelManualSaveWindow.HasManualSavedStitchedWorld())
            {
                return true;
            }

            if (ForceRebuildRequested())
            {
                VlnWorldModelManualSaveWindow.ClearManualSaveManifestForForcedRebuild(action + " with " + ForceRebuildEnvironmentVariable);
                return true;
            }

            string message = "当前 Mesa+Oasis 主世界已经通过“VLN/更改世界模型/保存本次世界”手工保存。\n\n" +
                             "继续重建会覆盖你在 Unity 里拖动、删除、添加后的世界模型。\n\n" +
                             "场景：" + StitchedScenePath + "\n" +
                             "保存记录：" + VlnWorldModelManualSaveWindow.ManualSaveManifestRelativePath;
            if (interactive)
            {
                bool proceed = EditorUtility.DisplayDialog("手工保存世界受保护", message, "强制重建覆盖", "取消");
                if (proceed)
                {
                    VlnWorldModelManualSaveWindow.ClearManualSaveManifestForForcedRebuild(action + " confirmed in Unity menu");
                    return true;
                }
            }

            Debug.LogWarning("VLN_MESA_OASIS_STITCHED_REBUILD_BLOCKED_MANUAL_SAVE_PROTECTED action=" + action + " manifest=" + VlnWorldModelManualSaveWindow.ManualSaveManifestPath);
            return false;
        }

        static bool ForceRebuildRequested()
        {
            string value = Environment.GetEnvironmentVariable(ForceRebuildEnvironmentVariable);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        static string ResultText(StitchResult result, Func<StitchResult, string> selector)
        {
            return result == null ? "manual_saved_scene_not_rebuilt" : selector(result);
        }

        static StitchResult BuildStitchedScene()
        {
            if (VlnWorldModelManualSaveWindow.HasManualSavedStitchedWorld())
            {
                if (!ForceRebuildRequested())
                {
                    throw new InvalidOperationException(
                        "当前 Mesa+Oasis 世界已经通过 VLN/更改世界模型/保存本次世界 手工保存。为避免覆盖用户修改，普通重建已被拒绝。若确实要覆盖，请先备份并设置环境变量 " + ForceRebuildEnvironmentVariable + "=1。");
                }

                VlnWorldModelManualSaveWindow.ClearManualSaveManifestForForcedRebuild("forced BuildStitchedScene");
            }

            EnsureRequiredAssets();
            if (!File.Exists(ProjectRelativeToAbsolute(MesaCandidateScenePath)))
            {
                VlnPureNatureMesaDesertRouteCandidateBuilder.BuildCandidateFromMenu();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(StitchedScenePath)) ?? string.Empty);
            var mainScene = EditorSceneManager.OpenScene(MesaCandidateScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(mainScene, StitchedScenePath);
            mainScene = EditorSceneManager.OpenScene(StitchedScenePath, OpenSceneMode.Single);

            RemoveExistingRoot(OasisRootName);
            RemoveExistingRoot(SeamRouteRootName);

            Terrain mesaTerrain = FindFirstTerrain(mainScene);
            if (mesaTerrain == null || mesaTerrain.terrainData == null)
            {
                throw new InvalidOperationException("Mesa stitched scene requires a Terrain.");
            }

            var oasisScene = EditorSceneManager.OpenScene(OasisDayScenePath, OpenSceneMode.Additive);
            Terrain oasisTerrain = FindFirstTerrain(oasisScene);
            if (oasisTerrain == null || oasisTerrain.terrainData == null)
            {
                throw new InvalidOperationException("Oasis day scene requires a Terrain.");
            }

            Physics.SyncTransforms();
            var pair = ChooseBestEdgePair(mesaTerrain, oasisTerrain);
            Vector3 offset = pair.OasisOffset;

            var oasisRoot = new GameObject(OasisRootName);
            SceneManager.MoveGameObjectToScene(oasisRoot, mainScene);
            var originalRoots = oasisScene.GetRootGameObjects();
            int moved = 0;
            int removed = 0;
            foreach (var root in originalRoots)
            {
                if (root == null)
                {
                    continue;
                }

                if (ShouldDropDuplicateOasisRoot(root))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    removed++;
                    continue;
                }

                SceneManager.MoveGameObjectToScene(root, mainScene);
                root.transform.SetParent(oasisRoot.transform, true);
                moved++;
            }
            oasisRoot.transform.position = offset;
            EditorSceneManager.CloseScene(oasisScene, true);
            Physics.SyncTransforms();
            int gateRemoved = OpenOasisMountainGate(oasisRoot, oasisTerrain, pair.OasisEdge);
            Physics.SyncTransforms();

            var result = new StitchResult
            {
                MesaEdge = pair.MesaEdge,
                OasisEdge = pair.OasisEdge,
                OasisOffset = offset,
                PairScore = pair.Score,
                OasisMovedRootCount = moved,
                OasisRemovedCameraLightRootCount = removed,
                OasisGateRemovedObstacleCount = gateRemoved,
                SeamHeightDeltaMeters = CalculateSeamHeightDelta(mesaTerrain, oasisTerrain, pair.MesaEdge, pair.OasisEdge),
                SeamProfileMeanDeltaMeters = pair.SeamProfileMeanDeltaMeters,
                SeamProfileMaxDeltaMeters = pair.SeamProfileMaxDeltaMeters,
                SeamProfileSampleCount = pair.SeamProfileSampleCount
            };
            CreateStitchRoute(mesaTerrain, oasisTerrain, pair.MesaEdge, pair.OasisEdge, result.RoutePoints);
            var gateCut = CutMountainGateAroundRoute(mainScene, result.RoutePoints);
            result.MountainGateRemovedRendererCount = gateCut.RemovedRendererCount;
            result.MountainGateRemovedColliderCount = gateCut.RemovedColliderCount;
            ConfigureReviewCamera(result);

            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene, StitchedScenePath);
            WriteRouteConfig(result);
            AssetDatabase.Refresh();
            return result;
        }

        static void EnsureRequiredAssets()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(OasisDayScenePath)))
            {
                throw new FileNotFoundException("Missing Oasis day scene", OasisDayScenePath);
            }
            if (!File.Exists(ProjectRelativeToAbsolute(MesaCandidateScenePath)) && !File.Exists(ProjectRelativeToAbsolute(VlnPureNatureMesaDesertRouteCandidateBuilder.DemoScenePath)))
            {
                throw new FileNotFoundException("Missing Mesa candidate and source scene", MesaCandidateScenePath);
            }
        }

        static EdgePair ChooseBestEdgePair(Terrain mesaTerrain, Terrain oasisTerrain)
        {
            var mesaEdges = AnalyzeTerrainEdgePortals(mesaTerrain, "mesa");
            var oasisEdges = AnalyzeTerrainEdgePortals(oasisTerrain, "oasis");
            EdgePair best = null;
            foreach (var mesa in mesaEdges)
            {
                foreach (var oasis in oasisEdges)
                {
                    if (!AreOpposite(mesa.Direction, oasis.Direction))
                    {
                        continue;
                    }

                    Vector3 offset = CalculateOasisOffset(mesaTerrain, oasisTerrain, mesa, oasis);
                    SeamProfile profile = EvaluateSeamProfile(mesaTerrain, oasisTerrain, mesa, oasis, offset);
                    if (profile.SampleCount < 18)
                    {
                        continue;
                    }

                    float hardDeltaPenalty = Mathf.Max(0f, profile.MaxAbsDeltaMeters - StitchMaxAcceptableProfileDeltaMeters) * 28f;
                    float score = mesa.Score + oasis.Score -
                                  profile.MeanAbsDeltaMeters * 16f -
                                  profile.MaxAbsDeltaMeters * 3.4f -
                                  hardDeltaPenalty -
                                  profile.MeanSlopeMismatch * 1.2f;
                    if (best == null || score > best.Score)
                    {
                        best = new EdgePair
                        {
                            MesaEdge = mesa,
                            OasisEdge = oasis,
                            OasisOffset = offset,
                            Score = score,
                            SeamProfileMeanDeltaMeters = profile.MeanAbsDeltaMeters,
                            SeamProfileMaxDeltaMeters = profile.MaxAbsDeltaMeters,
                            SeamProfileSampleCount = profile.SampleCount
                        };
                    }
                }
            }
            if (best == null)
            {
                throw new InvalidOperationException("Could not find opposite terrain edges for stitching.");
            }
            return best;
        }

        static List<EdgeInfo> AnalyzeTerrainEdgePortals(Terrain terrain, string prefix)
        {
            var output = new List<EdgeInfo>();
            output.AddRange(AnalyzeTerrainEdgePortals(terrain, EdgeDirection.West, prefix));
            output.AddRange(AnalyzeTerrainEdgePortals(terrain, EdgeDirection.East, prefix));
            output.AddRange(AnalyzeTerrainEdgePortals(terrain, EdgeDirection.South, prefix));
            output.AddRange(AnalyzeTerrainEdgePortals(terrain, EdgeDirection.North, prefix));
            return output;
        }

        static List<EdgeInfo> AnalyzeTerrainEdgePortals(Terrain terrain, EdgeDirection direction, string prefix)
        {
            var portals = new List<EdgeInfo>();
            for (int i = 0; i <= 14; i++)
            {
                float centerT = 0.15f + i * 0.05f;
                portals.Add(AnalyzeTerrainEdgePortal(terrain, direction, prefix, centerT));
            }
            return portals;
        }

        static EdgeInfo AnalyzeTerrainEdgePortal(Terrain terrain, EdgeDirection direction, string prefix, float centerT)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float lateralExtent = IsHorizontalEdge(direction) ? size.z : size.x;
            float centerLateral = IsHorizontalEdge(direction) ? origin.z + size.z * centerT : origin.x + size.x * centerT;
            float halfWindow = Mathf.Min(135f, lateralExtent * 0.105f);
            float[] lateralSamples = { -1.00f, -0.70f, -0.42f, -0.18f, 0.0f, 0.18f, 0.42f, 0.70f, 1.00f };
            float[] depthSamples = { 18f, 44f, 82f, 135f, 215f };
            var heights = new List<float>();
            var clearLaterals = new List<float>();
            float slopeSum = 0f;
            int clear = 0;
            int total = 0;
            int blocked = 0;

            foreach (float lateralFactor in lateralSamples)
            {
                foreach (float depth in depthSamples)
                {
                    total++;
                    float lateral = centerLateral + lateralFactor * halfWindow;
                    Vector2 point = PortalSamplePoint(origin, size, direction, lateral, depth);
                    if (!PointInsideTerrain(origin, size, point, 14f))
                    {
                        blocked++;
                        continue;
                    }

                    float nx = Mathf.Clamp01((point.x - origin.x) / size.x);
                    float nz = Mathf.Clamp01((point.y - origin.z) / size.z);
                    float slope = terrain.terrainData.GetSteepness(nx, nz);
                    float height = TerrainHeight(terrain, point);
                    bool openTerrain = TopRayHitsTerrain(terrain, point, height);
                    if (openTerrain && slope < 13f)
                    {
                        heights.Add(height);
                        clearLaterals.Add(lateral);
                        slopeSum += slope;
                        clear++;
                    }
                    else
                    {
                        blocked++;
                    }
                }
            }

            if (heights.Count == 0)
            {
                return new EdgeInfo
                {
                    Direction = direction,
                    Label = PortalLabel(prefix, direction, centerT),
                    MedianHeight = TerrainHeight(terrain, PortalSamplePoint(origin, size, direction, centerLateral, 44f)),
                    CenterLateral = centerLateral,
                    CenterLateral01 = Mathf.Clamp01((centerLateral - (IsHorizontalEdge(direction) ? origin.z : origin.x)) / (IsHorizontalEdge(direction) ? size.z : size.x)),
                    Score = -100f,
                    ClearRatio = 0f,
                    HeightStdDev = 999f,
                    AverageSlope = 999f,
                    CenterT = centerT,
                    BlockedRatio = 1f
                };
            }

            heights.Sort();
            clearLaterals.Sort();
            float medianHeight = heights[heights.Count / 2];
            float usableCenterLateral = clearLaterals[clearLaterals.Count / 2];
            float usableCenterLateral01 = Mathf.Clamp01((usableCenterLateral - (IsHorizontalEdge(direction) ? origin.z : origin.x)) / (IsHorizontalEdge(direction) ? size.z : size.x));
            float std = StdDev(heights, medianHeight);
            float avgSlope = slopeSum / Mathf.Max(clear, 1);
            float clearRatio = clear / (float)Mathf.Max(total, 1);
            float blockedRatio = blocked / (float)Mathf.Max(total, 1);
            float edgeCenterPenalty = Mathf.Abs(centerT - 0.5f) * 10f;
            float score = clearRatio * 170f - blockedRatio * 95f - std * 9.0f - avgSlope * 2.6f - edgeCenterPenalty;
            return new EdgeInfo
            {
                Direction = direction,
                Label = PortalLabel(prefix, direction, centerT),
                MedianHeight = medianHeight,
                CenterLateral = usableCenterLateral,
                CenterLateral01 = usableCenterLateral01,
                Score = score,
                ClearRatio = clearRatio,
                HeightStdDev = std,
                AverageSlope = avgSlope,
                CenterT = centerT,
                BlockedRatio = blockedRatio
            };
        }

        static Vector3 CalculateOasisOffset(Terrain mesaTerrain, Terrain oasisTerrain, EdgeInfo mesaEdge, EdgeInfo oasisEdge)
        {
            Vector3 mesaOrigin = mesaTerrain.transform.position;
            Vector3 mesaSize = mesaTerrain.terrainData.size;
            Vector3 oasisOrigin = oasisTerrain.transform.position;
            Vector3 oasisSize = oasisTerrain.terrainData.size;
            Vector3 offset = Vector3.zero;

            if (mesaEdge.Direction == EdgeDirection.East && oasisEdge.Direction == EdgeDirection.West)
            {
                offset.x = (mesaOrigin.x + mesaSize.x - TerrainSeamOverlapMeters) - oasisOrigin.x;
                offset.z = CurrentCenterLateral(mesaTerrain, mesaEdge) - CurrentCenterLateral(oasisTerrain, oasisEdge);
            }
            else if (mesaEdge.Direction == EdgeDirection.West && oasisEdge.Direction == EdgeDirection.East)
            {
                offset.x = mesaOrigin.x - (oasisOrigin.x + oasisSize.x - TerrainSeamOverlapMeters);
                offset.z = CurrentCenterLateral(mesaTerrain, mesaEdge) - CurrentCenterLateral(oasisTerrain, oasisEdge);
            }
            else if (mesaEdge.Direction == EdgeDirection.North && oasisEdge.Direction == EdgeDirection.South)
            {
                offset.z = (mesaOrigin.z + mesaSize.z - TerrainSeamOverlapMeters) - oasisOrigin.z;
                offset.x = CurrentCenterLateral(mesaTerrain, mesaEdge) - CurrentCenterLateral(oasisTerrain, oasisEdge);
            }
            else if (mesaEdge.Direction == EdgeDirection.South && oasisEdge.Direction == EdgeDirection.North)
            {
                offset.z = mesaOrigin.z - (oasisOrigin.z + oasisSize.z - TerrainSeamOverlapMeters);
                offset.x = CurrentCenterLateral(mesaTerrain, mesaEdge) - CurrentCenterLateral(oasisTerrain, oasisEdge);
            }
            else
            {
                throw new InvalidOperationException("Unsupported edge pair " + mesaEdge.Label + " / " + oasisEdge.Label);
            }
            var verticalOffsets = new List<float>();
            foreach (float lateralFactor in SeamLateralFactors())
            {
                foreach (float depth in SeamProfileDepths())
                {
                    if (TryGetPairedSeamSamples(mesaTerrain, oasisTerrain, mesaEdge, oasisEdge, lateralFactor, depth, out var mesaPoint, out var oasisPoint))
                    {
                        float mesaSlope = TerrainSlope(mesaTerrain, mesaPoint);
                        float oasisSlope = TerrainSlope(oasisTerrain, oasisPoint);
                        if (mesaSlope < 18f && oasisSlope < 18f)
                        {
                            verticalOffsets.Add(TerrainHeight(mesaTerrain, mesaPoint) - TerrainHeight(oasisTerrain, oasisPoint));
                        }
                    }
                }
            }

            if (verticalOffsets.Count > 0)
            {
                verticalOffsets.Sort();
                offset.y = verticalOffsets[verticalOffsets.Count / 2];
            }
            else
            {
                Vector2 mesaSeamPoint = PointInsideFromEdge(mesaTerrain, mesaEdge, 24f);
                Vector2 oasisSeamPoint = PointInsideFromEdge(oasisTerrain, oasisEdge, 24f);
                offset.y = TerrainHeight(mesaTerrain, mesaSeamPoint) - TerrainHeight(oasisTerrain, oasisSeamPoint);
            }
            return offset;
        }

        static SeamProfile EvaluateSeamProfile(Terrain mesaTerrain, Terrain oasisTerrain, EdgeInfo mesaEdge, EdgeInfo oasisEdge, Vector3 oasisOffset)
        {
            float sumAbsDelta = 0f;
            float maxAbsDelta = 0f;
            float sumSlopeMismatch = 0f;
            int sampleCount = 0;

            foreach (float lateralFactor in SeamLateralFactors())
            {
                foreach (float depth in SeamProfileDepths())
                {
                    if (!TryGetPairedSeamSamples(mesaTerrain, oasisTerrain, mesaEdge, oasisEdge, lateralFactor, depth, out var mesaPoint, out var oasisPoint))
                    {
                        continue;
                    }

                    float mesaHeight = TerrainHeight(mesaTerrain, mesaPoint);
                    float oasisHeight = TerrainHeight(oasisTerrain, oasisPoint) + oasisOffset.y;
                    float absDelta = Mathf.Abs(mesaHeight - oasisHeight);
                    sumAbsDelta += absDelta;
                    maxAbsDelta = Mathf.Max(maxAbsDelta, absDelta);
                    sumSlopeMismatch += Mathf.Abs(TerrainSlope(mesaTerrain, mesaPoint) - TerrainSlope(oasisTerrain, oasisPoint));
                    sampleCount++;
                }
            }

            if (sampleCount == 0)
            {
                return new SeamProfile
                {
                    MeanAbsDeltaMeters = 999f,
                    MaxAbsDeltaMeters = 999f,
                    MeanSlopeMismatch = 999f,
                    SampleCount = 0
                };
            }

            return new SeamProfile
            {
                MeanAbsDeltaMeters = sumAbsDelta / sampleCount,
                MaxAbsDeltaMeters = maxAbsDelta,
                MeanSlopeMismatch = sumSlopeMismatch / sampleCount,
                SampleCount = sampleCount
            };
        }

        static bool TryGetPairedSeamSamples(Terrain mesaTerrain, Terrain oasisTerrain, EdgeInfo mesaEdge, EdgeInfo oasisEdge, float lateralFactor, float depth, out Vector2 mesaPoint, out Vector2 oasisPoint)
        {
            float halfWindow = Mathf.Min(110f, Mathf.Min(LateralExtent(mesaTerrain, mesaEdge.Direction), LateralExtent(oasisTerrain, oasisEdge.Direction)) * 0.08f);
            float mesaLateral = CurrentCenterLateral(mesaTerrain, mesaEdge) + lateralFactor * halfWindow;
            float oasisLateral = CurrentCenterLateral(oasisTerrain, oasisEdge) + lateralFactor * halfWindow;
            mesaPoint = PointInsideFromEdgeAtLateral(mesaTerrain, mesaEdge, depth, mesaLateral);
            oasisPoint = PointInsideFromEdgeAtLateral(oasisTerrain, oasisEdge, depth, oasisLateral);
            return PointInsideTerrain(mesaTerrain.transform.position, mesaTerrain.terrainData.size, mesaPoint, 6f) &&
                   PointInsideTerrain(oasisTerrain.transform.position, oasisTerrain.terrainData.size, oasisPoint, 6f);
        }

        static float[] SeamLateralFactors()
        {
            return new[] { -1.0f, -0.72f, -0.44f, -0.20f, 0.0f, 0.20f, 0.44f, 0.72f, 1.0f };
        }

        static float[] SeamProfileDepths()
        {
            return new[] { 4f, 12f, 24f, 48f, 86f, 145f };
        }

        static void CreateStitchRoute(Terrain mesaTerrain, Terrain oasisTerrain, EdgeInfo mesaEdge, EdgeInfo oasisEdge, List<Vector3> routePoints)
        {
            var root = new GameObject(SeamRouteRootName);
            var points = new List<Vector3>();
            AddApproachPoints(mesaTerrain, mesaEdge, false, points);
            AddApproachPoints(oasisTerrain, oasisEdge, true, points);

            for (int i = 0; i < points.Count; i++)
            {
                var marker = new GameObject("VLN_MesaOasis_StitchWaypoint_" + i.ToString("00", CultureInfo.InvariantCulture));
                marker.transform.SetParent(root.transform, false);
                marker.transform.position = points[i] + Vector3.up * 0.20f;
                routePoints.Add(points[i]);
            }
        }

        static GateCutResult CutMountainGateAroundRoute(Scene scene, List<Vector3> routePoints)
        {
            if (routePoints == null || routePoints.Count < 2)
            {
                return new GateCutResult();
            }

            int removedRenderers = 0;
            int removedColliders = 0;
            var roots = scene.GetRootGameObjects();
            var renderers = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.gameObject == null)
                {
                    continue;
                }
                if (ShouldCutMountainGateRenderer(renderer, routePoints))
                {
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                    removedRenderers++;
                }
            }

            Physics.SyncTransforms();

            roots = scene.GetRootGameObjects();
            var colliders = roots.SelectMany(root => root.GetComponentsInChildren<Collider>(true)).ToArray();
            foreach (var collider in colliders)
            {
                if (collider == null || collider.gameObject == null)
                {
                    continue;
                }
                if (ShouldCutMountainGateCollider(collider, routePoints))
                {
                    UnityEngine.Object.DestroyImmediate(collider.gameObject);
                    removedColliders++;
                }
            }

            Physics.SyncTransforms();
            Debug.Log("VLN_MESA_OASIS_MOUNTAIN_GATE_CUT renderers=" + removedRenderers + " colliders=" + removedColliders);
            return new GateCutResult
            {
                RemovedRendererCount = removedRenderers,
                RemovedColliderCount = removedColliders
            };
        }

        static bool ShouldCutMountainGateRenderer(Renderer renderer, List<Vector3> routePoints)
        {
            if (renderer.GetComponentInParent<Terrain>() != null || renderer.GetComponentInParent<Camera>() != null || renderer.GetComponentInParent<Light>() != null)
            {
                return false;
            }
            if (renderer.gameObject.name.StartsWith("VLN_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            Bounds bounds = renderer.bounds;
            if (!BoundsIntersectsMountainGateCorridor(bounds, routePoints))
            {
                return false;
            }

            float horizontalMax = Mathf.Max(bounds.size.x, bounds.size.z);
            float horizontalArea = Mathf.Max(bounds.size.x, 0.1f) * Mathf.Max(bounds.size.z, 0.1f);
            bool largeEnough = horizontalMax >= 18f || bounds.size.y >= 9f || horizontalArea >= 260f;
            bool mountainLike = IsMountainLikeName(renderer.gameObject.name) || RendererUsesMountainLikeMaterial(renderer);
            return largeEnough && (mountainLike || horizontalMax >= 34f || bounds.size.y >= 15f);
        }

        static bool ShouldCutMountainGateCollider(Collider collider, List<Vector3> routePoints)
        {
            if (collider is TerrainCollider || collider.GetComponentInParent<Terrain>() != null)
            {
                return false;
            }
            if (collider.gameObject.name.StartsWith("VLN_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            Bounds bounds = collider.bounds;
            if (!BoundsIntersectsMountainGateCorridor(bounds, routePoints))
            {
                return false;
            }

            float horizontalMax = Mathf.Max(bounds.size.x, bounds.size.z);
            float horizontalArea = Mathf.Max(bounds.size.x, 0.1f) * Mathf.Max(bounds.size.z, 0.1f);
            bool largeEnough = horizontalMax >= 18f || bounds.size.y >= 8f || horizontalArea >= 240f;
            return largeEnough && (IsMountainLikeName(collider.gameObject.name) || horizontalMax >= 34f || bounds.size.y >= 14f);
        }

        static bool BoundsIntersectsMountainGateCorridor(Bounds bounds, List<Vector3> routePoints)
        {
            Vector2[] samples =
            {
                new Vector2(bounds.center.x, bounds.center.z),
                new Vector2(bounds.min.x, bounds.min.z),
                new Vector2(bounds.min.x, bounds.max.z),
                new Vector2(bounds.max.x, bounds.min.z),
                new Vector2(bounds.max.x, bounds.max.z)
            };
            float halfDiagonal = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
            float allowedDistance = MountainGateCorridorHalfWidthMeters + Mathf.Min(halfDiagonal, 65f);

            foreach (var sample in samples)
            {
                if (DistanceToRoutePolyline(sample, routePoints) <= allowedDistance + MountainGateEndpointPadMeters * 0.15f)
                {
                    return true;
                }
            }
            return false;
        }

        static float DistanceToRoutePolyline(Vector2 point, List<Vector3> routePoints)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < routePoints.Count - 1; i++)
            {
                Vector2 a = new Vector2(routePoints[i].x, routePoints[i].z);
                Vector2 b = new Vector2(routePoints[i + 1].x, routePoints[i + 1].z);
                best = Mathf.Min(best, DistancePointToSegment(point, a, b));
            }
            return best;
        }

        static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denom = Mathf.Max(ab.sqrMagnitude, 0.0001f);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denom);
            return Vector2.Distance(point, a + ab * t);
        }

        static bool RendererUsesMountainLikeMaterial(Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && IsMountainLikeName(material.name))
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsMountainLikeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            string lower = value.ToLowerInvariant();
            return lower.Contains("rock") || lower.Contains("stone") || lower.Contains("boulder") || lower.Contains("cliff") ||
                   lower.Contains("mountain") || lower.Contains("mesa") || lower.Contains("canyon") || lower.Contains("ridge") ||
                   lower.Contains("wall") || lower.Contains("pillar") || lower.Contains("monolith") || lower.Contains("sandstone");
        }

        static void AddApproachPoints(Terrain terrain, EdgeInfo edge, bool reverseFromEdge, List<Vector3> points, Vector3 offset = default)
        {
            float[] distances = reverseFromEdge
                ? new[] { TerrainSeamOverlapMeters + 24f, TerrainSeamOverlapMeters + 125f, TerrainSeamOverlapMeters + 270f }
                : new[] { 270f, 125f, 24f };
            foreach (float distance in distances)
            {
                Vector2 sample = PointInsideFromEdge(terrain, edge, distance);
                Vector3 world = new Vector3(sample.x, TerrainHeight(terrain, sample), sample.y) + offset;
                points.Add(world);
            }
        }

        static Vector2 PointInsideFromEdge(Terrain terrain, EdgeInfo edge, float distance)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float lateral = CurrentCenterLateral(terrain, edge);
            return PointInsideFromEdgeAtLateral(terrain, edge, distance, lateral);
        }

        static Vector2 PointInsideFromEdgeAtLateral(Terrain terrain, EdgeInfo edge, float distance, float lateral)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            switch (edge.Direction)
            {
                case EdgeDirection.West:
                    return new Vector2(origin.x + distance, lateral);
                case EdgeDirection.East:
                    return new Vector2(origin.x + size.x - distance, lateral);
                case EdgeDirection.South:
                    return new Vector2(lateral, origin.z + distance);
                case EdgeDirection.North:
                    return new Vector2(lateral, origin.z + size.z - distance);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static float LateralExtent(Terrain terrain, EdgeDirection direction)
        {
            Vector3 size = terrain.terrainData.size;
            return IsHorizontalEdge(direction) ? size.z : size.x;
        }

        static float CurrentCenterLateral(Terrain terrain, EdgeInfo edge)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (edge.Direction == EdgeDirection.East || edge.Direction == EdgeDirection.West)
            {
                return origin.z + size.z * edge.CenterLateral01;
            }
            return origin.x + size.x * edge.CenterLateral01;
        }

        static void ConfigureReviewCamera(StitchResult result)
        {
            Vector3 seamStart = result.RoutePoints.Count > 0 ? result.RoutePoints[Mathf.Max(0, result.RoutePoints.Count / 2 - 1)] : Vector3.zero;
            Vector3 seamEnd = result.RoutePoints.Count > 0 ? result.RoutePoints[Mathf.Min(result.RoutePoints.Count - 1, result.RoutePoints.Count / 2 + 1)] : Vector3.forward;
            result.SeamTarget = Vector3.Lerp(seamStart, seamEnd, 0.5f) + Vector3.up * 2f;
            Vector3 dir = seamEnd - seamStart;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = Vector3.forward;
            }
            dir.Normalize();
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            result.SeamCameraPosition = result.SeamTarget - dir * 82f + side * 54f + Vector3.up * 30f;

            result.MesaTarget = result.RoutePoints.Count > 1 ? result.RoutePoints[1] + Vector3.up * 2f : result.SeamTarget;
            result.MesaCameraPosition = result.MesaTarget - dir * 75f + side * 45f + Vector3.up * 28f;
            result.OasisTarget = result.RoutePoints.Count > 4 ? result.RoutePoints[4] + Vector3.up * 2f : result.SeamTarget;
            result.OasisCameraPosition = result.OasisTarget + dir * 75f - side * 45f + Vector3.up * 28f;
        }

        static void WriteRouteConfig(StitchResult result)
        {
            string configPath = ProjectRootPath("config/mesa_oasis_stitched_route_candidate.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"stage\": \"pure_nature_mesa_oasis_stitched_route_candidate\",");
            sb.AppendLine("  \"scene_path\": \"" + StitchedScenePath + "\",");
            sb.AppendLine("  \"mesa_source_scene\": \"" + MesaCandidateScenePath + "\",");
            sb.AppendLine("  \"oasis_source_scene\": \"" + OasisDayScenePath + "\",");
            sb.AppendLine("  \"selected_mesa_edge\": \"" + result.MesaEdge.Label + "\",");
            sb.AppendLine("  \"selected_oasis_edge\": \"" + result.OasisEdge.Label + "\",");
            sb.AppendLine("  \"oasis_offset\": { \"x\": " + result.OasisOffset.x.ToString("F3", CultureInfo.InvariantCulture) + ", \"y\": " + result.OasisOffset.y.ToString("F3", CultureInfo.InvariantCulture) + ", \"z\": " + result.OasisOffset.z.ToString("F3", CultureInfo.InvariantCulture) + " },");
            sb.AppendLine("  \"seam_height_delta_m\": " + result.SeamHeightDeltaMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"seam_profile_mean_delta_m\": " + result.SeamProfileMeanDeltaMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"seam_profile_max_delta_m\": " + result.SeamProfileMaxDeltaMeters.ToString("F3", CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"seam_profile_sample_count\": " + result.SeamProfileSampleCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"oasis_gate_removed_obstacle_count\": " + result.OasisGateRemovedObstacleCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"mountain_gate_removed_renderer_count\": " + result.MountainGateRemovedRendererCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"mountain_gate_removed_collider_count\": " + result.MountainGateRemovedColliderCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"notes\": \"Mesa and Oasis are stitched by full-scene terrain edges selected for open low-slope sand and compatible seam height profiles. Blocking mountain meshes/colliders near the route corridor are removed to open an entrance, without adding a hand-built sand transition strip.\",");
            sb.AppendLine("  \"waypoints\": [");
            for (int i = 0; i < result.RoutePoints.Count; i++)
            {
                Vector3 p = result.RoutePoints[i];
                sb.Append("    { \"index\": ").Append(i.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"x\": ").Append(p.x.ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(", \"y\": ").Append(p.y.ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(", \"z\": ").Append(p.z.ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(" }");
                if (i < result.RoutePoints.Count - 1)
                {
                    sb.Append(',');
                }
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(configPath, sb.ToString());
        }

        static bool ShouldDropDuplicateOasisRoot(GameObject root)
        {
            if (root.GetComponentInChildren<Camera>(true) != null)
            {
                return true;
            }
            var lights = root.GetComponentsInChildren<Light>(true);
            if (lights.Any(light => light.type == LightType.Directional))
            {
                return true;
            }
            return false;
        }

        static int OpenOasisMountainGate(GameObject oasisRoot, Terrain oasisTerrain, EdgeInfo oasisEdge)
        {
            var deletionRoots = new HashSet<GameObject>();
            foreach (var renderer in oasisRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.GetComponent<Terrain>() != null || renderer.GetComponentInParent<Terrain>() != null)
                {
                    continue;
                }

                if (IsGateObstacleCandidate(renderer.gameObject, renderer.bounds) && BoundsIntersectsOasisGate(oasisTerrain, oasisEdge, renderer.bounds))
                {
                    var root = FindGateDeletionRoot(renderer.gameObject, oasisRoot.transform);
                    if (root != null)
                    {
                        deletionRoots.Add(root);
                    }
                }
            }

            foreach (var collider in oasisRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider is TerrainCollider || collider.GetComponentInParent<Terrain>() != null)
                {
                    continue;
                }

                if (IsGateObstacleCandidate(collider.gameObject, collider.bounds) && BoundsIntersectsOasisGate(oasisTerrain, oasisEdge, collider.bounds))
                {
                    var root = FindGateDeletionRoot(collider.gameObject, oasisRoot.transform);
                    if (root != null)
                    {
                        deletionRoots.Add(root);
                    }
                }
            }

            int removed = 0;
            foreach (var root in deletionRoots.OrderBy(go => go.name).ToList())
            {
                if (root == null || root == oasisRoot || root.GetComponentInChildren<Terrain>(true) != null)
                {
                    continue;
                }
                Debug.Log("VLN_MESA_OASIS_GATE_REMOVE " + GetHierarchyPath(root));
                UnityEngine.Object.DestroyImmediate(root);
                removed++;
            }
            return removed;
        }

        static bool BoundsIntersectsOasisGate(Terrain terrain, EdgeInfo edge, Bounds bounds)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float centerLateral = CurrentCenterLateral(terrain, edge);
            float edgeLine;
            float minLongitudinal;
            float maxLongitudinal;
            float boundsLateralMin;
            float boundsLateralMax;
            float boundsLongitudinalMin;
            float boundsLongitudinalMax;

            switch (edge.Direction)
            {
                case EdgeDirection.North:
                    edgeLine = origin.z + size.z;
                    minLongitudinal = edgeLine - OasisGateDepthMeters;
                    maxLongitudinal = edgeLine + OasisGateOutsidePaddingMeters;
                    boundsLateralMin = bounds.min.x;
                    boundsLateralMax = bounds.max.x;
                    boundsLongitudinalMin = bounds.min.z;
                    boundsLongitudinalMax = bounds.max.z;
                    break;
                case EdgeDirection.South:
                    edgeLine = origin.z;
                    minLongitudinal = edgeLine - OasisGateOutsidePaddingMeters;
                    maxLongitudinal = edgeLine + OasisGateDepthMeters;
                    boundsLateralMin = bounds.min.x;
                    boundsLateralMax = bounds.max.x;
                    boundsLongitudinalMin = bounds.min.z;
                    boundsLongitudinalMax = bounds.max.z;
                    break;
                case EdgeDirection.East:
                    edgeLine = origin.x + size.x;
                    minLongitudinal = edgeLine - OasisGateDepthMeters;
                    maxLongitudinal = edgeLine + OasisGateOutsidePaddingMeters;
                    boundsLateralMin = bounds.min.z;
                    boundsLateralMax = bounds.max.z;
                    boundsLongitudinalMin = bounds.min.x;
                    boundsLongitudinalMax = bounds.max.x;
                    break;
                case EdgeDirection.West:
                    edgeLine = origin.x;
                    minLongitudinal = edgeLine - OasisGateOutsidePaddingMeters;
                    maxLongitudinal = edgeLine + OasisGateDepthMeters;
                    boundsLateralMin = bounds.min.z;
                    boundsLateralMax = bounds.max.z;
                    boundsLongitudinalMin = bounds.min.x;
                    boundsLongitudinalMax = bounds.max.x;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            bool lateralOverlap = boundsLateralMax >= centerLateral - OasisGateHalfWidthMeters &&
                                  boundsLateralMin <= centerLateral + OasisGateHalfWidthMeters;
            bool depthOverlap = boundsLongitudinalMax >= minLongitudinal && boundsLongitudinalMin <= maxLongitudinal;
            return lateralOverlap && depthOverlap;
        }

        static bool IsGateObstacleCandidate(GameObject go, Bounds bounds)
        {
            string path = GetHierarchyPath(go).ToLowerInvariant();
            bool namedRock = path.Contains("cliff") || path.Contains("highcliff") || path.Contains("boulder") ||
                             path.Contains("desertrock") || path.Contains("rocks") || path.Contains("plateau");
            bool largeEnough = Mathf.Max(bounds.size.x, bounds.size.z) >= 16f || bounds.size.y >= 8f;
            bool plantOrWater = path.Contains("tree") || path.Contains("palm") || path.Contains("plant") ||
                                path.Contains("water") || path.Contains("reed") || path.Contains("lilly");
            return namedRock && largeEnough && !plantOrWater;
        }

        static GameObject FindGateDeletionRoot(GameObject go, Transform oasisRoot)
        {
            Transform current = go.transform;
            while (current.parent != null && current.parent != oasisRoot)
            {
                string parentName = current.parent.name.ToLowerInvariant();
                if (parentName.StartsWith("zone", StringComparison.OrdinalIgnoreCase) || parentName.StartsWith("oasis", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                current = current.parent;
            }
            return current.gameObject;
        }

        static string GetHierarchyPath(GameObject go)
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

        static bool AreOpposite(EdgeDirection a, EdgeDirection b)
        {
            return (a == EdgeDirection.East && b == EdgeDirection.West) ||
                   (a == EdgeDirection.West && b == EdgeDirection.East) ||
                   (a == EdgeDirection.North && b == EdgeDirection.South) ||
                   (a == EdgeDirection.South && b == EdgeDirection.North);
        }

        static bool IsHorizontalEdge(EdgeDirection direction)
        {
            return direction == EdgeDirection.East || direction == EdgeDirection.West;
        }

        static Vector2 EdgeSamplePoint(Vector3 origin, Vector3 size, EdgeDirection direction, float t, float inset)
        {
            switch (direction)
            {
                case EdgeDirection.West:
                    return new Vector2(origin.x + inset, origin.z + size.z * t);
                case EdgeDirection.East:
                    return new Vector2(origin.x + size.x - inset, origin.z + size.z * t);
                case EdgeDirection.South:
                    return new Vector2(origin.x + size.x * t, origin.z + inset);
                case EdgeDirection.North:
                    return new Vector2(origin.x + size.x * t, origin.z + size.z - inset);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static Vector2 PortalSamplePoint(Vector3 origin, Vector3 size, EdgeDirection direction, float lateral, float depth)
        {
            switch (direction)
            {
                case EdgeDirection.West:
                    return new Vector2(origin.x + depth, lateral);
                case EdgeDirection.East:
                    return new Vector2(origin.x + size.x - depth, lateral);
                case EdgeDirection.South:
                    return new Vector2(lateral, origin.z + depth);
                case EdgeDirection.North:
                    return new Vector2(lateral, origin.z + size.z - depth);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static bool PointInsideTerrain(Vector3 origin, Vector3 size, Vector2 point, float margin)
        {
            return point.x >= origin.x + margin && point.x <= origin.x + size.x - margin &&
                   point.y >= origin.z + margin && point.y <= origin.z + size.z - margin;
        }

        static string PortalLabel(string prefix, EdgeDirection direction, float centerT)
        {
            return prefix + "_" + direction.ToString().ToLowerInvariant() + "_t" + centerT.ToString("0.00", CultureInfo.InvariantCulture);
        }

        static Terrain FindFirstTerrain(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var terrain = root.GetComponentInChildren<Terrain>(true);
                if (terrain != null)
                {
                    return terrain;
                }
            }
            return null;
        }

        static float CalculateSeamHeightDelta(Terrain mesaTerrain, Terrain oasisTerrain, EdgeInfo mesaEdge, EdgeInfo oasisEdge)
        {
            var deltas = new List<float>();
            float halfWindow = Mathf.Min(95f, Mathf.Min(LateralExtent(mesaTerrain, mesaEdge.Direction), LateralExtent(oasisTerrain, oasisEdge.Direction)) * 0.065f);
            foreach (float lateralFactor in SeamLateralFactors())
            {
                float lateral = CurrentCenterLateral(mesaTerrain, mesaEdge) + lateralFactor * halfWindow;
                Vector2 overlapPoint = PointInsideFromEdgeAtLateral(mesaTerrain, mesaEdge, TerrainSeamOverlapMeters * 0.5f, lateral);
                if (!PointInsideTerrain(mesaTerrain.transform.position, mesaTerrain.terrainData.size, overlapPoint, 4f) ||
                    !PointInsideTerrain(oasisTerrain.transform.position, oasisTerrain.terrainData.size, overlapPoint, 4f))
                {
                    continue;
                }
                deltas.Add(TerrainHeight(mesaTerrain, overlapPoint) - TerrainHeight(oasisTerrain, overlapPoint));
            }

            if (deltas.Count == 0)
            {
                Vector2 mesaPoint = PointInsideFromEdge(mesaTerrain, mesaEdge, TerrainSeamOverlapMeters * 0.5f);
                Vector2 oasisPoint = PointInsideFromEdge(oasisTerrain, oasisEdge, TerrainSeamOverlapMeters * 0.5f);
                return TerrainHeight(mesaTerrain, mesaPoint) - TerrainHeight(oasisTerrain, oasisPoint);
            }

            deltas.Sort();
            return deltas[deltas.Count / 2];
        }

        static float TerrainHeight(Terrain terrain, Vector2 worldXZ)
        {
            return terrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) + terrain.transform.position.y;
        }

        static float TerrainSlope(Terrain terrain, Vector2 worldXZ)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = Mathf.Clamp01((worldXZ.x - origin.x) / size.x);
            float nz = Mathf.Clamp01((worldXZ.y - origin.z) / size.z);
            return terrain.terrainData.GetSteepness(nx, nz);
        }

        static bool TopRayHitsTerrain(Terrain terrain, Vector2 worldXZ, float expectedTerrainHeight)
        {
            var origin = new Vector3(worldXZ.x, expectedTerrainHeight + 520f, worldXZ.y);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 900f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            if (hit.collider is TerrainCollider)
            {
                return Mathf.Abs(hit.point.y - expectedTerrainHeight) < 2.5f;
            }
            var hitTerrain = hit.collider.GetComponent<Terrain>();
            return hitTerrain == terrain && Mathf.Abs(hit.point.y - expectedTerrainHeight) < 2.5f;
        }

        static float StdDev(List<float> values, float center)
        {
            if (values.Count == 0)
            {
                return 0f;
            }
            float sum = 0f;
            foreach (float value in values)
            {
                float d = value - center;
                sum += d * d;
            }
            return Mathf.Sqrt(sum / values.Count);
        }

        static void RemoveExistingRoot(string rootName)
        {
            var existing = GameObject.Find(rootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        static Bounds CalculateSceneBounds(Terrain[] terrains, Renderer[] renderers)
        {
            bool initialized = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            foreach (var terrain in terrains)
            {
                var size = terrain.terrainData != null ? terrain.terrainData.size : Vector3.one;
                var tb = new Bounds(terrain.transform.position + size * 0.5f, size);
                if (!initialized)
                {
                    bounds = tb;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(tb);
                }
            }
            foreach (var renderer in renderers)
            {
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return initialized ? bounds : new Bounds(Vector3.zero, new Vector3(100f, 50f, 100f));
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
                    if (material != null && material.shader != null && material.shader.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        static void SaveView(string logRoot, string viewName, Vector3 position, Vector3 target, float fov, float farClip, bool disableFogForScreenshot)
        {
            string path = Path.Combine(logRoot, "vln_pure_nature_mesa_oasis_stitched_" + viewName + ".png");
            var cameraObject = new GameObject("MesaOasis_" + viewName + "_ScreenshotCamera");
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
                camera.farClipPlane = Mathf.Max(farClip, 1000f);
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
            return value.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                   value.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                   value.z.ToString("F2", CultureInfo.InvariantCulture);
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        static string ProjectRootPath(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            return Path.Combine(root, relativePath);
        }

        enum EdgeDirection
        {
            West,
            East,
            South,
            North
        }

        sealed class EdgeInfo
        {
            public EdgeDirection Direction;
            public string Label;
            public float MedianHeight;
            public float CenterLateral;
            public float CenterLateral01;
            public float Score;
            public float ClearRatio;
            public float HeightStdDev;
            public float AverageSlope;
            public float CenterT;
            public float BlockedRatio;
        }

        sealed class EdgePair
        {
            public EdgeInfo MesaEdge;
            public EdgeInfo OasisEdge;
            public Vector3 OasisOffset;
            public float Score;
            public float SeamProfileMeanDeltaMeters;
            public float SeamProfileMaxDeltaMeters;
            public int SeamProfileSampleCount;
        }

        sealed class SeamProfile
        {
            public float MeanAbsDeltaMeters;
            public float MaxAbsDeltaMeters;
            public float MeanSlopeMismatch;
            public int SampleCount;
        }

        sealed class GateCutResult
        {
            public int RemovedRendererCount;
            public int RemovedColliderCount;
        }

        sealed class StitchResult
        {
            public EdgeInfo MesaEdge;
            public EdgeInfo OasisEdge;
            public Vector3 OasisOffset;
            public float PairScore;
            public float SeamHeightDeltaMeters;
            public float SeamProfileMeanDeltaMeters;
            public float SeamProfileMaxDeltaMeters;
            public int SeamProfileSampleCount;
            public int OasisGateRemovedObstacleCount;
            public int MountainGateRemovedRendererCount;
            public int MountainGateRemovedColliderCount;
            public int OasisMovedRootCount;
            public int OasisRemovedCameraLightRootCount;
            public readonly List<Vector3> RoutePoints = new List<Vector3>();
            public Vector3 SeamTarget;
            public Vector3 SeamCameraPosition;
            public Vector3 MesaTarget;
            public Vector3 MesaCameraPosition;
            public Vector3 OasisTarget;
            public Vector3 OasisCameraPosition;
        }
    }
}
