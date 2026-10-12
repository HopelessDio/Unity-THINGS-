using Survivor.Enemies;
using UnityEngine;

namespace Survivor.Combat
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 12f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.1f)] private float lifetime = 3f;
        [SerializeField, Min(0f)] private float knockbackSpeed = 8f;
        [SerializeField, Min(0f)] private float knockbackDuration = 0.15f;

        private Rigidbody body;
        private Vector3 direction;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            GetComponent<Collider>().isTrigger = true;
        }

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        public void Initialize(Vector3 travelDirection)
        {
            direction = travelDirection.normalized;
        }

        private void FixedUpdate()
        {
            body.MovePosition(body.position + direction * (speed * Time.fixedDeltaTime));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out EnemyHealth enemyHealth))
            {
                return;
            }

            if (other.TryGetComponent(out EnemyChaser enemyChaser))
            {
                enemyChaser.ApplyKnockback(direction * knockbackSpeed, knockbackDuration);
            }

            if (other.TryGetComponent(out EnemyHitFlash hitFlash))
            {
                hitFlash.Play();
            }

            enemyHealth.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
