using UnityEngine;

namespace Survivor.Player
{
    [RequireComponent(typeof(Rigidbody), typeof(PlayerMovement))]
    public sealed class PlayerAim : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float turnSmoothTime = .09f;
        [SerializeField, Min(1f)] private float maxTurnSpeed = 1080f;
        private Rigidbody body;
        private PlayerMovement movement;
        private Quaternion desiredFacing;
        private float angularVelocity;
        private Survivor.Combat.PlayerSwordAttack sword;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            movement = GetComponent<PlayerMovement>();
            desiredFacing = body.rotation;
        }

        private void OnEnable()
        {
            if (body != null) desiredFacing = body.rotation;
            angularVelocity = 0f;
        }

        private void FixedUpdate()
        {
            if (movement == null || !movement.enabled) return;
            if (sword == null) sword = GetComponentInChildren<Survivor.Combat.PlayerSwordAttack>();
            bool attackTurning = sword != null && sword.IsFacingLocked && !movement.IsDashing;
            Vector3 direction = attackTurning
                ? sword.AttackDirection : movement.FacingDirection;
            if (TryGetFacingRotation(direction, out Quaternion rotation))
                desiredFacing = rotation;
            // Complete the last requested turn even when the keys are released.
            body.MoveRotation(SmoothFacing(body.rotation, desiredFacing,
                ref angularVelocity, attackTurning ? turnSmoothTime * .5f : turnSmoothTime,
                attackTurning ? maxTurnSpeed * 2f : maxTurnSpeed, Time.fixedDeltaTime));
        }

        public static Quaternion SmoothFacing(Quaternion current, Quaternion target,
            ref float velocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            float yaw = Mathf.SmoothDampAngle(current.eulerAngles.y, target.eulerAngles.y,
                ref velocity, smoothTime, maxSpeed, deltaTime);
            return Quaternion.Euler(0f, yaw, 0f);
        }

        public static bool TryGetFacingRotation(Vector3 direction, out Quaternion rotation)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < .001f)
            {
                rotation = Quaternion.identity;
                return false;
            }
            rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return true;
        }
    }
}
