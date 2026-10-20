using Survivor.Player;
using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>Animation and target acquisition only; existing components own combat/physics.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyChaser), typeof(Rigidbody), typeof(EnemyHealth))]
    public sealed class AshboundZombie : MonoBehaviour
    {
        [SerializeField] private Animator modelAnimator;
        [SerializeField, Min(.1f)] private float animationWalkSpeed = .9f;
        [SerializeField] private bool findPlayerOnStart = true;

        private static readonly int Speed = Animator.StringToHash("Speed");
        private Rigidbody body;
        private EnemyChaser chaser;
        private PlayerHealth targetHealth;
        private bool targetAssigned;

        public void Configure(Animator animator)
        {
            modelAnimator = animator;
            modelAnimator.applyRootMotion = false;
        }

        public void SetPlayerTarget(PlayerHealth player)
        {
            targetAssigned = true;
            targetHealth = player;
            if (chaser == null) chaser = GetComponent<EnemyChaser>();
            chaser.SetTarget(player != null ? player.transform : null);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            chaser = GetComponent<EnemyChaser>();
            if (modelAnimator == null) modelAnimator = GetComponentInChildren<Animator>();
            if (modelAnimator != null) modelAnimator.applyRootMotion = false;
        }

        private void Start()
        {
            // Works when manually placed, as well as through EnemySpawner.SetTarget.
            if (findPlayerOnStart && !targetAssigned)
                SetPlayerTarget(FindAnyObjectByType<PlayerHealth>());
        }

        private void Update()
        {
            if (targetHealth != null && targetHealth.IsDead)
                SetPlayerTarget(null);
            if (modelAnimator == null || Time.timeScale <= 0f) return;

            Vector3 velocity = body.linearVelocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            modelAnimator.SetFloat(Speed, speed, .12f, Time.deltaTime);
            // In-place feet match pursuit speed; physics alone moves the capsule.
            modelAnimator.speed = speed > .08f
                ? Mathf.Clamp(speed / animationWalkSpeed, .35f, 2.2f) : 1f;
        }
    }
}
