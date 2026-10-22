using Survivor.Player;
using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>Committed, dodgeable claw swipe. Replaces contact damage on the home encounter only.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class ZombieMeleeAttack : MonoBehaviour
    {
        [SerializeField] private PlayerHealth target;
        [SerializeField] private float attackRange = 1.7f, damage = 15f, duration = 1.1f, cooldown = 1.5f;
        private EnemyChaser chaser;
        private EnemyHealth health;
        private Rigidbody body;
        private Animator animator;
        private Transform spine, chest;
        private Transform[] arm;
        private float elapsed, nextAttack;
        private bool attemptedHit;
        private Vector3 attackDirection;
        public bool IsAttacking { get; private set; }
        public int AttackCount { get; private set; }
        public int HitCount { get; private set; }
        public float AttackProgress => IsAttacking ? elapsed / duration : 0;
        public void ConfigureTarget(PlayerHealth player) => target = player;
        private void Awake()
        {
            chaser = GetComponent<EnemyChaser>(); health = GetComponent<EnemyHealth>(); body = GetComponent<Rigidbody>();
            animator = GetComponentInChildren<Animator>();
        }
        private void Start()
        {
            if (target == null) target = FindAnyObjectByType<PlayerHealth>();
            spine = animator.GetBoneTransform(HumanBodyBones.Spine); chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            arm = new[] { animator.GetBoneTransform(HumanBodyBones.RightUpperArm), animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.RightHand) };
        }
        private void Update()
        {
            if (Time.timeScale <= 0) return;
            if (target == null || target.IsDead || health.IsDead) { Cancel(); return; }
            if (!IsAttacking)
            {
                Vector3 delta = target.transform.position - body.position; delta.y = 0;
                if (Time.time < nextAttack || delta.magnitude > attackRange || Vector3.Dot(transform.forward, delta.normalized) < .65f) return;
                IsAttacking = true; elapsed = 0; attemptedHit = false; AttackCount++;
                attackDirection = transform.forward; nextAttack = Time.time + cooldown; chaser.SetAttackPaused(true);
            }
            elapsed += Time.deltaTime;
            if (!attemptedHit && elapsed >= .46f)
            {
                attemptedHit = true;
                Vector3 delta = target.transform.position - body.position;
                float height = Mathf.Abs(delta.y); delta.y = 0;
                if (height < 1.6f && delta.magnitude <= attackRange + .15f && Vector3.Dot(delta.normalized, attackDirection) > .42f &&
                    !Physics.Linecast(body.position, target.transform.position, LayerMask.GetMask("ArenaObstacle"), QueryTriggerInteraction.Ignore))
                {
                    float before = target.CurrentHealth; target.TakeDamage(damage);
                    if (target.CurrentHealth < before) HitCount++;
                }
            }
            if (elapsed >= duration) Cancel();
        }
        private void LateUpdate()
        {
            if (!IsAttacking || arm == null) return;
            float reach;
            if (elapsed < .4f) reach = Mathf.SmoothStep(0, 1, elapsed / .4f) * -.22f;
            else if (elapsed < .58f) reach = Mathf.Lerp(-.22f, .9f, Mathf.SmoothStep(0, 1, (elapsed - .4f) / .18f));
            else reach = Mathf.Lerp(.9f, 0, Mathf.SmoothStep(0, 1, (elapsed - .58f) / (duration - .58f)));
            float weight = Mathf.SmoothStep(0, 1, elapsed / .14f) * (1 - Mathf.SmoothStep(0, 1, (elapsed - .8f) / .3f));
            spine.rotation = Quaternion.AngleAxis(reach * 13 * weight, transform.right) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(-reach * 18 * weight, Vector3.up) * chest.rotation;
            Vector3 hand = body.position + Vector3.up * .55f + attackDirection * reach + transform.right * (.42f - Mathf.Max(0, reach) * .5f);
            Vector3 targetHand = Vector3.Lerp(arm[2].position, hand, weight);
            FeedingZombiePose.Solve(arm, targetHand, transform.right);
        }
        private void Cancel() { IsAttacking = false; if (chaser != null) chaser.SetAttackPaused(false); }
        private void OnDisable() => Cancel();
    }
}
