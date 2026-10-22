using System;
using System.IO;
using Survivor.Enemies;
using Survivor.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class EnemyDashSpinValidation
    {
        private const string Pending = "Assets/Editor/EnemyDashSpinValidation.pending", Key = "EnemyDashSpin.Test.";
        private static GameObject fixture, enemy;
        private static PlayerMovement player;
        private static Keyboard keyboard, previous;
        private static EnemyDashSpinProbe probe;
        private static float shotAt;
        private static int shots, dashes;
        private static Vector3 knockbackStart;
        static EnemyDashSpinValidation() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                int phase = SessionState.GetInt(Key + "Phase", 0);
                if (phase == 0)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                        throw new Exception("Save and stop Play before dash validation.");
                    SessionState.SetBool(Key + "Background", Application.runInBackground);
                    SessionState.SetInt(Key + "Input", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
                    SessionState.SetBool(Key + "Changed", true);
                    Application.runInBackground = true;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    SessionState.SetFloat(Key + "Deadline", (float)now + 60);
                    Next(1, now + 1); EditorApplication.isPlaying = true; return;
                }
                if (now > SessionState.GetFloat(Key + "Deadline", 0)) throw new Exception("Enemy dash-spin test timed out, phase " + phase);
                if (!EditorApplication.isPlaying || now < SessionState.GetFloat(Key + "Wait", 0)) return;
                if (phase == 1)
                {
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    fixture = new GameObject("DashSpin_RuntimeOnly");
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(fixture.transform);
                    floor.transform.position = new Vector3(2000, -.25f, 2000); floor.transform.localScale = new Vector3(20, .5f, 20);
                    enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AshboundZombieSetup.PrefabPath),
                        new Vector3(2000, 1.08f, 2000), Quaternion.identity, fixture.transform);
                    enemy.GetComponent<AshboundZombie>().SetPlayerTarget(player.GetComponent<PlayerHealth>());
                    probe = enemy.AddComponent<EnemyDashSpinProbe>(); probe.PlayerBody = player.GetComponent<Rigidbody>();
                    previous = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    shots = dashes = 0; player.DashStarted += OnDash;
                    Next(2, now + .4);
                }
                else if (phase == 2)
                {
                    Vector3 forward = player.WorldInput(Vector2.up).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    var body = enemy.GetComponent<Rigidbody>(); body.position = new Vector3(2000, 1.08f, 2000);
                    body.rotation = Quaternion.LookRotation(-forward); body.linearVelocity = Vector3.zero;
                    var hero = player.GetComponent<Rigidbody>(); hero.position = body.position - forward * 1.5f + right * (shots % 2 == 0 ? .22f : -.22f);
                    hero.linearVelocity = Vector3.zero; player.transform.position = hero.position;
                    probe.ResetSample(); Physics.SyncTransforms();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.Space));
                    shotAt = Time.time; shots++; Next(3, now);
                }
                else if (phase == 3)
                {
                    if (Time.time - shotAt < 1.15f) return;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (shots < 3) { Next(2, now + .15); return; }
                    if (dashes != 3 || probe.Contacts < 1 || probe.PeakStep < .5f || probe.PeakStep > 5.5f || probe.PeakAngularSpeed > 5.5f)
                        throw new Exception($"Dash-spin regression: dashes={dashes}, contacts={probe.Contacts}, step={probe.PeakStep}, angular={probe.PeakAngularSpeed}.");
                    if ((enemy.GetComponent<Rigidbody>().constraints & RigidbodyConstraints.FreezeRotation) != RigidbodyConstraints.FreezeRotation)
                        throw new Exception("Enemy rotation constraints were not applied.");
                    enemy.GetComponent<AshboundZombie>().SetPlayerTarget(null);
                    knockbackStart = enemy.transform.position;
                    enemy.GetComponent<EnemyChaser>().ApplyKnockback(Vector3.right * 5, .3f);
                    shotAt = Time.time; Next(4, now);
                }
                else
                {
                    if (Time.time - shotAt < .2f) return;
                    if (enemy.transform.position.x - knockbackStart.x < .2f) throw new Exception("Rotation lock broke linear knockback.");
                    Debug.Log($"ENEMY DASH SPIN PASS: 3 real Space dashes with glancing collision contacts={probe.Contacts}; peak yaw step={probe.PeakStep:F2} degrees/fixed frame, angular speed={probe.PeakAngularSpeed:F2} rad/s; scripted yaw still turns, no physics spin/tilt, linear knockback retained. No saved scene edits.");
                    Finish();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(); }
        }
        private static void OnDash(float duration) => dashes++;
        private static void Next(int phase, double wait) { SessionState.SetInt(Key + "Phase", phase); SessionState.SetFloat(Key + "Wait", (float)wait); }
        private static void Finish()
        {
            if (player != null) player.DashStarted -= OnDash;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null; if (previous != null && previous.added) previous.MakeCurrent();
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            if (SessionState.GetBool(Key + "Changed", false))
            {
                Application.runInBackground = SessionState.GetBool(Key + "Background", false);
                InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Key + "Input", 0);
                SessionState.SetBool(Key + "Changed", false);
            }
            SessionState.SetInt(Key + "Phase", 0); AssetDatabase.DeleteAsset(Pending);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }

}
