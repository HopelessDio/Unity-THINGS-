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

        [Header("Obstacle Steering")]
        [SerializeField] private LayerMask obstacleLayers;
        [SerializeField, Min(0.1f)] private float obstacleCheckDistance = 2.5f;
        [SerializeField, Range(10f, 90f)] private float avoidanceAngle = 55f;
        [SerializeField, Min(0.1f)] private float turnCommitmentTime = 0.6f;

        private Rigidbody body;
        private Vector3 knockbackVelocity;
        private float knockbackTimeRemaining;
        private float avoidanceDirection = 1f;
        private float turnCommitmentUntil;
        private Vector3 hazardEscapeDirection;
        private float hazardEscapeSpeed;
        private float hazardEscapeUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            avoidanceDirection = Random.value < 0.5f ? -1f : 1f;
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

        public void EscapeHazard(Vector3 hazardCenter, float escapeSpeed)
        {
            Vector3 awayFromHazard = body.position - hazardCenter;
            awayFromHazard.y = 0f;

            if (awayFromHazard.sqrMagnitude < 0.001f)
            {
                awayFromHazard = -transform.forward;
                awayFromHazard.y = 0f;
            }

            hazardEscapeDirection = awayFromHazard.normalized;
            hazardEscapeSpeed = escapeSpeed;
            hazardEscapeUntil = Time.time + 0.15f;
        }

        private void FixedUpdate()
        {
            if (Time.time < hazardEscapeUntil)
            {
                Vector3 hazardCurrentVelocity = body.linearVelocity;
                body.linearVelocity = new Vector3(
                    hazardEscapeDirection.x * hazardEscapeSpeed,
                    hazardCurrentVelocity.y,
                    hazardEscapeDirection.z * hazardEscapeSpeed);
                return;
            }

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

            Vector3 direction = GetSteeredDirection(toTarget.normalized);
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

        private Vector3 GetSteeredDirection(Vector3 desiredDirection)
        {
            if (obstacleLayers.value == 0)
            {
                return desiredDirection;
            }

            Vector3 rayOrigin = body.position + Vector3.up * 0.5f;
            bool obstacleAhead = Physics.SphereCast(
                rayOrigin,
                0.4f,
                desiredDirection,
                out RaycastHit hit,
                obstacleCheckDistance,
                obstacleLayers,
                QueryTriggerInteraction.Collide);

            if (!obstacleAhead)
            {
                return desiredDirection;
            }

            if (Time.time >= turnCommitmentUntil)
            {
                Vector3 obstacleDirection = hit.point - body.position;
                float side = Vector3.Dot(transform.right, obstacleDirection);

                avoidanceDirection = Mathf.Abs(side) > 0.05f
                    ? -Mathf.Sign(side)
                    : avoidanceDirection;
                turnCommitmentUntil = Time.time + turnCommitmentTime;
            }

            return Quaternion.AngleAxis(
                avoidanceAngle * avoidanceDirection,
                Vector3.up) * desiredDirection;
        }
    }
}
