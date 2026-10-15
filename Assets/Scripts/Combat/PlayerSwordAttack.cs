using System.Collections.Generic;
using Survivor.Enemies;
using Survivor.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Survivor.Combat
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class PlayerSwordAttack : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float sweepDamage = 15f;
        [SerializeField, Min(.1f)] private float reach = 2.5f;
        [SerializeField, Min(.05f)] private float comboGrace = .3f;
        [Header("Soft attack targeting")]
        [SerializeField, Range(0f, 85f)] private float assistHalfAngle = 70f;
        [SerializeField, Min(0f)] private float assistRangePadding = .35f;
        private PlayerMovement movement;
        private PlayerAim aim;
        private PlayerHealth health;
        private VeilWalkerLocomotion locomotion;
        private PlayerWeaponEquipment equipment;
        private Transform player;
        private TrailRenderer slash;
        private SwordSweepVfx sweepVfx;
        private int sweepSlot=-1;
        public const int ComboLength=2;
        private readonly Collider[] overlap = new Collider[128];
        private readonly HashSet<EnemyHealth> hitThisStrike = new HashSet<EnemyHealth>();
        private float elapsed, previousProgress, lastFinish = float.NegativeInfinity;
        private int strike, nextStrike;
        private bool queued;
        private int blockers;
        private Vector3 intendedDirection;

        public bool IsAttacking { get; private set; }
        public bool IsFacingLocked => IsAttacking && elapsed / Duration(strike) <= SwingEnd(strike);
        public Vector3 AttackDirection { get; private set; }
        public int CurrentStrike => strike;
        public float DamageForStrike(int index) => sweepDamage;
        public static float Duration(int index) => index == 1 ? .4f : .42f;
        public static float WindupEnd(int index) => index == 1 ? .25f : .28f;
        public static float SwingEnd(int index) => .7f;

        public void Initialize()
        {
            movement = GetComponentInParent<PlayerMovement>();
            aim = GetComponentInParent<PlayerAim>();
            health = GetComponentInParent<PlayerHealth>();
            locomotion = GetComponent<VeilWalkerLocomotion>();
            equipment = GetComponent<PlayerWeaponEquipment>();
            if (movement == null || locomotion == null || equipment == null) return;
            player = movement.transform;
            blockers = LayerMask.GetMask("ArenaObstacle");
            sweepVfx=GetComponent<SwordSweepVfx>();
            if(sweepVfx==null) sweepVfx=gameObject.AddComponent<SwordSweepVfx>();
            if (slash != null || equipment.EquippedSword == null) return;
            Transform socket = equipment.SwordSocket;
            Bounds blade = equipment.EquippedSword.GetComponentInChildren<Renderer>().bounds;
            float length = 0f;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                length = Mathf.Max(length, Vector3.Dot(blade.center +
                    Vector3.Scale(blade.extents, new Vector3(x,y,z)) - socket.position, socket.forward));
            GameObject tip = new GameObject("SwordSlashTip");
            tip.transform.SetParent(socket, false);
            tip.transform.position = socket.position + socket.forward * Mathf.Clamp(length, .4f, 2.5f);
            tip.layer = gameObject.layer;
            slash = tip.AddComponent<TrailRenderer>();
            slash.sharedMaterial = Resources.Load<Material>("DashIvoryStreak");
            slash.time = .075f;
            slash.widthMultiplier = .09f;
            slash.widthCurve = AnimationCurve.Linear(0f,1f,1f,0f);
            slash.minVertexDistance = .025f;
            slash.numCornerVertices = 4;
            slash.numCapVertices = 3;
            slash.shadowCastingMode = ShadowCastingMode.Off;
            slash.receiveShadows = false;
            slash.emitting = false;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white,0f), new GradientColorKey(new Color(1f,.8f,.7f),1f) },
                new[] { new GradientAlphaKey(.65f,0f), new GradientAlphaKey(0f,1f) });
            slash.colorGradient = fade;
        }

        private void Update()
        {
            if (player == null) Initialize();
            if (player == null || Time.timeScale <= 0f) return;
            if (!CanAttack()) { Cancel(); return; }
            if (IsAttacking)
            {
                elapsed += Time.deltaTime;
                // Correct aim gently during preparation, never swing around
                // chasing a target after the blade has committed to the strike.
                if (elapsed / Duration(strike) < WindupEnd(strike))
                {
                    if (aim != null && aim.UsesMouseAim) intendedDirection = aim.InputDirection;
                    AttackDirection = ResolveAttackDirection(intendedDirection, aim != null && aim.UsesMouseAim);
                }
            }
            bool mouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool padClick = Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame;
            if (mouseClick && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) mouseClick = false;
            if (mouseClick || padClick) RequestAttack();
        }

        private bool CanAttack() => movement != null && movement.enabled && !movement.IsDashing &&
            (health == null || !health.IsDead) && equipment != null && equipment.EquippedSword != null;

        public void RequestAttack()
        {
            if (!CanAttack()) return;
            if (IsAttacking)
            {
                // A click in recovery starts the next wind-up from the current
                // pose rather than snapping back to a held follow-through.
                if (elapsed / Duration(strike) > SwingEnd(strike)) Begin((strike+1)%ComboLength);
                else queued = true;
                return;
            }
            Begin(Time.time - lastFinish <= comboGrace ? nextStrike : 0);
        }

        private void Begin(int index)
        {
            // Preserve the incoming guard/follow-through instead of resetting
            // the weapon to locomotion's guard between queued combo strikes.
            locomotion.CaptureSwordAttackStart();
            if(sweepSlot>=0 && sweepVfx!=null) sweepVfx.Release(sweepSlot);
            sweepSlot=-1;
            strike = index % ComboLength;
            elapsed = previousProgress = 0f;
            queued = false;
            hitThisStrike.Clear();
            Vector3 input = aim != null ? aim.InputDirection : movement.FacingDirection;
            intendedDirection = input.sqrMagnitude > .001f ? input.normalized : player.forward;
            AttackDirection = ResolveAttackDirection(intendedDirection, aim != null && aim.UsesMouseAim);
            IsAttacking = true;
            if (slash != null) slash.Clear();
        }

        public Vector3 ResolveAttackDirection(Vector3 intended, bool mouseAiming = false)
        {
            intended.y = 0f;
            if (intended.sqrMagnitude < .001f) intended = player.forward;
            intended.Normalize();
            float radius = reach + assistRangePadding;
            int count = Physics.OverlapSphereNonAlloc(player.position,radius,overlap,~0,QueryTriggerInteraction.Collide);
            Collider[] candidates = count == overlap.Length
                ? Physics.OverlapSphere(player.position,radius,~0,QueryTriggerInteraction.Collide) : overlap;
            if (candidates != overlap) count = candidates.Length;
            float bestScore = float.NegativeInfinity;
            Vector3 bestDirection = intended;
            // Keep cursor aiming precise: broad keyboard assist must not pull a
            // mouse-directed strike sideways toward an unrelated enemy.
            float minimumDot = Mathf.Cos((mouseAiming ? Mathf.Min(assistHalfAngle,12f) : assistHalfAngle) * Mathf.Deg2Rad);
            for (int i=0;i<count;i++)
            {
                Collider candidate = candidates[i];
                EnemyHealth enemy = candidate.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead) continue;
                Vector3 delta = candidate.bounds.center - player.position;
                if (Mathf.Abs(delta.y) > 1.75f) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude < .0001f) continue;
                float distance = delta.magnitude;
                Vector3 direction = delta / distance;
                float alignment = Vector3.Dot(intended,direction);
                if (alignment < minimumDot || distance > radius) continue;
                Vector3 closest = candidate.ClosestPoint(player.position)-player.position;
                closest.y=0f;
                if (closest.sqrMagnitude > reach*reach) continue;
                if (Physics.Linecast(player.position,candidate.bounds.center,blockers,QueryTriggerInteraction.Ignore)) continue;
                // Direction matters more than distance: a very close enemy
                // behind the player must not steal the intended attack.
                float score = alignment * .7f + (1f-distance/radius) * .3f;
                if (score <= bestScore) continue;
                bestScore = score;
                bestDirection = direction;
            }
            return bestDirection;
        }

        private void LateUpdate()
        {
            if (!IsAttacking) return;
            if (!CanAttack()) { Cancel(); return; }
            float progress = Mathf.Clamp01(elapsed / Duration(strike));
            locomotion.SampleSwordAttack(strike, progress, queued);
            bool activeWindow = progress >= WindupEnd(strike) && progress <= SwingEnd(strike);
            if(progress>=WindupEnd(strike) && previousProgress<=SwingEnd(strike))
            {
                if(sweepSlot<0) sweepSlot=sweepVfx.BeginSweep(strike,player.position,AttackDirection,reach);
                sweepVfx.SetProgress(sweepSlot,Mathf.InverseLerp(WindupEnd(strike),SwingEnd(strike),progress));
            }
            if(progress>SwingEnd(strike) && sweepSlot>=0) sweepVfx.Release(sweepSlot);
            if (slash != null) slash.emitting = activeWindow;
            // Also covers an active window crossed by a low-framerate update.
            if (progress >= WindupEnd(strike) && previousProgress <= SwingEnd(strike)) DealHits();
            previousProgress = progress;
            if (elapsed < Duration(strike)) return;
            IsAttacking = false;
            lastFinish = Time.time;
            nextStrike = (strike + 1) % ComboLength;
            if (slash != null) slash.emitting = false;
            if (queued) Begin(nextStrike);
            else locomotion.ClearSwordAttackStart();
        }

        public static bool InsideStrike(Vector3 delta, Vector3 facing, float range, bool heavy)
        {
            delta.y = 0f;
            if (delta.sqrMagnitude > range * range) return false;
            if (delta.sqrMagnitude < .0001f) return true;
            return Vector3.Dot(delta.normalized, facing.normalized) >= Mathf.Cos((heavy ? 30f : 65f) * Mathf.Deg2Rad);
        }

        private void DealHits()
        {
            int count = Physics.OverlapSphereNonAlloc(player.position, reach, overlap, ~0, QueryTriggerInteraction.Collide);
            // Dense fights must not silently lose enemies beyond the fixed buffer.
            Collider[] candidates = count == overlap.Length
                ? Physics.OverlapSphere(player.position, reach, ~0, QueryTriggerInteraction.Collide) : overlap;
            if (candidates != overlap) count = candidates.Length;
            for (int i = 0; i < count; i++)
            {
                Collider collider = candidates[i];
                EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || hitThisStrike.Contains(enemy)) continue;
                Vector3 nearest = collider.ClosestPoint(player.position);
                if (!InsideStrike(nearest - player.position, AttackDirection, reach, false)) continue;
                if (Physics.Linecast(player.position, collider.bounds.center, blockers, QueryTriggerInteraction.Ignore)) continue;
                hitThisStrike.Add(enemy);
                Vector3 away = enemy.transform.position - player.position;
                away.y = 0f;
                if (away.sqrMagnitude < .001f) away = AttackDirection;
                EnemyChaser chaser = enemy.GetComponent<EnemyChaser>();
                if (chaser != null) chaser.ApplyKnockback(away.normalized * 6f, .15f);
                EnemyHitFlash flash = enemy.GetComponent<EnemyHitFlash>();
                if (flash != null) flash.Play();
                enemy.TakeDamage(DamageForStrike(strike));
            }
        }

        public void Cancel()
        {
            IsAttacking = queued = false;
            nextStrike = 0;
            lastFinish = float.NegativeInfinity;
            hitThisStrike.Clear();
            if(sweepVfx!=null && sweepSlot>=0) sweepVfx.Release(sweepSlot,true);
            sweepSlot=-1;
            if (locomotion != null) locomotion.ClearSwordAttackStart();
            if (slash != null) { slash.emitting = false; slash.Clear(); }
        }

        private void OnDisable() => Cancel();
    }
}
