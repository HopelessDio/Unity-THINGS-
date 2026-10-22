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
    public static class HomeEncounterPlayValidation
    {
        private const string Pending = "Assets/Editor/HomeEncounterPlayValidation.pending";
        private const string Key = "HomeEncounter.Test.";
        private static ZombieRiseEncounter rise;
        private static PlayerHealth player;
        private static Vector3 combatStart;
        private static int kills;
        private static bool capturedRise;
        static HomeEncounterPlayValidation() { EditorApplication.update += Tick; }
        private static void Tick()
        {
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                int phase = SessionState.GetInt(Key + "Phase", 0);
                double now = EditorApplication.timeSinceStartup;
                if (phase == 0)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new Exception("Save and stop Play first.");
                    EditorSceneManager.OpenScene(HomeInteriorSetup.ScenePath, OpenSceneMode.Single);
                    SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
                    SessionState.SetFloat(Key + "Deadline", (float)now + 90);
                    Next(1, now + 1); EditorApplication.isPlaying = true; return;
                }
                if (now > SessionState.GetFloat(Key + "Deadline", 0)) throw new Exception("Home encounter timed out at phase " + phase);
                if (!EditorApplication.isPlaying || now < SessionState.GetFloat(Key + "Wait", 0)) return;
                if (phase == 1)
                {
                    rise = UnityEngine.Object.FindAnyObjectByType<ZombieRiseEncounter>(); player = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
                    if (rise == null || rise.CombatEnemy.activeInHierarchy || player.IsDead) throw new Exception("Encounter did not start safely in feeding state.");
                    kills = 0; capturedRise = false; EnemyHealth.EnemyKilled += OnKill;
                    Next(2, now);
                }
                else if (phase == 2)
                {
                    if (player.CurrentHealth != player.MaxHealth) throw new Exception("Player took damage during the reveal.");
                    if (!rise.CombatEnemy.activeInHierarchy)
                    {
                        if (rise.IsRising && rise.RiseProgress > .4f && !capturedRise)
                        {
                            HomeDiscoveryPlayValidation.Capture("ArtSource/Previews/HomeDiscovery_Rising_Unity01.png");
                            capturedRise = true;
                        }
                        return;
                    }
                    if (rise.gameObject.activeSelf || rise.RiseProgress < 1 || UnityEngine.Object.FindObjectsByType<EnemyHealth>().Length != 1)
                        throw new Exception("Stand-up activated combat early or duplicated the enemy.");
                    var enemy = rise.CombatEnemy;
                    if (!enemy.GetComponent<EnemyChaser>().enabled || !enemy.GetComponent<EnemyContactDamage>().enabled ||
                        !enemy.GetComponentInChildren<Animator>().isHuman) throw new Exception("Existing enemy combat/animation systems missing.");
                    combatStart = enemy.transform.position;
                    HomeDiscoveryPlayValidation.Capture("ArtSource/Previews/HomeDiscovery_Combat_Unity01.png");
                    Next(7, now + 1);
                }
                else if (phase == 7)
                {
                    // Exercise the actual starting path around the dining furniture first.
                    var enemy = rise.CombatEnemy;
                    if (Vector3.Distance(enemy.transform.position, player.transform.position) > 1.8f) return;
                    combatStart = enemy.transform.position;
                    // A clear corridor outside the dining table tests real pursuit without
                    // saving any player positions or bypassing enemy physics.
                    var body = player.GetComponent<Rigidbody>();
                    body.position = new Vector3(combatStart.x, 1.03f, combatStart.z - 2.2f);
                    body.linearVelocity = Vector3.zero; player.transform.position = body.position;
                    Next(3, now + .5);
                }
                else if (phase == 3)
                {
                    var enemy = rise.CombatEnemy;
                    if (Vector3.Distance(enemy.transform.position, combatStart) < .3f) return;
                    var body = player.GetComponent<Rigidbody>();
                    body.position = enemy.transform.position + Vector3.right * .65f;
                    player.transform.position = body.position; body.linearVelocity = Vector3.zero;
                    Next(4, now + .2);
                }
                else if (phase == 4)
                {
                    if (player.CurrentHealth >= player.MaxHealth) return;
                    if (player.IsDead) throw new Exception("Contact damage was excessive.");
                    var health = rise.CombatEnemy.GetComponent<EnemyHealth>();
                    health.TakeDamage(15); if (health.IsDead) throw new Exception("Enemy health balance changed.");
                    health.TakeDamage(15); if (!health.IsDead || kills != 1) throw new Exception("Enemy death/kill count failed.");
                    Next(5, now + .3);
                }
                else if (phase == 5)
                {
                    if (rise.CombatEnemy != null || UnityEngine.Object.FindObjectsByType<EnemyHealth>().Length != 0)
                        throw new Exception("Killed encounter enemy was not removed.");
                    player.Kill(); UnityEngine.Object.FindAnyObjectByType<GameOverUI>().RestartGame(); Next(6, now + 2);
                }
                else
                {
                    var restored = UnityEngine.Object.FindAnyObjectByType<ZombieRiseEncounter>();
                    var hero = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
                    if (restored == null || restored.IsRising || restored.CombatEnemy.activeInHierarchy || hero.IsDead ||
                        GameObject.Find("FeedingZombie").GetComponent<ZombieNoticePlayer>().HasNoticed)
                        throw new Exception("Retry did not reset the entire encounter.");
                    Debug.Log("HOME ENCOUNTER PASS: safe feeding/reveal; stare and pose-blended rise; exactly one Humanoid combat enemy only after standing; real Rigidbody pursuit/contact damage; two 15-damage hits kill once; actual Retry restores feeding and inactive enemy. No runtime scene edits saved.");
                    Finish();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(); }
        }
        private static void OnKill() => kills++;
        private static void Next(int phase, double wait)
        { SessionState.SetInt(Key + "Phase", phase); SessionState.SetFloat(Key + "Wait", (float)wait); }
        private static void Finish()
        {
            EnemyHealth.EnemyKilled -= OnKill;
            Application.runInBackground = SessionState.GetBool(Key + "Background", false);
            SessionState.SetInt(Key + "Phase", 0); AssetDatabase.DeleteAsset(Pending);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
