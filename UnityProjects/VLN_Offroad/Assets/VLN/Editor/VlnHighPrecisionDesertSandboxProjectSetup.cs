using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnHighPrecisionDesertSandboxProjectSetup
    {
        public const string ScenePath = "Assets/VLN/Scenes/VLNHighPrecisionDesertSandbox.unity";
        public const float TerrainSize = 1000f;
        public const float TerrainAreaSquareMeters = TerrainSize * TerrainSize;

        const float TerrainHeight = 58f;
        const int HeightResolution = 1025;
        const int AlphaResolution = 512;
        const string TerrainDataPath = "Assets/VLN/Terrain/HighPrecisionDesertTerrainData.asset";
        const string MaterialRoot = "Assets/VLN/Materials/HighPrecisionDesert";
        const string AssetRoot = "Assets/VLN/ExternalAssets/HighPrecisionDesert/PolyHaven";

        [MenuItem("VLN/Build High Precision Desert Sandbox")]
        public static void BuildHighPrecisionDesertSandbox()
        {
            EnsureDirectories();
            AssetDatabase.Refresh();
            ConfigureTextureImporters();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            CreateOuterVisualTerrain();
            CreateTerrain();
            CreateCliffAndRockVisuals();
            CreateVegetation();
            CreateDryWashAndSurfaceDetails();
            CreateViewerCamera();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"VLN_HIGH_PRECISION_DESERT_SETUP saved scene at {ScenePath}, terrain_size_m={TerrainSize:F0}, terrain_area_m2={TerrainAreaSquareMeters:F0}");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/VLN/Scenes");
            Directory.CreateDirectory("Assets/VLN/Terrain");
            Directory.CreateDirectory(MaterialRoot);
        }

        static void ConfigureTextureImporters()
        {
            MarkNormalMap(SandNormalPath);
            MarkNormalMap(GroundRockNormalPath);
            MarkNormalMap(CliffNormalPath);
            MarkNormalMap(BoulderNormalPath);
            MarkNormalMap(DideltaNormalPath);
            MarkNormalMap(QuiverTrunkNormalPath);
            MarkNormalMap(QuiverLeafNormalPath);
        }

        static void MarkNormalMap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap)
            {
                return;
            }

            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        static void CreateLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.reflectionIntensity = 0.55f;

            var hdri = LoadTexture(HdriGoegapPath, false);
            if (hdri != null)
            {
                var skybox = EnsureMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Goegap_Skybox.mat", null, Shader.Find("Skybox/Panoramic"));
                skybox.SetTexture("_MainTex", hdri);
                skybox.SetFloat("_Exposure", 1.05f);
                skybox.SetFloat("_Rotation", 35f);
                RenderSettings.skybox = skybox;
            }
            else
            {
                RenderSettings.ambientLight = new Color(0.58f, 0.55f, 0.50f);
            }

            var sunObject = new GameObject("HighPrecisionDesert_Sun_DirectionalLight");
            sunObject.transform.rotation = Quaternion.Euler(47f, -32f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.color = new Color(1.0f, 0.92f, 0.80f);
            sun.shadows = LightShadows.Soft;
        }

        static void CreateTerrain()
        {
            DeleteAssetIfExists(TerrainDataPath);
            DeleteAssetIfExists("Assets/VLN/Terrain/HighPrecisionDesert_Sand.terrainlayer");
            DeleteAssetIfExists("Assets/VLN/Terrain/HighPrecisionDesert_RockGround.terrainlayer");
            DeleteAssetIfExists("Assets/VLN/Terrain/HighPrecisionDesert_Cliff.terrainlayer");
            DeleteAssetIfExists("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Terrain.physicMaterial");

            var terrainData = new TerrainData
            {
                name = "HighPrecisionDesertTerrainData",
                heightmapResolution = HeightResolution,
                alphamapResolution = AlphaResolution,
                size = new Vector3(TerrainSize, TerrainHeight, TerrainSize)
            };
            terrainData.SetHeights(0, 0, BuildHeights());

            var sand = CreateTerrainLayer("Assets/VLN/Terrain/HighPrecisionDesert_Sand.terrainlayer", SandDiffusePath, SandNormalPath, new Vector2(38f, 38f), new Vector4(0.23f, 0.18f, 0.12f, 0f), new Vector4(0.44f, 0.34f, 0.22f, 1f));
            var groundRock = CreateTerrainLayer("Assets/VLN/Terrain/HighPrecisionDesert_RockGround.terrainlayer", GroundRockDiffusePath, GroundRockNormalPath, new Vector2(34f, 34f), new Vector4(0.30f, 0.24f, 0.18f, 0f), new Vector4(0.50f, 0.39f, 0.29f, 1f));
            var cliff = CreateTerrainLayer("Assets/VLN/Terrain/HighPrecisionDesert_Cliff.terrainlayer", CliffDiffusePath, CliffNormalPath, new Vector2(30f, 30f), new Vector4(0.28f, 0.22f, 0.17f, 0f), new Vector4(0.50f, 0.40f, 0.29f, 1f));
            terrainData.terrainLayers = new[] { sand, groundRock, cliff };
            terrainData.SetAlphamaps(0, 0, BuildAlphaMaps());

            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);

            var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "HighPrecisionDesert_Terrain_1000m_x_1000m";
            terrainObject.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
            terrainObject.isStatic = true;

            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 10f;
            terrain.basemapDistance = 1800f;

            var collider = terrainObject.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.sharedMaterial = CreatePhysicMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Terrain.physicMaterial", 0.82f, 0.68f, 0.0f);
            }
        }

        static void CreateOuterVisualTerrain()
        {
            DeleteAssetIfExists("Assets/VLN/Terrain/HighPrecisionDesertOuterVisualMesh.asset");
            var mesh = BuildOuterVisualMesh(1800f, 193);
            AssetDatabase.CreateAsset(mesh, "Assets/VLN/Terrain/HighPrecisionDesertOuterVisualMesh.asset");

            var outer = new GameObject("HighPrecisionDesert_OuterVisualTerrain_1800m_NoCollider");
            outer.isStatic = true;
            var filter = outer.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = outer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_OuterSand.mat", SandDiffusePath, SandNormalPath, new Color(0.54f, 0.42f, 0.28f));
        }

        static Mesh BuildOuterVisualMesh(float size, int resolution)
        {
            var vertices = new Vector3[resolution * resolution];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    float worldX = Mathf.Lerp(-size * 0.5f, size * 0.5f, x / (float)(resolution - 1));
                    float worldZ = Mathf.Lerp(-size * 0.5f, size * 0.5f, z / (float)(resolution - 1));
                    float edgeFade = Mathf.Clamp01((Mathf.Max(Mathf.Abs(worldX), Mathf.Abs(worldZ)) - TerrainSize * 0.5f) / 360f);
                    float y = TerrainWorldY(Mathf.Clamp(worldX, -TerrainSize * 0.5f, TerrainSize * 0.5f), Mathf.Clamp(worldZ, -TerrainSize * 0.5f, TerrainSize * 0.5f));
                    y += 11.0f * Smooth01(edgeFade) + 2.5f * Mathf.Sin(worldX * 0.010f + worldZ * 0.007f);
                    vertices[index] = new Vector3(worldX, y - 0.08f, worldZ);
                    uvs[index] = new Vector2(worldX / 12f, worldZ / 12f);
                }
            }

            int t = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int bl = z * resolution + x;
                    int br = bl + 1;
                    int tl = bl + resolution;
                    int tr = tl + 1;
                    triangles[t++] = bl;
                    triangles[t++] = tl;
                    triangles[t++] = br;
                    triangles[t++] = br;
                    triangles[t++] = tl;
                    triangles[t++] = tr;
                }
            }

            var mesh = new Mesh
            {
                name = "HighPrecisionDesertOuterVisualMesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float[,] BuildHeights()
        {
            var heights = new float[HeightResolution, HeightResolution];
            for (int z = 0; z < HeightResolution; z++)
            {
                float worldZ = Mathf.Lerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z / (float)(HeightResolution - 1));
                for (int x = 0; x < HeightResolution; x++)
                {
                    float worldX = Mathf.Lerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, x / (float)(HeightResolution - 1));
                    heights[z, x] = NormalizedTerrainHeight(worldX, worldZ);
                }
            }

            return heights;
        }

        static float[,,] BuildAlphaMaps()
        {
            var maps = new float[AlphaResolution, AlphaResolution, 3];
            for (int z = 0; z < AlphaResolution; z++)
            {
                float worldZ = Mathf.Lerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z / (float)(AlphaResolution - 1));
                for (int x = 0; x < AlphaResolution; x++)
                {
                    float worldX = Mathf.Lerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, x / (float)(AlphaResolution - 1));
                    float routeX = RouteCenterX(worldZ);
                    float routeDistance = Mathf.Abs(worldX - routeX);
                    float cliffMask = Mathf.Clamp01((Mathf.Abs(worldX) - 452f) / 42f) + Mathf.Clamp01((Mathf.Abs(worldZ) - 466f) / 32f);
                    cliffMask = Mathf.Clamp01(cliffMask);
                    float routeBlend = Smooth01(1f - Mathf.Clamp01(routeDistance / 42f));
                    float washA = Smooth01(1f - Mathf.Clamp01(Mathf.Abs(worldX - (RouteCenterX(worldZ) - 74f + Mathf.Sin(worldZ * 0.011f + 38f) * 20.5f)) / 52f));
                    float washB = Smooth01(1f - Mathf.Clamp01(Mathf.Abs(worldX - (RouteCenterX(worldZ) + 162f + Mathf.Sin(worldZ * 0.011f - 24f) * 12.0f)) / 38f));
                    float washBlend = Mathf.Max(washA * 0.28f, washB * 0.20f);
                    float stoneNoise = 0.5f + 0.5f * Mathf.Sin(worldX * 0.025f + worldZ * 0.019f) * Mathf.Cos(worldX * 0.011f - worldZ * 0.017f);
                    float outcropMask = Smooth01(Mathf.Clamp01((stoneNoise - 0.54f) / 0.46f));

                    float cliffWeight = cliffMask * 0.26f + outcropMask * 0.045f;
                    float rockWeight = 0.30f + routeBlend * 0.14f + outcropMask * 0.13f + washBlend;
                    rockWeight *= 1f - cliffMask * 0.32f;
                    float sandWeight = 0.55f + (1f - routeBlend) * 0.04f + (1f - outcropMask) * 0.03f;
                    sandWeight *= 1f - cliffMask * 0.18f;
                    float sum = sandWeight + rockWeight + cliffWeight;
                    maps[z, x, 0] = sandWeight / sum;
                    maps[z, x, 1] = rockWeight / sum;
                    maps[z, x, 2] = cliffWeight / sum;
                }
            }

            return maps;
        }

        static float NormalizedTerrainHeight(float x, float z)
        {
            float routeX = RouteCenterX(z);
            float routeBlend = Smooth01(1f - Mathf.Clamp01(Mathf.Abs(x - routeX) / 24f));
            float dunes = 0.050f * Mathf.Sin(0.018f * x + 0.45f * Mathf.Sin(0.009f * z));
            dunes += 0.034f * Mathf.Cos(0.016f * z - 0.012f * x);
            dunes += 0.018f * Mathf.Sin(0.055f * x + 0.038f * z);
            float longSlope = 0.090f * Mathf.InverseLerp(-TerrainSize * 0.5f, TerrainSize * 0.5f, z);
            float edgeRise = 0.20f * Smooth01(Mathf.Clamp01((Mathf.Abs(x) - 360f) / 135f));
            edgeRise += 0.08f * Smooth01(Mathf.Clamp01((Mathf.Abs(z) - 420f) / 80f));
            float baseHeight = 0.23f + dunes + longSlope + edgeRise;
            float roadHeight = 0.245f + longSlope * 0.72f + 0.010f * Mathf.Sin(0.027f * z) + 0.006f * Mathf.Sin(0.041f * x);
            return Mathf.Clamp01(Mathf.Lerp(baseHeight, roadHeight, routeBlend * 0.76f));
        }

        public static float TerrainWorldY(float x, float z)
        {
            return NormalizedTerrainHeight(x, z) * TerrainHeight;
        }

        static float RouteCenterX(float z)
        {
            return 72f * Mathf.Sin((z + 430f) * 0.0062f) + 21f * Mathf.Sin((z - 80f) * 0.019f);
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static float Hash01(int seed)
        {
            int reduced = seed % 100000;
            if (reduced < 0)
            {
                reduced += 100000;
            }
            double value = Math.Sin(reduced * 12.9898 + 78.233) * 43758.5453;
            double fraction = value - Math.Floor(value);
            if (double.IsNaN(fraction) || double.IsInfinity(fraction))
            {
                return 0.5f;
            }
            return Mathf.Clamp01((float)fraction);
        }

        static Material PickMaterial(Material[] materials, int seed)
        {
            if (materials == null || materials.Length == 0)
            {
                return null;
            }
            int index = Mathf.Clamp(Mathf.FloorToInt(Hash01(seed) * materials.Length), 0, materials.Length - 1);
            return materials[index];
        }

        static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }
                return hash;
            }
        }

        static TerrainLayer CreateTerrainLayer(string path, string diffusePath, string normalPath, Vector2 tileSize, Vector4 remapMin, Vector4 remapMax)
        {
            var layer = new TerrainLayer
            {
                diffuseTexture = LoadTexture(diffusePath, false),
                normalMapTexture = LoadTexture(normalPath, true),
                tileSize = tileSize,
                metallic = 0f,
                smoothness = 0.30f
            };
            layer.diffuseRemapMin = remapMin;
            layer.diffuseRemapMax = remapMax;
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        static void CreateCliffAndRockVisuals()
        {
            var boulderMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Boulder01.mat", BoulderDiffusePath, BoulderNormalPath, new Color(0.56f, 0.50f, 0.42f));
            var boulderWarmMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Boulder01_Warm.mat", BoulderDiffusePath, BoulderNormalPath, new Color(0.65f, 0.53f, 0.39f));
            var boulderDarkMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Boulder01_Dark.mat", BoulderDiffusePath, BoulderNormalPath, new Color(0.43f, 0.37f, 0.30f));
            var boulderPaleMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Boulder01_Pale.mat", BoulderDiffusePath, BoulderNormalPath, new Color(0.68f, 0.59f, 0.47f));
            var rockMaterials = new[] { boulderMaterial, boulderWarmMaterial, boulderDarkMaterial, boulderPaleMaterial };

            CreateRockRidge("NorthWestRidge", new Vector3(-390f, 0f, 360f), 18, 23f, 15.5f, 19f, boulderMaterial, false);
            CreateRockRidge("NorthEastRidge", new Vector3(365f, 0f, 315f), 20, 24f, 18.0f, -24f, boulderMaterial, false);
            CreateRockRidge("SouthRidge", new Vector3(-360f, 0f, -405f), 18, 27f, 14.0f, -15f, boulderMaterial, false);
            CreateRockRidge("WestCanyonEdge", new Vector3(-440f, 0f, -40f), 17, 24f, 11.0f, 88f, boulderMaterial, false);
            CreateRockRidge("EastCanyonEdge", new Vector3(420f, 0f, 65f), 17, 24f, 11.5f, 92f, boulderMaterial, false);

            CreateRockCluster("RouteOutcropA", new Vector2(-125f, -315f), 28, 64f, 46f, 6.8f, rockMaterials, 18f, true);
            CreateRockCluster("RouteOutcropB", new Vector2(128f, -172f), 34, 78f, 54f, 7.5f, rockMaterials, -32f, true);
            CreateRockCluster("MidValleyBrokenRocks", new Vector2(-46f, 42f), 42, 112f, 70f, 5.6f, rockMaterials, 6f, true);
            CreateRockCluster("EastMesaFoot", new Vector2(274f, 220f), 50, 130f, 92f, 9.8f, rockMaterials, -18f, false);
            CreateRockCluster("WestScarpDebris", new Vector2(-310f, 150f), 46, 118f, 86f, 8.6f, rockMaterials, 24f, false);
            CreateRockCluster("SouthernBasin", new Vector2(220f, -378f), 36, 126f, 72f, 6.2f, rockMaterials, -8f, false);

            for (int i = 0; i < 132; i++)
            {
                float z = Mathf.Lerp(-455f, 455f, Hash01(i * 61 + 3));
                float side = Hash01(i * 71 + 9) < 0.5f ? -1f : 1f;
                float routeOffset = Mathf.Lerp(23f, 92f, Hash01(i * 83 + 11));
                if (Hash01(i * 97 + 15) < 0.28f)
                {
                    routeOffset = Mathf.Lerp(112f, 390f, Hash01(i * 101 + 17));
                }
                float x = RouteCenterX(z) + side * routeOffset + Mathf.Sin(i * 1.31f) * Mathf.Lerp(4f, 20f, Hash01(i * 43 + 5));
                x = Mathf.Clamp(x, -455f, 455f);
                z = Mathf.Clamp(z, -455f, 455f);
                float targetHeight = Mathf.Lerp(0.85f, 3.65f, Hash01(i * 113 + 19));
                bool addCollider = Mathf.Abs(x - RouteCenterX(z)) < 56f || Hash01(i * 127 + 23) < 0.18f;
                var material = PickMaterial(rockMaterials, i * 131 + 29);
                var rotation = Quaternion.Euler(Mathf.Lerp(-6f, 8f, Hash01(i * 137 + 31)), Mathf.Lerp(0f, 360f, Hash01(i * 149 + 37)), Mathf.Lerp(-5f, 7f, Hash01(i * 151 + 41)));
                var instance = InstantiateModel(BoulderModelPath, $"HighPrecisionDesert_Boulder01_{i:00}", new Vector3(x, 0f, z), rotation, Vector3.one, material, addCollider);
                if (instance != null)
                {
                    NormalizeHeight(instance, targetHeight);
                    AlignBottomToGround(instance, x, z, 0.02f);
                }
            }

            CreatePebbleField("MainTrackShoulders", 210, -440f, 455f, 16f, 46f, rockMaterials);
            CreatePebbleField("DryWashScatter", 160, -360f, 390f, 68f, 148f, rockMaterials);
        }

        static void CreateRockCluster(string name, Vector2 center, int count, float radiusX, float radiusZ, float maxHeight, Material[] materials, float yawBias, bool nearRouteCollider)
        {
            int seed = StableHash(name);
            for (int i = 0; i < count; i++)
            {
                float angle = Hash01(seed + i * 41) * Mathf.PI * 2f;
                float radius = Mathf.Pow(Hash01(seed + i * 47 + 7), 0.62f);
                float x = center.x + Mathf.Cos(angle) * radiusX * radius + Mathf.Sin(i * 1.77f) * 7.0f;
                float z = center.y + Mathf.Sin(angle) * radiusZ * radius + Mathf.Cos(i * 1.13f) * 5.5f;
                x = Mathf.Clamp(x, -462f, 462f);
                z = Mathf.Clamp(z, -462f, 462f);
                float height = Mathf.Lerp(0.9f, maxHeight, Mathf.Pow(Hash01(seed + i * 53 + 13), 0.46f));
                bool addCollider = nearRouteCollider && Mathf.Abs(x - RouteCenterX(z)) < 62f && Hash01(seed + i * 59 + 17) < 0.45f;
                var material = PickMaterial(materials, seed + i * 61 + 19);
                var rotation = Quaternion.Euler(Mathf.Lerp(-9f, 11f, Hash01(seed + i * 67 + 23)), yawBias + Mathf.Lerp(0f, 360f, Hash01(seed + i * 71 + 29)), Mathf.Lerp(-7f, 8f, Hash01(seed + i * 73 + 31)));
                var rock = InstantiateModel(BoulderModelPath, $"HighPrecisionDesert_RockCluster_{name}_{i:00}", new Vector3(x, 0f, z), rotation, Vector3.one, material, addCollider);
                if (rock != null)
                {
                    NormalizeHeight(rock, height);
                    AlignBottomToGround(rock, x, z, Mathf.Lerp(-0.06f, 0.04f, Hash01(seed + i * 79 + 37)));
                }
            }
        }

        static void CreatePebbleField(string name, int count, float zMin, float zMax, float nearOffset, float farOffset, Material[] materials)
        {
            int seed = StableHash(name);
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(zMin, zMax, Hash01(seed + i * 31));
                float side = Hash01(seed + i * 37 + 3) < 0.5f ? -1f : 1f;
                float offset = Mathf.Lerp(nearOffset, farOffset, Mathf.Pow(Hash01(seed + i * 43 + 5), 1.4f));
                float x = RouteCenterX(z) + side * offset + Mathf.Lerp(-9f, 9f, Hash01(seed + i * 47 + 7));
                x = Mathf.Clamp(x, -470f, 470f);
                z = Mathf.Clamp(z, -470f, 470f);
                float height = Mathf.Lerp(0.18f, 0.72f, Hash01(seed + i * 53 + 11));
                var material = PickMaterial(materials, seed + i * 59 + 13);
                var pebble = InstantiateModel(BoulderModelPath, $"HighPrecisionDesert_Pebble_{name}_{i:00}", new Vector3(x, 0f, z), Quaternion.Euler(Mathf.Lerp(-12f, 12f, Hash01(seed + i * 61 + 17)), Mathf.Lerp(0f, 360f, Hash01(seed + i * 67 + 19)), Mathf.Lerp(-10f, 10f, Hash01(seed + i * 71 + 23))), Vector3.one, material, false);
                if (pebble != null)
                {
                    NormalizeHeight(pebble, height);
                    AlignBottomToGround(pebble, x, z, -0.02f);
                }
            }
        }

        static void CreateRockRidge(string name, Vector3 center, int count, float spacing, float height, float yawBase, Material material, bool addCollider)
        {
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * spacing;
                float x = center.x + Mathf.Cos(yawBase * Mathf.Deg2Rad) * offset + Mathf.Sin(i * 1.7f) * 2.4f;
                float z = center.z + Mathf.Sin(yawBase * Mathf.Deg2Rad) * offset + Mathf.Cos(i * 1.3f) * 2.0f;
                float targetHeight = height * Mathf.Lerp(0.62f, 1.15f, (i % 5) / 4f);
                var rock = InstantiateModel(BoulderModelPath, $"HighPrecisionDesert_RockRidge_{name}_{i:00}", new Vector3(x, 0f, z), Quaternion.Euler(-4f + i % 3 * 3f, yawBase + i * 31f, 2f - i % 4), Vector3.one, material, addCollider);
                if (rock != null)
                {
                    NormalizeHeight(rock, targetHeight);
                    AlignBottomToGround(rock, x, z, -0.08f);
                }
            }
        }

        static void CreateVegetation()
        {
            var shrubMaterial = CreateCutoutMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_DideltaSpinosa.mat", DideltaDiffusePath, DideltaNormalPath, DideltaAlphaPath, new Color(0.45f, 0.38f, 0.24f));
            var treeMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_QuiverTree.mat", QuiverTrunkDiffusePath, QuiverTrunkNormalPath, new Color(0.43f, 0.30f, 0.19f));

            for (int i = 0; i < 520; i++)
            {
                float z = Mathf.Lerp(-470f, 470f, Hash01(i * 17 + 5));
                float side = Hash01(i * 19 + 7) < 0.5f ? -1f : 1f;
                float offset = Mathf.Lerp(12f, 94f, Mathf.Pow(Hash01(i * 23 + 11), 0.72f));
                if (Hash01(i * 29 + 13) < 0.34f)
                {
                    offset = Mathf.Lerp(120f, 430f, Hash01(i * 31 + 17));
                }
                float habitatWave = 18f * Mathf.Sin(z * 0.012f + i * 0.11f) + 9f * Mathf.Cos(z * 0.021f - i * 0.07f);
                float x = RouteCenterX(z) + side * offset + habitatWave;
                x = Mathf.Clamp(x, -465f, 465f);
                z = Mathf.Clamp(z, -465f, 465f);
                var shrub = InstantiateModel(DideltaModelPath, $"HighPrecisionDesert_DryShrub_{i:00}", new Vector3(x, 0f, z), Quaternion.Euler(-90f + Mathf.Lerp(-4f, 5f, Hash01(i * 37 + 19)), Mathf.Lerp(0f, 360f, Hash01(i * 41 + 23)), Mathf.Lerp(-3f, 3f, Hash01(i * 43 + 29))), Vector3.one, shrubMaterial, false);
                if (shrub != null)
                {
                    NormalizeHeight(shrub, Mathf.Lerp(0.48f, 1.18f, Hash01(i * 47 + 31)));
                    AlignBottomToGround(shrub, x, z, 0f);
                    if (Mathf.Abs(x - RouteCenterX(z)) < 38f)
                    {
                        AddVegetationProxy(shrub, Mathf.Lerp(0.45f, 0.85f, Hash01(i * 53 + 37)));
                    }
                }
            }

            for (int i = 0; i < 72; i++)
            {
                float z = Mathf.Lerp(-440f, 440f, Hash01(i * 67 + 41));
                float side = Hash01(i * 71 + 43) < 0.5f ? -1f : 1f;
                float x = RouteCenterX(z) + side * Mathf.Lerp(58f, 360f, Hash01(i * 73 + 47)) + Mathf.Cos(i * 0.58f) * Mathf.Lerp(8f, 30f, Hash01(i * 79 + 53));
                x = Mathf.Clamp(x, -455f, 455f);
                z = Mathf.Clamp(z, -455f, 455f);
                Vector3 p = new Vector3(x, 0f, z);
                var tree = InstantiateModel(QuiverTreeModelPath, $"HighPrecisionDesert_QuiverTree_{i:00}", p, Quaternion.Euler(-90f + Mathf.Lerp(-2f, 3f, Hash01(i * 83 + 59)), Mathf.Lerp(0f, 360f, Hash01(i * 89 + 61)), Mathf.Lerp(-2f, 2f, Hash01(i * 97 + 67))), Vector3.one, treeMaterial, false);
                if (tree != null)
                {
                    NormalizeHeight(tree, Mathf.Lerp(3.4f, 6.8f, Hash01(i * 101 + 71)));
                    AlignBottomToGround(tree, p.x, p.z, 0f);
                    if (Mathf.Abs(x - RouteCenterX(z)) < 95f)
                    {
                        AddTrunkProxy(tree, 0.38f, 2.0f);
                    }
                }
            }
        }

        static void CreateDryWashAndSurfaceDetails()
        {
            var washMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_DryWashSand.mat", SandDiffusePath, SandNormalPath, new Color(0.40f, 0.32f, 0.22f));
            var gravelMaterial = CreateStandardMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_RouteGravel.mat", GroundRockDiffusePath, GroundRockNormalPath, new Color(0.37f, 0.31f, 0.24f));

            CreateSurfaceRibbon("HighPrecisionDesert_DryWash_Main", -430f, 420f, 18f, -74f, 38f, washMaterial, 54, 0.012f);
            CreateSurfaceRibbon("HighPrecisionDesert_DryWash_EastFork", -250f, 330f, 12f, 162f, -24f, washMaterial, 38, 0.014f);
            CreateSurfaceRibbon("HighPrecisionDesert_RouteGravel_BrokenTrack", -455f, 455f, 5.5f, 0f, 0f, gravelMaterial, 96, 0.016f);
        }

        static void CreateSurfaceRibbon(string name, float zMin, float zMax, float width, float xOffset, float phase, Material material, int segments, float lift)
        {
            int seed = StableHash(name);
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float z = Mathf.Lerp(zMin, zMax, t);
                float centerX = RouteCenterX(z) + xOffset + Mathf.Sin(z * 0.011f + phase) * width * 0.72f;
                float localWidth = width * Mathf.Lerp(0.62f, 1.28f, Hash01(seed + i * 31));
                float leftX = centerX - localWidth;
                float rightX = centerX + localWidth;
                vertices[i * 2] = new Vector3(leftX, TerrainWorldY(leftX, z) + lift, z);
                vertices[i * 2 + 1] = new Vector3(rightX, TerrainWorldY(rightX, z) + lift, z);
                uvs[i * 2] = new Vector2(0f, t * 12f);
                uvs[i * 2 + 1] = new Vector2(1f, t * 12f);
            }
            int tri = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles[tri++] = a;
                triangles[tri++] = c;
                triangles[tri++] = b;
                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = d;
            }

            var mesh = new Mesh
            {
                name = name + "Mesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var obj = new GameObject(name);
            obj.isStatic = true;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void CreateViewerCamera()
        {
            var cameraObject = new GameObject("HighPrecisionDesert_ViewerCamera");
            cameraObject.transform.position = new Vector3(-96f, 28f, -430f);
            cameraObject.transform.LookAt(new Vector3(RouteCenterX(-240f), TerrainWorldY(RouteCenterX(-240f), -240f) + 2.0f, -240f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2400f;
            camera.fieldOfView = 49f;
            camera.tag = "MainCamera";
        }

        static GameObject InstantiateModel(string path, string instanceName, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool addCollider)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"VLN_HIGH_PRECISION_DESERT missing model {path}");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = instanceName;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = scale;
            instance.isStatic = true;
            if (material != null)
            {
                ApplyMaterialRecursively(instance, material);
            }
            if (addCollider)
            {
                AddMeshColliderProxies(instance);
            }
            return instance;
        }

        static void NormalizeHeight(GameObject instance, float targetHeight)
        {
            Bounds bounds = CalculateRendererBounds(instance);
            if (bounds.size.y < 0.001f)
            {
                return;
            }
            float factor = targetHeight / bounds.size.y;
            instance.transform.localScale *= factor;
        }

        static void AlignBottomToGround(GameObject instance, float x, float z, float lift)
        {
            Bounds bounds = CalculateRendererBounds(instance);
            float targetY = TerrainWorldY(x, z) + lift;
            instance.transform.position += new Vector3(0f, targetY - bounds.min.y, 0f);
        }

        static void ApplyMaterialRecursively(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    shared[i] = material;
                }
                renderer.sharedMaterials = shared;
            }
        }

        static void AddMeshColliderProxies(GameObject root)
        {
            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }
                var collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
                collider.sharedMaterial = CreatePhysicMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Rock.physicMaterial", 0.95f, 0.75f, 0f);
            }
        }

        static void AddVegetationProxy(GameObject root, float radius)
        {
            var bounds = CalculateRendererBounds(root);
            var proxy = root.AddComponent<CapsuleCollider>();
            proxy.radius = radius * 0.5f;
            proxy.height = Mathf.Max(bounds.size.y, 0.5f);
            proxy.center = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + proxy.height * 0.5f, bounds.center.z));
            proxy.isTrigger = true;
        }

        static void AddTrunkProxy(GameObject root, float radius, float height)
        {
            var bounds = CalculateRendererBounds(root);
            var proxy = root.AddComponent<CapsuleCollider>();
            proxy.radius = radius;
            proxy.height = height;
            proxy.center = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z));
            proxy.isTrigger = false;
            proxy.sharedMaterial = CreatePhysicMaterial("Assets/VLN/Materials/HighPrecisionDesert/HighPrecisionDesert_Wood.physicMaterial", 0.80f, 0.58f, 0f);
        }

        static Material CreateStandardMaterial(string path, string diffusePath, string normalPath, Color fallbackColor)
        {
            var material = EnsureMaterial(path, fallbackColor);
            var diffuse = LoadTexture(diffusePath, false);
            var normal = LoadTexture(normalPath, true);
            if (diffuse != null)
            {
                material.mainTexture = diffuse;
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 0.65f);
            }
            material.SetFloat("_Glossiness", 0.18f);
            return material;
        }

        static Material CreateCutoutMaterial(string path, string diffusePath, string normalPath, string alphaPath, Color fallbackColor)
        {
            var material = CreateStandardMaterial(path, diffusePath, normalPath, fallbackColor);
            var alpha = LoadTexture(alphaPath, false);
            if (alpha != null)
            {
                material.SetTexture("_MainTex", LoadTexture(diffusePath, false));
                material.SetTexture("_OcclusionMap", alpha);
                material.SetFloat("_Mode", 1f);
                material.SetFloat("_Cutoff", 0.35f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            return material;
        }

        static Material EnsureMaterial(string path, Color? color, Shader shader = null)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (color.HasValue)
            {
                material.color = color.Value;
            }
            return material;
        }

        static PhysicMaterial CreatePhysicMaterial(string path, float staticFriction, float dynamicFriction, float bounciness)
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
            if (material == null)
            {
                material = new PhysicMaterial(Path.GetFileNameWithoutExtension(path));
                AssetDatabase.CreateAsset(material, path);
            }
            material.staticFriction = staticFriction;
            material.dynamicFriction = dynamicFriction;
            material.bounciness = bounciness;
            material.frictionCombine = PhysicMaterialCombine.Average;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            return material;
        }

        static Texture2D LoadTexture(string path, bool normal)
        {
            if (normal)
            {
                MarkNormalMap(path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"VLN_HIGH_PRECISION_DESERT missing texture {path}");
            }
            return texture;
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

        static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        const string SandDiffusePath = AssetRoot + "/Surfaces/aerial_sand/aerial_sand_diff_4k.jpg";
        const string SandNormalPath = AssetRoot + "/Surfaces/aerial_sand/aerial_sand_nor_gl_4k.jpg";
        const string GroundRockDiffusePath = AssetRoot + "/Surfaces/aerial_ground_rock/aerial_ground_rock_diff_4k.jpg";
        const string GroundRockNormalPath = AssetRoot + "/Surfaces/aerial_ground_rock/aerial_ground_rock_nor_gl_4k.jpg";
        const string CliffDiffusePath = AssetRoot + "/Surfaces/cliff_side/cliff_side_diff_4k.jpg";
        const string CliffNormalPath = AssetRoot + "/Surfaces/cliff_side/cliff_side_nor_gl_4k.jpg";
        const string HdriGoegapPath = AssetRoot + "/HDRI/goegap/goegap_4k.hdr";
        const string BoulderModelPath = AssetRoot + "/Models/boulder_01/boulder_01_4k.fbx";
        const string BoulderDiffusePath = AssetRoot + "/Models/boulder_01/boulder_01_diff_4k.jpg";
        const string BoulderNormalPath = AssetRoot + "/Models/boulder_01/boulder_01_nor_gl_4k.jpg";
        const string DideltaModelPath = AssetRoot + "/Models/didelta_spinosa/didelta_spinosa_2k.fbx";
        const string DideltaDiffusePath = AssetRoot + "/Models/didelta_spinosa/didelta_spinosa_diff_2k.jpg";
        const string DideltaAlphaPath = AssetRoot + "/Models/didelta_spinosa/didelta_spinosa_alpha_2k.jpg";
        const string DideltaNormalPath = AssetRoot + "/Models/didelta_spinosa/didelta_spinosa_nor_gl_2k.jpg";
        const string QuiverTreeModelPath = AssetRoot + "/Models/quiver_tree_01/quiver_tree_01_2k.fbx";
        const string QuiverTrunkDiffusePath = AssetRoot + "/Models/quiver_tree_01/quiver_tree_01_trunk_diff_2k.jpg";
        const string QuiverTrunkNormalPath = AssetRoot + "/Models/quiver_tree_01/quiver_tree_01_trunk_nor_gl_2k.jpg";
        const string QuiverLeafNormalPath = AssetRoot + "/Models/quiver_tree_01/quiver_tree_01_leaf_nor_gl_2k.jpg";
    }
}
