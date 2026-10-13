using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        public event Action<float> DashStarted;

        public float DashCooldownProgress
        {
            get
            {
                if (Time.time >= nextDashTime)
                {
                    return 1f;
                }

                float timeRemaining = nextDashTime - Time.time;
                return 1f - Mathf.Clamp01(timeRemaining / dashCooldown);
            }
        }

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;

        [Header("Dash")]
        [SerializeField, Min(0f)] private float dashSpeed = 16f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float dashCooldown = 1f;

        private Rigidbody body;
        private InputAction moveAction;
        private InputAction dashAction;
        private Vector2 moveInput;
        private Vector3 dashDirection;
        private float dashTimeRemaining;
        private float nextDashTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            dashAction = new InputAction("Dash", InputActionType.Button);
            dashAction.AddBinding("<Keyboard>/space");
            dashAction.AddBinding("<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            moveAction.Enable();
            dashAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            dashAction.Disable();
        }

        private void OnDestroy()
        {
            moveAction.Dispose();
            dashAction.Dispose();
        }

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

            if (dashAction.WasPressedThisFrame() && Time.time >= nextDashTime)
            {
                StartDash();
            }
        }

        private void FixedUpdate()
        {
            if (dashTimeRemaining > 0f)
            {
                dashTimeRemaining -= Time.fixedDeltaTime;

                Vector3 dashVelocity = body.linearVelocity;
                body.linearVelocity = new Vector3(
                    dashDirection.x * dashSpeed,
                    dashVelocity.y,
                    dashDirection.z * dashSpeed);
                return;
            }

            Vector3 movement = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 currentVelocity = body.linearVelocity;
            body.linearVelocity = new Vector3(
                movement.x * moveSpeed,
                currentVelocity.y,
                movement.z * moveSpeed);
        }

        private void StartDash()
        {
            dashDirection = new Vector3(moveInput.x, 0f, moveInput.y);

            if (dashDirection.sqrMagnitude < 0.001f)
            {
                dashDirection = transform.forward;
                dashDirection.y = 0f;
            }

            dashDirection.Normalize();
            dashTimeRemaining = dashDuration;
            nextDashTime = Time.time + dashCooldown;
            DashStarted?.Invoke(dashDuration);
        }
    }
}
