using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VLN.Editor
{
    public static class VlnHighPrecisionWorldPhysicsAuditRunner
    {
        public static void RunAll()
        {
            string logRoot = Path.Combine(Application.dataPath, "../Logs");
            Directory.CreateDirectory(logRoot);
            string resultPath = Path.Combine(logRoot, "vln_high_precision_world_physics_audit.txt");

            var sb = new StringBuilder();
            sb.AppendLine("started=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            try
            {
                AuditWorld(sb, "first_mesa", VlnPureNatureMesaDesertRouteCandidateBuilder.CandidateScenePath);
                AuditWorld(sb, "second_oasis", VlnPureNatureOasisDesertRouteCandidateBuilder.CandidateScenePath);
                AuditWorld(sb, "stitched_mesa_oasis", VlnPureNatureMesaOasisStitchBuilder.StitchedScenePath);
                sb.AppendLine("finished=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                sb.AppendLine("success=1");
                File.WriteAllText(resultPath, sb.ToString());
                Debug.Log("VLN_HIGH_PRECISION_WORLD_PHYSICS_AUDIT_RESULT " + resultPath);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                sb.AppendLine("exception=" + ex);
                sb.AppendLine("success=0");
                File.WriteAllText(resultPath, sb.ToString());
                Debug.LogError("VLN_HIGH_PRECISION_WORLD_PHYSICS_AUDIT_FAILED " + ex);
                EditorApplication.Exit(1);
            }
        }

        static void AuditWorld(StringBuilder sb, string key, string scenePath)
        {
            if (!File.Exists(ProjectRelativeToAbsolute(scenePath)))
            {
                sb.AppendLine(key + ".scene_missing=" + scenePath);
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true);
            var terrainColliders = UnityEngine.Object.FindObjectsOfType<TerrainCollider>(true);
            var colliders = UnityEngine.Object.FindObjectsOfType<Collider>(true);
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            var rigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>(true);
            var meshColliders = colliders.OfType<MeshCollider>().ToArray();

            sb.AppendLine("[" + key + "]");
            sb.AppendLine(key + ".scene_path=" + scenePath);
            sb.AppendLine(key + ".terrain_count=" + terrains.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".terrain_collider_count=" + terrainColliders.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".collider_count=" + colliders.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".enabled_collider_count=" + colliders.Count(c => c != null && c.enabled).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".trigger_collider_count=" + colliders.Count(c => c != null && c.isTrigger).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".mesh_collider_count=" + meshColliders.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".convex_mesh_collider_count=" + meshColliders.Count(c => c != null && c.convex).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".box_collider_count=" + colliders.OfType<BoxCollider>().Count().ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".sphere_collider_count=" + colliders.OfType<SphereCollider>().Count().ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".capsule_collider_count=" + colliders.OfType<CapsuleCollider>().Count().ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".collider_with_physics_material_count=" + colliders.Count(c => c != null && c.sharedMaterial != null).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".rigidbody_count=" + rigidbodies.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".renderer_count=" + renderers.Length.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".collider_renderer_ratio=" + (renderers.Length == 0 ? "0" : (colliders.Length / (float)renderers.Length).ToString("F3", CultureInfo.InvariantCulture)));
            sb.AppendLine(key + ".missing_mesh_collider_mesh_count=" + meshColliders.Count(c => c != null && c.sharedMesh == null).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".rock_named_collider_count=" + CountNamed(colliders, "rock", "boulder", "cliff", "rubble", "stone", "strate").ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".tree_named_collider_count=" + CountNamed(colliders, "tree", "palm", "cactus", "saguaro", "senita", "opuntia", "datepalm", "olive").ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(key + ".plant_named_collider_count=" + CountNamed(colliders, "plant", "grass", "reed", "bush", "perennial", "malcomia", "buttercup").ToString(CultureInfo.InvariantCulture));
        }

        static int CountNamed(Collider[] colliders, params string[] needles)
        {
            int count = 0;
            foreach (var collider in colliders)
            {
                if (collider == null || collider.gameObject == null)
                {
                    continue;
                }
                string path = GetHierarchyPath(collider.gameObject).ToLowerInvariant();
                if (needles.Any(path.Contains))
                {
                    count++;
                }
            }
            return count;
        }

        static string GetHierarchyPath(GameObject go)
        {
            var names = new System.Collections.Generic.List<string>();
            Transform current = go.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static string ProjectRelativeToAbsolute(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }
    }
}
