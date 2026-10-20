using System;
using System.IO;
using System.Linq;
using Survivor.Enemies;
using Survivor.Player;
using Survivor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class HomeDiscoveryPlayValidation
    {
        private const string Pending = "Assets/Editor/HomeDiscoveryPlayValidation.pending";
        private const string Key = "HomeDiscovery.Test.";
        private static Quaternion headStart;
        static HomeDiscoveryPlayValidation() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                int phase = SessionState.GetInt(Key + "Phase", 0);
                double now = EditorApplication.timeSinceStartup;
                if (phase == 0)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new Exception("Save and stop Play before the discovery test.");
                    EditorSceneManager.OpenScene(HomeInteriorSetup.ScenePath, OpenSceneMode.Single);
                    SessionState.SetBool(Key + "Background", Application.runInBackground);
                    Application.runInBackground = true;
                    SessionState.SetFloat(Key + "Deadline", (float)now + 60);
                    Next(1, now + 2);
                    EditorApplication.isPlaying = true;
                    return;
                }
                if (now > SessionState.GetFloat(Key + "Deadline", 0)) throw new Exception("Discovery Play test timed out.");
                if (!EditorApplication.isPlaying || now < SessionState.GetFloat(Key + "Wait", 0)) return;
                Validate();
                var head = GameObject.Find("FeedingZombie").GetComponentsInChildren<Transform>().Single(t => t.name == "mixamorig:Head");
                if (phase == 1)
                {
                    headStart = head.localRotation;
                    Next(2, now + .5);
                }
                else if (phase == 2)
                {
                    if (Quaternion.Angle(headStart, head.localRotation) < .1f) throw new Exception("Feeding loop did not animate.");
                    Capture();
                    UnityEngine.Object.FindAnyObjectByType<PlayerHealth>().Kill();
                    UnityEngine.Object.FindAnyObjectByType<GameOverUI>().RestartGame();
                    Next(3, now + 2);
                }
                else
                {
                    Capture();
                    Debug.Log("HOME DISCOVERY PLAY PASS: lying mother uses separate static mesh; original living prefab preserved; crouched zombie's feeding loop animates with no AI/contact damage/health/physics; equipped hero alive and entry clear; actual Retry restores tableau. Preview captured from running scene.");
                    Finish();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(); }
        }
        private static void Validate()
        {
            var mother = GameObject.Find("Mother"); var zombie = GameObject.Find("FeedingZombie");
            if (mother == null || zombie == null) throw new Exception("Discovery actors missing.");
            HomeDiscoverySetup.Validate(SceneManager.GetActiveScene(), mother, zombie);
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if (player.GetComponent<PlayerHealth>().IsDead || Time.timeScale != 1 || player.transform.Find("VeilWalkerVisual") == null)
                throw new Exception("Discovery player not alive/equipped.");
            var skin = zombie.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh(); skin.BakeMesh(mesh, true);
            var points = mesh.vertices.Select(p => skin.transform.TransformPoint(p)).ToArray();
            UnityEngine.Object.DestroyImmediate(mesh);
            float height = points.Max(p => p.y) - points.Min(p => p.y);
            if (height < .6f || height > 1.7f || points.Min(p => p.y) < -.15f)
                throw new Exception($"Crouch skin height/floor invalid: height={height}, minY={points.Min(p => p.y)}.");
        }
        private static void Capture()
        {
            var camera = Camera.main; var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1440, 900, 24); var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.aspect = 1.6f;
                camera.GetComponent<Survivor.CameraSystem.RoomCamera>().RefreshFraming();
                camera.Render(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes("ArtSource/Previews/HomeDiscovery_Unity01.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; camera.ResetAspect(); camera.GetComponent<Survivor.CameraSystem.RoomCamera>().RefreshFraming();
                RenderTexture.active = active; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        private static void Next(int phase, double wait)
        { SessionState.SetInt(Key + "Phase", phase); SessionState.SetFloat(Key + "Wait", (float)wait); }
        private static void Finish()
        {
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            SessionState.SetInt(Key + "Phase", 0); AssetDatabase.DeleteAsset(Pending);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
