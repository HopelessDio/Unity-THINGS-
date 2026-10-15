using UnityEngine;
using UnityEngine.InputSystem;

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
        private Camera aimCamera;
        public bool UsesMouseAim => enabled && Mouse.current != null;

        public Vector3 InputDirection
        {
            get
            {
                if (UsesMouseAim)
                {
                    if (aimCamera == null) aimCamera = Camera.main;
                    if (aimCamera != null && aimCamera.pixelRect.Contains(Mouse.current.position.ReadValue()) &&
                        TryProjectDirection(aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue()),transform.position,out Vector3 direction))
                        return direction;
                    return transform.forward;
                }
                if (movement == null) movement = GetComponent<PlayerMovement>();
                Vector3 input = movement != null ? movement.FacingDirection : Vector3.zero;
                return input.sqrMagnitude > .001f ? input.normalized : transform.forward;
            }
        }

        public static bool TryProjectDirection(Ray ray, Vector3 playerPosition, out Vector3 direction)
        {
            direction = Vector3.zero;
            // Project onto the horizontal combat plane, not individual floor
            // colliders: aim stays stable across pits, walls and raised pieces.
            var plane = new UnityEngine.Plane(Vector3.up, playerPosition);
            if (!plane.Raycast(ray, out float distance)) return false;
            Vector3 delta = ray.GetPoint(distance) - playerPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < .0025f) return false;
            direction = delta.normalized;
            return true;
        }

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
            Vector3 direction = movement.IsDashing ? movement.FacingDirection
                : attackTurning ? sword.AttackDirection : InputDirection;
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
