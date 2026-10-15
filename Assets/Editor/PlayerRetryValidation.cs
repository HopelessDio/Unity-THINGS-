using System;
using System.IO;
using Survivor.Combat;
using Survivor.Player;
using Survivor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    /// <summary>Explicit one-shot play-mode regression test, gated by a pending file.</summary>
    [InitializeOnLoad]
    public static class PlayerRetryValidation
    {
        private const string Pending = "Assets/Editor/PlayerRetryValidation.pending";
        private const string PhaseKey = "Survivor.RetryTest.Phase";
        private const string WaitKey = "Survivor.RetryTest.Wait";
        private const string DeadlineKey = "Survivor.RetryTest.Deadline";
        private const string RunningKey = "Survivor.RetryTest.WasPlaying";
        private const string LevelKey = "Survivor.RetryTest.LevelOneExpected";

        static PlayerRetryValidation() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                int phase = SessionState.GetInt(PhaseKey,0);
                float now = (float)EditorApplication.timeSinceStartup;
                if (phase == 0)
                {
                    Scene scene = SceneManager.GetActiveScene();
                    if (scene.name != "Game") throw new InvalidOperationException("Retry validation only runs in the Game scene.");
                    SessionState.SetBool(RunningKey,EditorApplication.isPlaying);
                    SessionState.SetBool(LevelKey,GameObject.Find("Level01_CrimsonVillage")!=null);
                    SessionState.SetInt(PhaseKey,1);
                    SessionState.SetFloat(WaitKey,now+.6f);
                    SessionState.SetFloat(DeadlineKey,now+20f);
                    if (!EditorApplication.isPlaying) EditorApplication.isPlaying=true;
                    return;
                }
                if (now > SessionState.GetFloat(DeadlineKey,0f))
                    throw new InvalidOperationException("Retry validation timed out while waiting for play/scene reload.");
                if (!EditorApplication.isPlaying || now < SessionState.GetFloat(WaitKey,0f)) return;
                Scene active = SceneManager.GetActiveScene();
                if (!active.isLoaded) return;
                GameObject player = null;
                GameOverUI retry = null;
                foreach (GameObject root in active.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (child.name=="Player3d") player=child.gameObject;
                    if (retry==null) retry=root.GetComponentInChildren<GameOverUI>(true);
                }
                if (player==null || retry==null) throw new InvalidOperationException("Retry test cannot locate player/UI.");
                if(SessionState.GetBool(LevelKey,false))
                {
                    Camera camera=Camera.main;
                    if(GameObject.Find("Level01_CrimsonVillage")==null || camera==null ||
                        camera.GetComponent<Survivor.CameraSystem.LevelOneCamera>()==null ||
                        camera.transform.Find("Level01_PictureBackdrop")==null ||
                        !Physics.Raycast(player.transform.position,Vector3.down,2,1,QueryTriggerInteraction.Ignore))
                        throw new InvalidOperationException($"Retry phase {phase}: Level 1 map/floor/camera/background did not reload.");
                    Debug.Log($"LEVEL01 RETRY PASS phase {phase}: village, spawn floor, shifted camera and backdrop retained.");
                }
                Transform visual=player.transform.Find("VeilWalkerVisual");
                int count=0;
                foreach (Transform child in player.transform) if(child.name=="VeilWalkerVisual") count++;
                if(visual==null || count!=1 || !visual.gameObject.activeInHierarchy)
                    throw new InvalidOperationException($"Retry phase {phase}: player must have exactly one active Veil Walker.");
                SkinnedMeshRenderer skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();
                PlayerWeaponEquipment gear=visual.GetComponent<PlayerWeaponEquipment>();
                PlayerHealth health=player.GetComponent<PlayerHealth>();
                Animator humanoid=visual.GetComponent<Animator>();
                if(humanoid!=null && humanoid.avatar!=null && humanoid.avatar.isHuman &&
                    (visual.GetComponent<GreatSwordLocomotion>()==null || !visual.GetComponent<GreatSwordLocomotion>().IsReady))
                    throw new InvalidOperationException($"Retry phase {phase}: Humanoid movement graph missing.");
                if(skin==null || !skin.enabled || skin.bounds.size.y<.5f || gear==null || gear.EquippedSword==null ||
                    visual.GetComponent<VeilWalkerLocomotion>()==null || !visual.GetComponent<VeilWalkerLocomotion>().IsReady ||
                    visual.GetComponent<PlayerSwordAttack>()==null || visual.GetComponent<PlayerDashAfterimages>()==null ||
                    health.IsDead || health.CurrentHealth<=0f)
                    throw new InvalidOperationException($"Retry phase {phase}: visual, sword, animation, combat or fresh health missing.");
                Debug.Log($"Retry regression phase {phase} passed: one visible character, sword, locomotion, combat, afterimages and fresh health.");
                if(phase==3)
                {
                    Debug.Log("Player Retry regression passed: initial Play plus two actual death/Retry scene reloads; character stayed visible with sword and gameplay components.");
                    Finish();
                    return;
                }
                SessionState.SetInt(PhaseKey,phase+1);
                SessionState.SetFloat(WaitKey,now+.6f);
                health.Kill();
                retry.RestartGame();
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
                Finish();
            }
        }

        private static void Finish()
        {
            bool leavePlaying=SessionState.GetBool(RunningKey,false);
            SessionState.SetInt(PhaseKey,0);
            AssetDatabase.DeleteAsset(Pending);
            if(File.Exists(Pending)) File.Delete(Pending);
            Time.timeScale=1f;
            if(!leavePlaying && EditorApplication.isPlaying) EditorApplication.isPlaying=false;
        }
    }
}
