using System;
using System.IO;
using System.Linq;
using Survivor.Player;
using Survivor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class HomeDoorPlayValidation
    {
        private const string Pending = "Assets/Editor/HomeDoorPlayValidation.pending";
        private const string Prefix = "HomeDoor.Test.";
        private static Keyboard keyboard, previous;
        private static bool sawFade;
        static HomeDoorPlayValidation() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                int phase = SessionState.GetInt(Prefix + "Phase", 0);
                if (phase == 0)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                        throw new InvalidOperationException("Door test requires saved scenes and Edit mode.");
                    EditorSceneManager.OpenScene(HomeDoorSetup.Outside, OpenSceneMode.Single);
                    SessionState.SetFloat(Prefix + "Deadline", (float)now + 75);
                    Next(1, now + 2);
                    EditorApplication.isPlaying = true;
                    return;
                }
                if (now > SessionState.GetFloat(Prefix + "Deadline", 0)) throw new Exception($"Home door test timed out: phase={phase}, playing={EditorApplication.isPlaying}, scene={SceneManager.GetActiveScene().path}, fade={HomeDoor.IsTransitioning}, timeScale={Time.timeScale}.");
                if (!EditorApplication.isPlaying || now < SessionState.GetFloat(Prefix + "Wait", 0)) return;
                if (phase == 1)
                {
                    Debug.Log("HOME DOOR TEST: checking school/doorstep and queueing real E input.");
                    var door = UnityEngine.Object.FindAnyObjectByType<HomeDoor>();
                    var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    if (door == null || player == null || door.CanEnter || door.TryEnter()) throw new Exception("Door missing or enter allowed from school.");
                    // Move the real player to the existing home approach, not a synthetic door.
                    Vector3 point = door.transform.position + Vector3.up * 3;
                    if (!Physics.Raycast(point, Vector3.down, out var floor, 6, 1, QueryTriggerInteraction.Ignore)) throw new Exception("Door has no floor.");
                    var body = player.GetComponent<Rigidbody>();
                    body.position = new Vector3(door.transform.position.x, floor.point.y + 1.03f, door.transform.position.z);
                    player.transform.position = body.position;
                    body.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    if (Physics.CheckCapsule(body.position + Vector3.down * .5f, body.position + Vector3.up * .5f, .49f,
                        LayerMask.GetMask("ArenaObstacle"), QueryTriggerInteraction.Ignore)) throw new Exception("Door approach intersects solid geometry.");
                    if (!door.CanEnter) throw new Exception("Cannot interact from home doorstep.");
                    // Pause/death must suppress entry, then recover.
                    Time.timeScale = 0;
                    if (door.TryEnter()) throw new Exception("Door activates while paused.");
                    Time.timeScale = 1;
                    previous = Keyboard.current;
                    SessionState.SetInt(Prefix + "Input", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
                    SessionState.SetBool(Prefix + "Background", Application.runInBackground);
                    SessionState.SetBool(Prefix + "Changed", true);
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    Application.runInBackground = true;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    sawFade = false;
                    Debug.Log($"HOME DOOR TEST: keyboard ready at {body.position}; CanEnter={door.CanEnter}; player enabled={player.enabled}.");
                    // Device/focus synchronization can clear a just-added keyboard's
                    // first state event. Let it settle before pressing E.
                    Next(4, now + .4);
                }
                else if (phase == 4)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    Next(2, now);
                }
                else if (phase == 2)
                {
                    if (!sawFade && keyboard != null && !keyboard.eKey.isPressed)
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    if (!sawFade && SceneManager.GetActiveScene().path == HomeDoorSetup.Outside &&
                        now > SessionState.GetFloat(Prefix + "Wait", 0) + 5)
                    {
                        var door = UnityEngine.Object.FindAnyObjectByType<HomeDoor>();
                        throw new Exception($"Door did not respond to E: key={keyboard?.eKey.isPressed}, canEnter={door?.CanEnter}, timeScale={Time.timeScale}, player={UnityEngine.Object.FindAnyObjectByType<PlayerMovement>()?.transform.position}.");
                    }
                    if (HomeDoor.IsTransitioning)
                    {
                        sawFade = true;
                        var door = UnityEngine.Object.FindAnyObjectByType<HomeDoor>();
                        if (door != null && door.TryEnter()) throw new Exception("Repeated door load allowed during fade.");
                    }
                    if (SceneManager.GetActiveScene().path != HomeInteriorSetup.ScenePath || HomeDoor.IsTransitioning) return;
                    if (!sawFade) throw new Exception("Scene changed without fade.");
                    ReleaseInput();
                    ValidateHome();
                    UnityEngine.Object.FindAnyObjectByType<PlayerHealth>().Kill();
                    if (Time.timeScale != 0) throw new Exception("Interior death UI not wired.");
                    UnityEngine.Object.FindAnyObjectByType<GameOverUI>().RestartGame();
                    Next(3, now + 2);
                }
                else if (phase == 3)
                {
                    ValidateHome();
                    Debug.Log("HOME DOOR PLAY PASS: school cannot enter remotely; doorstep is clear and paved; paused entry blocked; real E input triggers one fade/load; equipped player and mother present at interior entry; no duplicate players/fade; real death/Retry remains inside home.");
                    Finish();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(); }
        }
        private static void ValidateHome()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            var entry = GameObject.Find("Home_EntryMarker");
            if (SceneManager.GetActiveScene().path != HomeInteriorSetup.ScenePath || player == null || entry == null ||
                GameObject.Find("Mother") == null || Time.timeScale != 1 || player.GetComponent<PlayerHealth>().IsDead ||
                UnityEngine.Object.FindObjectsByType<PlayerMovement>().Length != 1 || HomeDoor.IsTransitioning ||
                UnityEngine.Object.FindAnyObjectByType<DoorFade>() != null || Vector3.Distance(player.transform.position, entry.transform.position) > .2f)
                throw new Exception("Home arrival/Retry state incorrect.");
            var visual = player.transform.Find("VeilWalkerVisual");
            if (visual == null || visual.GetComponent<PlayerWeaponEquipment>().EquippedSword == null ||
                !visual.GetComponent<GreatSwordLocomotion>().IsReady) throw new Exception("Player visual/sword/locomotion missing after door.");
        }
        private static void Next(int phase, double wait)
        {
            SessionState.SetInt(Prefix + "Phase", phase);
            SessionState.SetFloat(Prefix + "Wait", (float)wait);
        }
        private static void ReleaseInput()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (previous != null && previous.added) previous.MakeCurrent();
            if (!SessionState.GetBool(Prefix + "Changed", false)) return;
            InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Prefix + "Input", 0);
            Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
            SessionState.SetBool(Prefix + "Changed", false);
        }
        private static void Finish()
        {
            ReleaseInput();
            SessionState.SetInt(Prefix + "Phase", 0);
            AssetDatabase.DeleteAsset(Pending);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
