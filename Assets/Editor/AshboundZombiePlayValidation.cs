using System;
using System.IO;
using Survivor.Enemies;
using Survivor.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Survivor.EditorTools
{
    /// <summary>Temporary, distant Play-mode fixture. No level/spawner changes are saved.</summary>
    [InitializeOnLoad]
    public static class AshboundZombiePlayValidation
    {
        private const string Pending = "Assets/Editor/AshboundZombiePlayValidation.pending";
        private const string Active = "AshboundZombieValidation.Active";
        private const string Batch = "AshboundZombieValidation.Batch";
        private const string Failed = "AshboundZombieValidation.Failed";
        private static GameObject fixture, enemy;
        private static PlayerHealth target;
        private static Vector3 start;
        private static double since;
        private static int phase, kills;
        private static float initialDistance;
        private static Color[] colors;

        static AshboundZombiePlayValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
        }

        public static void RunBatch()
        {
            SessionState.SetBool(Batch, true);
            SessionState.SetBool(Failed, false);
            // Loading is read-only. Preserve the user's saved scene and spawner settings.
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            Begin();
        }

        public static void BuildAndRunBatch()
        {
            AshboundZombieSetup.Install();
            RunBatch();
        }

        [MenuItem("Survivor/Enemies/Validate Ashbound Zombie in Play Mode")]
        private static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start zombie validation from Edit mode.");
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                since = EditorApplication.timeSinceStartup;
                phase = 0;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Active, false);
                AssetDatabase.DeleteAsset(Pending);
                if (SessionState.GetBool(Batch, false))
                {
                    int code = SessionState.GetBool(Failed, false) ? 1 : 0;
                    SessionState.SetBool(Batch, false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(code);
                }
            }
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!SessionState.GetBool(Active, false))
            {
                if (File.Exists(Pending) && !EditorApplication.isPlayingOrWillChangePlaymode) Begin();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - since;
                if (elapsed > 25) throw new InvalidOperationException("Zombie validation timed out.");
                if (phase == 0 && elapsed > 1)
                {
                    if (Time.timeScale == 0) throw new InvalidOperationException("Play mode is paused by game-over; retry first.");
                    fixture = new GameObject("AshboundValidation_RuntimeOnly");
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.SetParent(fixture.transform);
                    floor.transform.position = new Vector3(2000,-.25f,2000);
                    floor.transform.localScale = new Vector3(16,.5f,16);
                    var dummy = new GameObject("ZombieTarget_ValidationOnly");
                    dummy.transform.SetParent(fixture.transform);
                    dummy.transform.position = new Vector3(2000,1,2003);
                    dummy.AddComponent<CapsuleCollider>().height = 2;
                    var rb = dummy.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    target = dummy.AddComponent<PlayerHealth>();
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AshboundZombieSetup.PrefabPath);
                    enemy = UnityEngine.Object.Instantiate(prefab, new Vector3(2000,1.08f,1999), Quaternion.identity);
                    enemy.transform.SetParent(fixture.transform);
                    enemy.GetComponent<AshboundZombie>().SetPlayerTarget(target);
                    start = enemy.transform.position;
                    initialDistance = Vector3.Distance(start, target.transform.position);
                    colors = Array.ConvertAll(enemy.GetComponentInChildren<Renderer>().sharedMaterials, m => m.color);
                    phase = 1;
                    since = EditorApplication.timeSinceStartup;
                }
                else if (phase == 1 && elapsed > 1.2)
                {
                    if (Vector3.Distance(start,enemy.transform.position) < .7f ||
                        Vector3.Distance(enemy.transform.position,target.transform.position) > initialDistance-.6f)
                        throw new InvalidOperationException("Zombie did not pursue its target.");
                    var animator = enemy.GetComponentInChildren<Animator>();
                    if (animator.GetFloat("Speed") < .4f || animator.applyRootMotion)
                        throw new InvalidOperationException("Zombie locomotion is not driven by Rigidbody speed.");
                    enemy.GetComponent<EnemyHitFlash>().Play();
                    enemy.GetComponent<EnemyHitFlash>().Play();
                    var renderer = enemy.GetComponentInChildren<Renderer>();
                    var block = new MaterialPropertyBlock();
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        renderer.GetPropertyBlock(block,i);
                        if (block.GetColor("_BaseColor") != Color.white)
                            throw new InvalidOperationException("Not every zombie material flashes on hit.");
                        if (renderer.sharedMaterials[i].color != colors[i])
                            throw new InvalidOperationException("Hit flash mutated a shared material.");
                    }
                    phase = 2;
                    since = EditorApplication.timeSinceStartup;
                }
                else if (phase == 2 && elapsed > .2)
                {
                    var renderer = enemy.GetComponentInChildren<Renderer>();
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block,0);
                    if (!block.isEmpty) throw new InvalidOperationException("Hit flash did not restore its property block.");
                    // Let pursuit reach the capsule to exercise actual collision/contact damage.
                    if (target.CurrentHealth == target.MaxHealth)
                    {
                        if (elapsed > 6) throw new InvalidOperationException("Zombie contact damage did not reach the target.");
                        return;
                    }
                    if (target.CurrentHealth != 90f) throw new InvalidOperationException("Unexpected contact damage.");
                    enemy.GetComponent<EnemyChaser>().ApplyKnockback(Vector3.back*5f,.25f);
                    start = enemy.transform.position;
                    phase = 3;
                    since = EditorApplication.timeSinceStartup;
                }
                else if (phase == 3 && elapsed > .16)
                {
                    if (enemy.transform.position.z > start.z-.15f)
                        throw new InvalidOperationException("Zombie did not respond to knockback.");
                    kills = 0;
                    EnemyHealth.EnemyKilled += CountKill;
                    var health = enemy.GetComponent<EnemyHealth>();
                    health.TakeDamage(15);
                    if (health.IsDead) throw new InvalidOperationException("Zombie died before the second 15-damage hit.");
                    health.TakeDamage(15);
                    health.TakeDamage(15);
                    if (!health.IsDead || kills != 1)
                        throw new InvalidOperationException("Zombie death/kill event did not fire exactly once.");
                    phase = 4;
                    since = EditorApplication.timeSinceStartup;
                }
                else if (phase == 4 && elapsed > .2)
                {
                    if (enemy != null) throw new InvalidOperationException("Dead zombie was not removed.");
                    Debug.Log("ASHBOUND PLAY PASS: pursuit, velocity-driven shuffle, all-material flash and restore, 10 contact damage, knockback, two 15-damage hits, one kill event, enemy cleanup. Temporary test only; no scene/spawner changes saved.");
                    Finish();
                }
            }
            catch (Exception e)
            {
                SessionState.SetBool(Failed, true);
                Debug.LogException(e);
                Finish();
            }
        }

        private static void CountKill() { kills++; }
        private static void Cleanup()
        {
            EnemyHealth.EnemyKilled -= CountKill;
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
        }
        private static void Finish()
        {
            Cleanup();
            AssetDatabase.DeleteAsset(Pending);
            EditorApplication.isPlaying = false;
        }
    }
}
