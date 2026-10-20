using System;
using System.IO;
using System.Linq;
using Survivor.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class HomeDoorSetup
    {
        private const string Pending = "Assets/Editor/HomeDoorSetup.pending";
        public const string Outside = "Assets/Scenes/Game.unity";
        static HomeDoorSetup() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Install(); }
            catch (Exception error) { Debug.LogException(error); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Levels/Connect Home Door")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save scene edits and stop Play mode before connecting the home door.");
            if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == HomeInteriorSetup.ScenePath))
                throw new InvalidOperationException("Home interior must be enabled in Build Settings first.");
            string interiorHash = Hash(HomeInteriorSetup.ScenePath);
            var scene = EditorSceneManager.OpenScene(Outside, OpenSceneMode.Single);
            var marker = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "Level01_HomeEntrance");
            var door = marker.GetComponent<HomeDoor>();
            if (door == null) door = marker.gameObject.AddComponent<HomeDoor>();
            // The original marker sits on the paved front approach. No geometry/colliders move.
            Physics.SyncTransforms();
            if (!Physics.Raycast(marker.position + Vector3.up * 3, Vector3.down, out _, 6, 1, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Home entrance does not have walkable paving underneath.");
            EditorUtility.SetDirty(door);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the connected door.");
            if (Hash(HomeInteriorSetup.ScenePath) != interiorHash) throw new InvalidOperationException("Interior unexpectedly changed.");
            Selection.activeGameObject = marker.gameObject;
            Debug.Log("HOME DOOR SETUP PASS: E / controller North at existing front entrance, fade into home. Outdoor geometry/lighting/player settings preserved; interior file unchanged.");
        }
        private static string Hash(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
