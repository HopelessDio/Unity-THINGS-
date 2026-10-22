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
                    // This fixture isolates feeding/head tracking. The separate
                    // encounter fixture validates the real stand-up/combat handoff.
                    var rise = GameObject.Find("FeedingZombie").GetComponent<ZombieRiseEncounter>();
                    if (rise != null) rise.enabled = false;
                    if (GameObject.Find("FeedingZombie").GetComponent<ZombieNoticePlayer>().HasNoticed)
                        throw new Exception("Zombie reacted before the feeding delay elapsed.");
                    ValidateFeedingCycle();
                    headStart = head.localRotation;
                    Next(2, now + .5);
                }
                else if (phase == 2)
                {
                    if (Quaternion.Angle(headStart, head.localRotation) < .1f) throw new Exception("Feeding loop did not animate.");
                    Capture();
                    Next(4, now + 3.5);
                }
                else if (phase == 4)
                {
                    var notice = GameObject.Find("FeedingZombie").GetComponent<ZombieNoticePlayer>();
                    var player = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
                    if (!notice.IsWatching) return; // Editor rendering can advance slower than wall-clock time.
                    if (Vector3.Dot(notice.GazeDirection.normalized,
                        (player.transform.position + Vector3.up * .55f - head.position).normalized) < .95f)
                        throw new Exception("Zombie did not finish turning its gaze toward the player.");
                    ValidateTrackingSweep(notice);
                    Capture("ArtSource/Previews/HomeDiscovery_Notice_Unity01.png");
                    UnityEngine.Object.FindAnyObjectByType<PlayerHealth>().Kill();
                    UnityEngine.Object.FindAnyObjectByType<GameOverUI>().RestartGame();
                    Next(3, now + 2);
                }
                else
                {
                    if (GameObject.Find("FeedingZombie").GetComponent<ZombieNoticePlayer>().HasNoticed)
                        throw new Exception("Retry did not reset the notice delay.");
                    Capture();
                    Debug.Log("HOME DISCOVERY PLAY PASS: full feeding cycle/blood dressing retained; no early reaction; delayed smooth head/upper-body turn faces the player; no combat enabled; actual Retry resets the feeding/notice sequence. Feeding and watching previews captured.");
                    Finish();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(); }
        }
        private static void ValidateTrackingSweep(ZombieNoticePlayer notice)
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            var body = player.GetComponent<Rigidbody>();
            Vector3 start = body.position, velocity = body.linearVelocity;
            var bones = notice.GetComponentsInChildren<Transform>().Where(t =>
                t.name == "mixamorig:Spine" || t.name == "mixamorig:Spine1" || t.name == "mixamorig:Neck" || t.name == "mixamorig:Head").ToArray();
            var before = bones.Select(t => t.rotation).ToArray();
            var sample = typeof(ZombieNoticePlayer).GetMethod("TickReaction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            float maximum = 0;
            void Step()
            {
                sample.Invoke(notice, new object[] { 1f / 60f, Time.time });
                for (int i = 0; i < bones.Length; i++)
                {
                    float change = Quaternion.Angle(before[i], bones[i].rotation);
                    maximum = Mathf.Max(maximum, change);
                    if (change > 3.25f) throw new Exception($"Zombie tracking snapped: {bones[i].name}, {change:F2} degrees/frame.");
                    before[i] = bones[i].rotation;
                }
            }
            try
            {
                float from = -150;
                foreach (float to in new[] { -175f, -185f, -210f, -185f, -175f, -150f, 150f, 175f, 185f, 210f, 150f, -95f, 95f })
                {
                    for (int frame = 1; frame <= 90; frame++)
                    {
                        Vector3 offset = Quaternion.AngleAxis(Mathf.Lerp(from, to, frame / 90f), Vector3.up) * notice.transform.forward * 4;
                        Vector3 position = notice.transform.position + offset; position.y = start.y;
                        body.position = position; player.transform.position = position;
                        Step();
                    }
                    from = to;
                }
                body.position = start; player.transform.position = start;
                for (int i = 0; i < 300; i++) Step();
                var head = bones.Single(t => t.name == "mixamorig:Head");
                Vector3 toward = (start + Vector3.up * .55f - head.position).normalized;
                if (Vector3.Dot(notice.GazeDirection.normalized, toward) < .95f) throw new Exception("Smoothed gaze did not settle back on the player.");
                Debug.Log($"ZOMBIE TRACKING PASS: 1,470 fixed-step samples, rear seam crossed both directions, sideways/front/full arc reversals; largest spine/chest/neck/head step {maximum:F2} degrees; gaze reacquired player. No scene edits saved.");
            }
            finally { body.position = start; player.transform.position = start; body.linearVelocity = velocity; }
        }
        private static void ValidateFeedingCycle()
        {
            var zombie = GameObject.Find("FeedingZombie");
            var feeding = zombie.GetComponent<FeedingZombiePose>();
            var bones = zombie.GetComponentsInChildren<Transform>();
            var feet = new[] { bones.Single(t => t.name == "mixamorig:LeftFoot"), bones.Single(t => t.name == "mixamorig:RightFoot") };
            var hands = new[] { bones.Single(t => t.name == "mixamorig:LeftHand"), bones.Single(t => t.name == "mixamorig:RightHand") };
            feeding.Evaluate(0);
            var footStart = feet.Select(t => t.position).ToArray();
            var handStart = hands.Select(t => t.position).ToArray();
            float closest = 100, farthest = 0;
            for (int i = 0; i <= 56; i++)
            {
                feeding.Evaluate(i * .05f);
                float distance = Vector3.Distance(feeding.MouthPosition, feeding.ContactPosition);
                closest = Mathf.Min(closest, distance); farthest = Mathf.Max(farthest, distance);
                if (distance > .21f) throw new Exception("Feeding mouth floats away from the body: " + distance);
                for (int j = 0; j < 2; j++)
                    if (Vector3.Distance(feet[j].position, footStart[j]) > .035f || Vector3.Distance(hands[j].position, handStart[j]) > .035f)
                        throw new Exception("Feeding feet/hands slide during the cycle.");
            }
            feeding.Evaluate(Time.time);
            if (closest > .03f || farthest < .12f) throw new Exception("Feeding needs distinct contact and pull-back phases.");
            var blood = GameObject.Find("BloodDressing");
            if (blood == null || blood.GetComponentsInChildren<Renderer>().Length < 30 || blood.GetComponentInChildren<Collider>() != null)
                throw new Exception("Blood pool/dressing missing or obstructs movement.");
            Debug.Log($"FEEDING CYCLE PASS: 57 sampled poses; mouth distance {closest:F3}–{farthest:F3}m; stable feet/braced hands; blood dressing non-colliding.");
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
        public static void Capture(string path = "ArtSource/Previews/HomeDiscovery_Unity01.png")
        {
            var camera = Camera.main; var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1440, 900, 24); var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.aspect = 1.6f;
                camera.GetComponent<Survivor.CameraSystem.RoomCamera>().RefreshFraming();
                camera.Render(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
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
