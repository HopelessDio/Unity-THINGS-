using UnityEngine;

namespace Survivor.Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EnemyChaser : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float rotationSpeed = 10f;
        [SerializeField, Min(0f)] private float stoppingDistance = 1f;

        private Rigidbody body;
        private Vector3 knockbackVelocity;
        private float knockbackTimeRemaining;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void ApplySpeedMultiplier(float multiplier)
        {
            moveSpeed *= Mathf.Max(0f, multiplier);
        }

        public void ApplyKnockback(Vector3 velocity, float duration)
        {
            knockbackVelocity = new Vector3(velocity.x, 0f, velocity.z);
            knockbackTimeRemaining = Mathf.Max(0f, duration);
        }

        private void FixedUpdate()
        {
            if (knockbackTimeRemaining > 0f)
            {
                knockbackTimeRemaining -= Time.fixedDeltaTime;

                Vector3 knockbackCurrentVelocity = body.linearVelocity;
                body.linearVelocity = new Vector3(
                    knockbackVelocity.x,
                    knockbackCurrentVelocity.y,
                    knockbackVelocity.z);

                knockbackVelocity = Vector3.Lerp(
                    knockbackVelocity,
                    Vector3.zero,
                    10f * Time.fixedDeltaTime);
                return;
            }

            if (target == null)
            {
                StopMoving();
                return;
            }

            Vector3 toTarget = target.position - body.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                StopMoving();
                return;
            }

            Vector3 direction = toTarget.normalized;
            Vector3 currentVelocity = body.linearVelocity;
            body.linearVelocity = new Vector3(
                direction.x * moveSpeed,
                currentVelocity.y,
                direction.z * moveSpeed);

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion nextRotation = Quaternion.Slerp(
                body.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime);

            body.MoveRotation(nextRotation);
        }

        private void StopMoving()
        {
            Vector3 currentVelocity = body.linearVelocity;
            body.linearVelocity = new Vector3(0f, currentVelocity.y, 0f);
        }
    }
}
