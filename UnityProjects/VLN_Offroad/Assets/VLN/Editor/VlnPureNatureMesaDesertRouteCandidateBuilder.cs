using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnPureNatureMesaDesertRouteCandidateBuilder
    {
        public const string DemoScenePath = "Assets/BK/PureNature_MesaDesert/Scenes/Mesa_Demo.unity";
        public const string CandidateScenePath = "Assets/VLN/Scenes/VLNMesaDesertRouteCandidate.unity";

        const string EnhancementRootName = "VLN_Mesa_ObstacleEnhancement";
        const string RouteRootName = "VLN_Mesa_RouteCandidate";
        const int Seed = 2026082201;

        static readonly string[] BoulderPrefabs =
        {
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Boulders_0.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Boulders_1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Boulders_2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Boulders_3.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Boulders_4.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Boulders_5.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Strate_0.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Strate_2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/Strate_4.prefab"
        };

        static readonly string[] RubblePrefabs =
        {
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/RubbleDense_1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/RubbleDense_2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/RubbleDense_3.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/RubbleSparse_1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/RubbleSparse_2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Rocks/RubbleSparse_3.prefab"
        };

        static readonly string[] TreePrefabs =
        {
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Saguaro1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Saguaro2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Saguaro3.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Senita2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Senita5.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Grocactus2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Grocactus5.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Opuntia2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Trees/Opuntia4.prefab"
        };

        static readonly string[] PlantPrefabs =
        {
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/DryGrass1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/DryGrass2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/DryGrass3.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/DryGrass4.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Brittlebush_1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Brittlebush_2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Brittlebush_3.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Brittlebush_4.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Peanut1.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Peanut2.prefab",
            "Assets/BK/PureNature_MesaDesert/Prefabs/Plants/Peanut3.prefab"
        };

        static readonly Vector2[] RouteSeeds =
        {
            new Vector2(-735f, -720f),
            new Vector2(-535f, -760f),
            new Vector2(-315f, -720f),
            new Vector2(-90f, -655f),
            new Vector2(145f, -560f),
            new Vector2(360f, -420f),
            new Vector2(560f, -250f),
            new Vector2(710f, -35f),
            new Vector2(735f, 220f)
        };

        [MenuItem("VLN/Mesa Desert/Build Route Candidate Scene")]
        public static void BuildCandidateFromMenu()
        {
            var result = BuildCandidateScene();
            Debug.Log("VLN_MESA_ROUTE_CANDIDATE_BUILT " + CandidateScenePath + " rocks=" + result.RockCount + " vegetation=" + result.VegetationCount);
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

            Debug.Log("VLN_MESA_ROUTE_CANDIDATE_OPENED_FOR_MANUAL_REVIEW " + CandidateScenePath);
        }

        public static void RunBuildAndSmokeTest()
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_result.txt");

            try
            {
                var result = BuildCandidateScene();
                EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);

                var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
                var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                var colliders = UnityEngine.Object.FindObjectsOfType<Collider>();
                var bounds = CalculateSceneBounds(terrains, renderers);
                int missingMaterialSlots = CountMissingMaterialSlots(renderers);
                int internalErrorMaterials = CountInternalErrorMaterials(renderers);

                SaveView(logRoot, "overview", bounds.center + new Vector3(bounds.extents.x * 0.34f, Mathf.Max(bounds.extents.y * 1.85f, 210f), -bounds.extents.z * 0.52f), bounds.center, 44f, bounds.size.magnitude * 3f, false);
                SaveRouteView(logRoot, "route_start", result.RoutePoints, 0, 42f);
                SaveRouteView(logRoot, "route_middle", result.RoutePoints, Math.Max(1, result.RoutePoints.Count / 2 - 1), 38f);
                SaveObstacleView(logRoot, "obstacle_closeup", result, 32f);
                SaveView(logRoot, "top_layout", bounds.center + new Vector3(0f, Mathf.Max(bounds.size.x, bounds.size.z) * 1.08f, 0f), bounds.center, 52f, bounds.size.magnitude * 3f, true);

                bool pass = terrains.Length > 0 && File.Exists(ProjectRelativeToAbsolute(CandidateScenePath)) &&
                            result.RockCount >= 80 && result.TreeCount >= 20 && result.PlantCount >= 100 &&
                            result.RoutePoints.Count >= 6 && missingMaterialSlots == 0 && internalErrorMaterials == 0;

                File.WriteAllText(resultPath,
                    "started=" + DateTime.UtcNow.ToString("O") + "\n" +
                    "stage=pure_nature_mesa_desert_route_candidate\n" +
                    "scene_path=" + CandidateScenePath + "\n" +
                    "source_scene_path=" + DemoScenePath + "\n" +
                    "seed=" + Seed + "\n" +
                    "terrain_count=" + terrains.Length + "\n" +
                    "camera_count=" + cameras.Length + "\n" +
                    "renderer_count=" + renderers.Length + "\n" +
                    "collider_count=" + colliders.Length + "\n" +
                    "route_waypoint_count=" + result.RoutePoints.Count + "\n" +
                    "added_rock_count=" + result.RockCount + "\n" +
                    "added_rubble_count=" + result.RubbleCount + "\n" +
                    "added_tree_count=" + result.TreeCount + "\n" +
                    "added_plant_count=" + result.PlantCount + "\n" +
                    "added_vegetation_count=" + result.VegetationCount + "\n" +
                    "added_obstacle_collider_count=" + result.AddedColliderCount + "\n" +
                    "missing_material_slots=" + missingMaterialSlots + "\n" +
                    "internal_error_materials=" + internalErrorMaterials + "\n" +
                    "scene_bounds_center=" + FormatVector(bounds.center) + "\n" +
                    "scene_bounds_size=" + FormatVector(bounds.size) + "\n" +
                    "route_config=" + ProjectRootPath("config/mesa_desert_route_candidate.json") + "\n" +
                    "overview_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_overview.png") + "\n" +
                    "route_start_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_route_start.png") + "\n" +
                    "route_middle_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_route_middle.png") + "\n" +
                    "obstacle_closeup_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_obstacle_closeup.png") + "\n" +
                    "top_layout_screenshot=" + Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_top_layout.png") + "\n" +
                    "finished=" + DateTime.UtcNow.ToString("O") + "\n" +
                    "success=" + (pass ? "1" : "0") + "\n");

                Debug.Log("VLN_MESA_ROUTE_CANDIDATE_RESULT " + resultPath);
                EditorApplication.Exit(pass ? 0 : 1);
            }
            catch (Exception ex)
            {
                File.WriteAllText(resultPath, "success=0\nexception=" + ex + "\n");
                Debug.LogError("VLN_MESA_ROUTE_CANDIDATE_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        static BuildResult BuildCandidateScene()
        {
            EnsureRequiredAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectRelativeToAbsolute(CandidateScenePath)) ?? string.Empty);

            if (File.Exists(ProjectRelativeToAbsolute(CandidateScenePath)))
            {
                EditorSceneManager.OpenScene(CandidateScenePath, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), CandidateScenePath);
            }

            RemoveExistingRoot(EnhancementRootName);
            RemoveExistingRoot(RouteRootName);

            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                throw new InvalidOperationException("Mesa candidate scene requires one Terrain with TerrainData.");
            }
            Physics.SyncTransforms();

            var random = new System.Random(Seed);
            var result = new BuildResult();
            var routeRoot = new GameObject(RouteRootName);
            var routePoints = CreateRouteWaypoints(terrain, routeRoot.transform);
            result.RoutePoints.AddRange(routePoints);

            var enhancementRoot = new GameObject(EnhancementRootName);
            PlaceRouteEdgeObstacles(terrain, enhancementRoot.transform, routePoints, random, result);
            PlaceBroadNaturalScatter(terrain, enhancementRoot.transform, routePoints, random, result);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), CandidateScenePath);
            WriteRouteConfig(routePoints, result);
            AssetDatabase.Refresh();
            return result;
        }

        static void EnsureRequiredAssets()
        {
            if (!File.Exists(ProjectRelativeToAbsolute(DemoScenePath)))
            {
                throw new FileNotFoundException("Missing Mesa demo scene", DemoScenePath);
            }

            var all = new List<string>();
            all.AddRange(BoulderPrefabs);
            all.AddRange(RubblePrefabs);
            all.AddRange(TreePrefabs);
            all.AddRange(PlantPrefabs);
            foreach (string path in all)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    throw new FileNotFoundException("Missing Mesa prefab", path);
                }
            }
        }

        static List<Vector3> CreateRouteWaypoints(Terrain terrain, Transform parent)
        {
            var route = new List<Vector3>();
            for (int i = 0; i < RouteSeeds.Length; i++)
            {
                Vector3 point = FindUsablePointNear(terrain, RouteSeeds[i], 220f, 14f, 3f, 72f, true, i * 37);
                route.Add(point);

                var waypoint = new GameObject("VLN_Mesa_RouteWaypoint_" + i.ToString("00", CultureInfo.InvariantCulture));
                waypoint.transform.SetParent(parent, false);
                waypoint.transform.position = point + Vector3.up * 0.18f;
            }
            return route;
        }

        static void PlaceRouteEdgeObstacles(Terrain terrain, Transform parent, List<Vector3> route, System.Random random, BuildResult result)
        {
            for (int i = 0; i < route.Count - 1; i++)
            {
                Vector3 a = route[i];
                Vector3 b = route[i + 1];
                Vector3 dir = (b - a);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f)
                {
                    continue;
                }
                dir.Normalize();
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);

                int localClusters = 4;
                for (int c = 0; c < localClusters; c++)
                {
                    float t = ((float)c + 0.5f + RandomRange(random, -0.18f, 0.18f)) / localClusters;
                    Vector3 center = Vector3.Lerp(a, b, Mathf.Clamp01(t));
                    float signedSide = random.NextDouble() < 0.5 ? -1f : 1f;
                    float offset = RandomRange(random, 8f, 28f) * signedSide;
                    Vector2 seed = new Vector2(center.x + side.x * offset + RandomRange(random, -7f, 7f), center.z + side.z * offset + RandomRange(random, -7f, 7f));
                    Vector3 pos = FindUsablePointNear(terrain, seed, 58f, 18f, 3f, 82f, true, i * 113 + c * 19);

                    bool boulder = c % 2 == 0;
                    if (boulder)
                    {
                        PlacePrefab(parent, Pick(random, BoulderPrefabs), pos, RandomRange(random, 1.05f, 2.35f), "VLN_Mesa_RockObstacle_", true, random, result);
                    }
                    else
                    {
                        PlacePrefab(parent, Pick(random, RubblePrefabs), pos, RandomRange(random, 0.9f, 1.85f), "VLN_Mesa_RubbleObstacle_", true, random, result);
                    }

                    int plantAround = 3 + random.Next(3);
                    for (int p = 0; p < plantAround; p++)
                    {
                        Vector2 plantSeed = new Vector2(pos.x + RandomRange(random, -9f, 9f), pos.z + RandomRange(random, -9f, 9f));
                        Vector3 plantPos = FindUsablePointNear(terrain, plantSeed, 26f, 22f, 3f, 90f, true, i * 271 + c * 31 + p);
                        PlacePrefab(parent, Pick(random, PlantPrefabs), plantPos, RandomRange(random, 0.85f, 1.95f), "VLN_Mesa_GrassPatch_", false, random, result);
                    }
                }
            }
        }

        static void PlaceBroadNaturalScatter(Terrain terrain, Transform parent, List<Vector3> route, System.Random random, BuildResult result)
        {
            int rockTarget = 64;
            int treeTarget = 38;
            int plantTarget = 145;
            int rubbleTarget = 34;

            Scatter(terrain, parent, route, random, result, rockTarget, BoulderPrefabs, "VLN_Mesa_RockObstacle_", 1.2f, 3.1f, 24f, true, 70f);
            Scatter(terrain, parent, route, random, result, rubbleTarget, RubblePrefabs, "VLN_Mesa_RubbleObstacle_", 0.85f, 2.1f, 26f, true, 42f);
            Scatter(terrain, parent, route, random, result, treeTarget, TreePrefabs, "VLN_Mesa_TreeObstacle_", 0.72f, 1.35f, 24f, true, 58f);
            Scatter(terrain, parent, route, random, result, plantTarget, PlantPrefabs, "VLN_Mesa_GrassPatch_", 0.65f, 2.05f, 32f, false, 18f);
        }

        static void Scatter(Terrain terrain, Transform parent, List<Vector3> route, System.Random random, BuildResult result, int targetCount, string[] prefabs, string prefix, float minScale, float maxScale, float maxSlope, bool addProxyCollider, float minRouteDistance)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < targetCount && attempts < targetCount * 90)
            {
                attempts++;
                Vector2 seed = RandomTerrainPoint(terrain, random, 0.08f, 0.92f);
                Vector3 pos = FindUsablePointNear(terrain, seed, 60f, maxSlope, 3f, 96f, true, attempts);
                if (DistanceToRoute(pos, route) < minRouteDistance)
                {
                    continue;
                }

                PlacePrefab(parent, Pick(random, prefabs), pos, RandomRange(random, minScale, maxScale), prefix, addProxyCollider, random, result);
                placed++;
            }
        }

        static void PlacePrefab(Transform parent, string prefabPath, Vector3 groundPoint, float scale, string namePrefix, bool addProxyCollider, System.Random random, BuildResult result)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException("Missing prefab", prefabPath);
            }

            var obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj == null)
            {
                throw new InvalidOperationException("Could not instantiate prefab " + prefabPath);
            }

            obj.name = namePrefix + result.NextObjectIndex.ToString("000", CultureInfo.InvariantCulture) + "__" + Path.GetFileNameWithoutExtension(prefabPath);
            result.NextObjectIndex++;
            obj.transform.SetParent(parent, true);
            obj.transform.rotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);
            obj.transform.localScale = Vector3.one * scale;
            obj.transform.position = groundPoint;
            SnapRendererBottomToGround(obj, groundPoint.y);

            if (addProxyCollider && obj.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                AddBoxColliderProxy(obj);
                result.AddedColliderCount++;
            }

            if (namePrefix.Contains("Rock"))
            {
                result.RockCount++;
                result.RockPositions.Add(obj.transform.position);
            }
            else if (namePrefix.Contains("Rubble"))
            {
                result.RubbleCount++;
                result.RockCount++;
                result.RockPositions.Add(obj.transform.position);
            }
            else if (namePrefix.Contains("Tree"))
            {
                result.TreeCount++;
                result.TreePositions.Add(obj.transform.position);
            }
            else
            {
                result.PlantCount++;
                result.PlantPositions.Add(obj.transform.position);
            }
        }

        static void SnapRendererBottomToGround(GameObject obj, float groundY)
        {
            Bounds bounds;
            if (!TryGetRendererBounds(obj, out bounds))
            {
                return;
            }
            obj.transform.position += Vector3.up * (groundY - bounds.min.y);
        }

        static void AddBoxColliderProxy(GameObject obj)
        {
            Bounds bounds;
            if (!TryGetRendererBounds(obj, out bounds))
            {
                return;
            }

            var collider = obj.AddComponent<BoxCollider>();
            collider.center = obj.transform.InverseTransformPoint(bounds.center);
            Vector3 lossy = obj.transform.lossyScale;
            collider.size = new Vector3(
                SafeDivide(bounds.size.x, Mathf.Abs(lossy.x)),
                SafeDivide(bounds.size.y, Mathf.Abs(lossy.y)),
                SafeDivide(bounds.size.z, Mathf.Abs(lossy.z)));
        }

        static bool TryGetRendererBounds(GameObject obj, out Bounds bounds)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool initialized = false;
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
            return initialized;
        }

        static Vector3 FindUsablePointNear(Terrain terrain, Vector2 desired, float searchRadius, float maxSlopeDegrees, float minHeight, float maxHeight, bool requireTopTerrainHit, int salt)
        {
            if (IsUsableTerrainPoint(terrain, desired, maxSlopeDegrees, minHeight, maxHeight, requireTopTerrainHit))
            {
                return GroundPoint(terrain, desired);
            }

            Vector2 bestCandidate = desired;
            float bestScore = float.MaxValue;
            bool hasFallback = false;

            for (int ring = 1; ring <= 14; ring++)
            {
                int samples = 16 + ring * 6;
                float radius = searchRadius * ring / 14f;
                for (int i = 0; i < samples; i++)
                {
                    float angle = (i + salt * 0.17f) * Mathf.PI * 2f / samples;
                    var candidate = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (IsUsableTerrainPoint(terrain, candidate, maxSlopeDegrees, minHeight, maxHeight, requireTopTerrainHit))
                    {
                        return GroundPoint(terrain, candidate);
                    }

                    if (IsUsableTerrainPoint(terrain, candidate, maxSlopeDegrees * 1.45f, minHeight, maxHeight + 18f, false))
                    {
                        float score = Vector2.Distance(candidate, desired) + Mathf.Abs(TerrainHeight(terrain, candidate) - Mathf.Clamp(TerrainHeight(terrain, desired), minHeight, maxHeight));
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestCandidate = candidate;
                            hasFallback = true;
                        }
                    }
                }
            }

            return GroundPoint(terrain, hasFallback ? bestCandidate : desired);
        }

        static bool IsUsableTerrainPoint(Terrain terrain, Vector2 worldXZ, float maxSlopeDegrees, float minHeight, float maxHeight, bool requireTopTerrainHit)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = (worldXZ.x - origin.x) / size.x;
            float nz = (worldXZ.y - origin.z) / size.z;
            if (nx < 0.03f || nx > 0.97f || nz < 0.03f || nz > 0.97f)
            {
                return false;
            }

            float slope = terrain.terrainData.GetSteepness(nx, nz);
            if (slope > maxSlopeDegrees)
            {
                return false;
            }

            float height = TerrainHeight(terrain, worldXZ);
            if (height < minHeight || height > maxHeight)
            {
                return false;
            }

            return !requireTopTerrainHit || TopRayHitsTerrain(terrain, worldXZ, height);
        }

        static bool TopRayHitsTerrain(Terrain terrain, Vector2 worldXZ, float expectedTerrainHeight)
        {
            var origin = new Vector3(worldXZ.x, expectedTerrainHeight + 420f, worldXZ.y);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 700f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
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

        static float TerrainHeight(Terrain terrain, Vector2 worldXZ)
        {
            return terrain.SampleHeight(new Vector3(worldXZ.x, 0f, worldXZ.y)) + terrain.transform.position.y;
        }

        static Vector3 GroundPoint(Terrain terrain, Vector2 worldXZ)
        {
            float y = TerrainHeight(terrain, worldXZ);
            return new Vector3(worldXZ.x, y, worldXZ.y);
        }

        static Vector2 RandomTerrainPoint(Terrain terrain, System.Random random, float minNormalized, float maxNormalized)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = RandomRange(random, minNormalized, maxNormalized);
            float nz = RandomRange(random, minNormalized, maxNormalized);
            return new Vector2(origin.x + size.x * nx, origin.z + size.z * nz);
        }

        static float DistanceToRoute(Vector3 point, List<Vector3> route)
        {
            float best = float.MaxValue;
            for (int i = 0; i < route.Count - 1; i++)
            {
                Vector3 a = route[i];
                Vector3 b = route[i + 1];
                a.y = 0f;
                b.y = 0f;
                Vector3 p = point;
                p.y = 0f;
                best = Mathf.Min(best, DistancePointSegment(p, a, b));
            }
            return best;
        }

        static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float denom = Vector3.Dot(ab, ab);
            if (denom < 0.001f)
            {
                return Vector3.Distance(p, a);
            }
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / denom);
            return Vector3.Distance(p, a + ab * t);
        }

        static void RemoveExistingRoot(string rootName)
        {
            var existing = GameObject.Find(rootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        static string Pick(System.Random random, string[] values)
        {
            return values[random.Next(values.Length)];
        }

        static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static float SafeDivide(float value, float divisor)
        {
            return value / Mathf.Max(divisor, 0.0001f);
        }

        static void WriteRouteConfig(List<Vector3> routePoints, BuildResult result)
        {
            string configPath = ProjectRootPath("config/mesa_desert_route_candidate.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? string.Empty);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"stage\": \"pure_nature_mesa_desert_route_candidate\",");
            sb.AppendLine("  \"scene_path\": \"" + CandidateScenePath + "\",");
            sb.AppendLine("  \"source_scene_path\": \"" + DemoScenePath + "\",");
            sb.AppendLine("  \"seed\": " + Seed.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"notes\": \"Mesa Desert is now the Stage 21 working route scene. Existing Stage 15/20 baselines remain frozen.\",");
            sb.AppendLine("  \"added_counts\": {");
            sb.AppendLine("    \"rocks\": " + result.RockCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"rubble\": " + result.RubbleCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"trees\": " + result.TreeCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"plants\": " + result.PlantCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("  },");
            sb.AppendLine("  \"waypoints\": [");
            for (int i = 0; i < routePoints.Count; i++)
            {
                Vector3 p = routePoints[i];
                sb.Append("    { \"index\": ").Append(i.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"x\": ").Append(p.x.ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(", \"y\": ").Append(p.y.ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(", \"z\": ").Append(p.z.ToString("F3", CultureInfo.InvariantCulture));
                sb.Append(" }");
                if (i < routePoints.Count - 1)
                {
                    sb.Append(',');
                }
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(configPath, sb.ToString());
        }

        static void SaveRouteView(string logRoot, string viewName, List<Vector3> routePoints, int index, float fov)
        {
            if (routePoints.Count < 2)
            {
                return;
            }
            int i = Mathf.Clamp(index, 0, routePoints.Count - 2);
            Vector3 a = routePoints[i];
            Vector3 b = routePoints[i + 1];
            Vector3 direction = b - a;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.forward;
            }
            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            Vector3 target = Vector3.Lerp(a, b, 0.55f) + Vector3.up * 1.8f;
            Vector3 position = Vector3.Lerp(a, b, 0.20f) - direction * 36f + side * 42f + Vector3.up * 24f;
            SaveView(logRoot, viewName, position, target, fov, 1800f, false);
        }

        static void SaveObstacleView(string logRoot, string viewName, BuildResult result, float fov)
        {
            if (result.RockPositions.Count == 0)
            {
                SaveRouteView(logRoot, viewName, result.RoutePoints, Math.Max(1, result.RoutePoints.Count / 2), fov);
                return;
            }

            Vector3 focus = result.RockPositions[Mathf.Clamp(result.RockPositions.Count / 3, 0, result.RockPositions.Count - 1)];
            Vector3 plantFocus = result.PlantPositions.Count > 0 ? result.PlantPositions[Mathf.Clamp(result.PlantPositions.Count / 2, 0, result.PlantPositions.Count - 1)] : focus;
            Vector3 target = Vector3.Lerp(focus, plantFocus, 0.25f) + Vector3.up * 1.5f;
            Vector3 position = target + new Vector3(-20f, 11f, -24f);
            SaveView(logRoot, viewName, position, target, fov, 1200f, false);
        }

        static void SaveView(string logRoot, string viewName, Vector3 position, Vector3 target, float fov, float farClip, bool disableFogForScreenshot)
        {
            string path = Path.Combine(logRoot, "vln_pure_nature_mesa_desert_route_candidate_" + viewName + ".png");
            var cameraObject = new GameObject("MesaRouteCandidate_" + viewName + "_ScreenshotCamera");
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

        sealed class BuildResult
        {
            public int RockCount;
            public int RubbleCount;
            public int TreeCount;
            public int PlantCount;
            public int AddedColliderCount;
            public int NextObjectIndex;
            public readonly List<Vector3> RoutePoints = new List<Vector3>();
            public readonly List<Vector3> RockPositions = new List<Vector3>();
            public readonly List<Vector3> TreePositions = new List<Vector3>();
            public readonly List<Vector3> PlantPositions = new List<Vector3>();

            public int VegetationCount
            {
                get { return TreeCount + PlantCount; }
            }
        }
    }
}
