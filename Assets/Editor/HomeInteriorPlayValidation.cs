using System;
using System.IO;
using System.Linq;
using Survivor.CameraSystem;
using Survivor.Enemies;
using Survivor.Player;
using Survivor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    /// <summary>One-shot movement, dash and real Retry test in the separate home scene.</summary>
    [InitializeOnLoad]
    public static class HomeInteriorPlayValidation
    {
        private const string Pending = "Assets/Editor/HomeInteriorPlayValidation.pending";
        private const string Phase = "HomeInterior.Test.Phase";
        private const string Wait = "HomeInterior.Test.Wait";
        private const string Deadline = "HomeInterior.Test.Deadline";
        private const string InputMode = "HomeInterior.Test.InputMode";
        private const string InputChanged = "HomeInterior.Test.InputChanged";
        private const string Background = "HomeInterior.Test.Background";
        private static Keyboard keyboard,previousKeyboard;
        private static PlayerMovement player;
        private static Vector3 start,cameraStart,dashStart;
        private static bool dashObserved;

        static HomeInteriorPlayValidation() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                int phase = SessionState.GetInt(Phase,0);
                double now = EditorApplication.timeSinceStartup;
                if (phase == 0)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != HomeInteriorSetup.ScenePath)
                        throw new InvalidOperationException("Home test starts in Edit mode in the home interior.");
                    SessionState.SetInt(Phase,1);
                    SessionState.SetFloat(Wait,(float)now+2f);
                    SessionState.SetFloat(Deadline,(float)now+60f);
                    EditorApplication.isPlaying = true;
                    return;
                }
                if (now > SessionState.GetFloat(Deadline,0)) throw new InvalidOperationException("Home Play validation timed out.");
                if (!EditorApplication.isPlaying || now < SessionState.GetFloat(Wait,0)) return;
                var camera = Camera.main;
                if (phase == 1)
                {
                    ValidateRuntime();
                    HomeInteriorSetup.RenderPreview(camera);
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    start = player.transform.position;
                    cameraStart = camera.transform.position;
                    Vector3 forward = camera.transform.forward; forward.y = 0; forward.Normalize();
                    if (Vector3.Dot(forward,player.WorldInput(Vector2.up)) < .99f)
                        throw new InvalidOperationException("Interior WASD is not camera-relative.");
                    previousKeyboard = Keyboard.current;
                    SessionState.SetInt(InputMode,(int)InputSystem.settings.editorInputBehaviorInPlayMode);
                    SessionState.SetBool(Background,Application.runInBackground);
                    SessionState.SetBool(InputChanged,true);
                    Application.runInBackground = true;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                    SessionState.SetInt(Phase,2);
                    SessionState.SetFloat(Wait,(float)now+.65f);
                    return;
                }
                if (phase == 2)
                {
                    if (Vector3.Dot(player.transform.position-start,player.WorldInput(Vector2.up)) < .6f)
                        throw new InvalidOperationException($"Player did not walk: start={start}, now={player.transform.position}, W={keyboard.wKey.isPressed}, direction={player.WorldInput(Vector2.up)}, velocity={player.GetComponent<Rigidbody>().linearVelocity}, enabled={player.enabled}, timeScale={Time.timeScale}, scenes={SceneManager.sceneCount}.");
                    dashObserved = false;
                    dashStart = player.transform.position;
                    player.DashStarted += ObserveDash;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space));
                    SessionState.SetInt(Phase,3);
                    SessionState.SetFloat(Wait,(float)now+.30f);
                    return;
                }
                if (phase == 3)
                {
                    if (!dashObserved || Vector3.Distance(dashStart,player.transform.position) < .8f)
                        throw new InvalidOperationException("Dash failed in the home.");
                    if (Vector3.Distance(cameraStart,camera.transform.position) > .001f)
                        throw new InvalidOperationException("Room camera moved with the player; expected stable framing.");
                    if (!OnFloor(player.transform.position) || player.GetComponent<PlayerHealth>().IsDead)
                        throw new InvalidOperationException("Player left the home floor.");
                    ReleaseKeyboard();
                    player.DashStarted -= ObserveDash;
                    var retry = UnityEngine.Object.FindAnyObjectByType<GameOverUI>();
                    player.GetComponent<PlayerHealth>().Kill();
                    if (Time.timeScale != 0) throw new InvalidOperationException("Copied game-over UI is not linked to the interior player.");
                    SessionState.SetInt(Phase,4);
                    SessionState.SetFloat(Wait,(float)now+1f);
                    retry.RestartGame();
                    return;
                }
                if (phase == 4)
                {
                    if (SceneManager.GetActiveScene().path != HomeInteriorSetup.ScenePath)
                        throw new InvalidOperationException("Retry loaded a different level.");
                    ValidateRuntime();
                    var restored = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    if (restored == player || restored.GetComponent<PlayerHealth>().IsDead)
                        throw new InvalidOperationException("Retry did not recreate a living player.");
                    var entry = GameObject.Find("Home_EntryMarker");
                    if (Vector3.Distance(restored.transform.position,entry.transform.position) > .15f)
                        throw new InvalidOperationException("Retry lost the interior spawn.");
                    HomeInteriorSetup.RenderPreview(Camera.main);
                    Debug.Log("HOME PLAY PASS: mother and equipped Veil Walker present, real WASD walking and Space dash, continuous floor, fixed room camera, no enemies, correctly wired game-over and actual Retry reload into the same interior. No outdoor edits saved.");
                    Finish();
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(); }
        }

        private static void ValidateRuntime()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            var mother = GameObject.Find("Mother");
            var room = GameObject.Find("HomeInterior_Environment");
            if (player == null || mother == null || room == null || Camera.main == null || Camera.main.GetComponent<RoomCamera>() == null)
                throw new InvalidOperationException("Home runtime objects missing.");
            if (UnityEngine.Object.FindObjectsByType<PlayerMovement>().Length != 1 ||
                UnityEngine.Object.FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include).Length != 0 ||
                UnityEngine.Object.FindObjectsByType<EnemyChaser>().Length != 0)
                throw new InvalidOperationException("Home has duplicate players, enemies or a spawner.");
            Transform visual = player.transform.Find("VeilWalkerVisual");
            if (visual == null || visual.GetComponent<PlayerWeaponEquipment>().EquippedSword == null ||
                !visual.GetComponent<GreatSwordLocomotion>().IsReady || !OnFloor(player.transform.position))
                throw new InvalidOperationException("Home player rig, sword, locomotion or floor missing.");
            var animator = mother.GetComponentInChildren<Animator>();
            var skin = mother.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!animator.isHuman || animator.runtimeAnimatorController == null || skin.sharedMaterials.Length != 1 ||
                mother.GetComponent<EnemyHealth>() != null || skin.bounds.size.y < 1.4f || skin.bounds.size.y > 2.0f)
                throw new InvalidOperationException("Mother's noncombatant Humanoid prefab is incorrect.");
            if (room.GetComponentsInChildren<BoxCollider>().Count(c => c.name == "Room boundary") != 4)
                throw new InvalidOperationException("Cutaway wall collision is missing.");
        }

        private static bool OnFloor(Vector3 p) => Physics.RaycastAll(p,Vector3.down,3,1,QueryTriggerInteraction.Ignore)
            .Any(hit => hit.collider.name == "Continuous floor");
        private static void ObserveDash(float duration) { dashObserved = true; }
        private static void ReleaseKeyboard()
        {
            if (keyboard != null && keyboard.added)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                InputSystem.RemoveDevice(keyboard);
            }
            keyboard = null;
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (SessionState.GetBool(InputChanged,false))
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(InputMode,0);
                Application.runInBackground = SessionState.GetBool(Background,false);
                SessionState.SetBool(InputChanged,false);
            }
        }
        private static void Finish()
        {
            if (player != null) player.DashStarted -= ObserveDash;
            ReleaseKeyboard();
            SessionState.SetInt(Phase,0);
            AssetDatabase.DeleteAsset(Pending);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
