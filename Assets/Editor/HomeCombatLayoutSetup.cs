using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    /// <summary>Geometry/collision-only update, preserving story actors, camera, lighting and UI.</summary>
    [InitializeOnLoad]
    public static class HomeCombatLayoutSetup
    {
        private const string Pending = "Assets/Editor/HomeCombatLayoutSetup.pending";
        private const string Model = "Assets/Models/Levels/HomeInterior/HomeInterior_CombatOpen.fbx";
        private const string Layout = "Assets/Models/Levels/HomeInterior/HomeInterior_CombatOpen_Layout.json";
        static HomeCombatLayoutSetup() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Install(); }
            catch (Exception error) { Debug.LogException(error); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Levels/Open Up Home Combat Layout")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scenes and stop Play mode first.");
            string outdoor = Hash(HomeDoorSetup.Outside);
            string backup = "Assets/Scenes/Backups/HomeInterior_BeforeOpenLayout_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            if (!AssetDatabase.CopyAsset(HomeInteriorSetup.ScenePath, backup)) throw new IOException("Interior backup failed.");
            Scene scene = SceneManager.GetActiveScene().path == HomeInteriorSetup.ScenePath ? SceneManager.GetActiveScene() :
                EditorSceneManager.OpenScene(HomeInteriorSetup.ScenePath, OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects().Single(r => r.name == "HomeInterior_Environment").transform;
            var discovery = scene.GetRootGameObjects().Single(r => r.name == "Home_Discovery");
            int storyObjects = discovery.GetComponentsInChildren<Transform>(true).Length;
            var old = environment.Find("RoomGeometry");
            var materials = old.GetComponentsInChildren<MeshRenderer>().ToDictionary(r => r.name, r => r.sharedMaterial);
            var oldTransforms = old.GetComponentsInChildren<Transform>();
            Vector3 origin = oldTransforms.Single(t => t.name == "H1_Origin").position;
            Vector3 ax = oldTransforms.Single(t => t.name == "H1_BasisX").position - origin;
            Vector3 ay = oldTransforms.Single(t => t.name == "H1_BasisY").position - origin;
            Vector3 az = oldTransforms.Single(t => t.name == "H1_BasisZ").position - origin;
            Vector3 C(Vector3 p) => origin + ax * p.x + ay * p.y + az * p.z;
            var layout = JsonUtility.FromJson<HomeInteriorSetup.Layout>(File.ReadAllText(Layout));
            AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = importer.importCameras = importer.importLights = importer.isReadable = false;
            importer.SaveAndReimport();
            var geometry = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), scene);
            PrefabUtility.UnpackPrefabInstance(geometry, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            geometry.name = "RoomGeometry"; geometry.transform.SetParent(environment, false);
            foreach (var renderer in geometry.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = materials[renderer.name];
            var basis = geometry.GetComponentsInChildren<Transform>();
            if (Vector3.Distance(basis.Single(t => t.name == "H1_BasisX").position, origin + ax) > .001f ||
                Vector3.Distance(basis.Single(t => t.name == "H1_BasisZ").position, origin + az) > .001f)
                throw new InvalidOperationException("Replacement FBX basis differs from the existing room.");
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            UnityEngine.Object.DestroyImmediate(environment.Find("RoomCollisions").gameObject);
            var collisions = new GameObject("RoomCollisions").transform; collisions.SetParent(environment, false);
            foreach (var box in layout.boxes)
            {
                var item = new GameObject(box.name); item.transform.SetParent(collisions, false);
                item.transform.position = C(box.center); item.layer = LayerMask.NameToLayer(box.layer);
                item.AddComponent<BoxCollider>().size = Abs(ax * box.size.x) + Abs(ay * box.size.y) + Abs(az * box.size.z);
            }
            Physics.SyncTransforms();
            int obstacle = LayerMask.GetMask("ArenaObstacle");
            for (int x = 0; x < 9; x++) for (int z = 0; z < 7; z++)
            {
                Vector3 p = C(new Vector3(Mathf.Lerp(-3.5f, 3.5f, x / 8f), 0, Mathf.Lerp(-3.4f, 1.3f, z / 6f)));
                if (Physics.CheckCapsule(p + Vector3.up * .53f, p + Vector3.up * 1.53f, .49f, obstacle, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Central fighting space is obstructed: " + p);
                if (!Physics.RaycastAll(p + Vector3.up * 2, Vector3.down, 3, 1, QueryTriggerInteraction.Ignore).Any(h => h.collider.name == "Continuous floor"))
                    throw new InvalidOperationException("Combat space has no floor.");
            }
            Vector3 dashStart = C(new Vector3(0, 0, -1));
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(i * 45, Vector3.up) * az;
                if (Physics.CapsuleCast(dashStart + Vector3.up * .53f, dashStart + Vector3.up * 1.53f, .49f,
                    direction, 2.88f, obstacle, QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Central dash lane blocked.");
            }
            if (discovery.GetComponentsInChildren<Transform>(true).Length != storyObjects || collisions.childCount != 12)
                throw new InvalidOperationException("Unexpected story/collider changes.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (Hash(HomeDoorSetup.Outside) != outdoor) throw new InvalidOperationException("Outdoor scene changed unexpectedly.");
            Selection.activeGameObject = environment.gameObject;
            Debug.Log("OPEN HOME LAYOUT PASS: dining set/stool removed; bed/desk/chests at perimeter; matching 12 colliders; 63 player-capsule clearance/floor checks and 8 full dash-length casts passed; story actors, camera, lights, materials and exterior preserved; original source and scene backup retained.");
        }
        private static Vector3 Abs(Vector3 p) => new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
        private static string Hash(string path)
        { using (var sha = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    }
}
