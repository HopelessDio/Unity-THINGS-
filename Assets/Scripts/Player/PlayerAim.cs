using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Survivor.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField, Min(0f)] private float rotationSpeed = 18f;

        private Rigidbody body;
        private Vector3 aimDirection = Vector3.forward;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (aimCamera == null || Mouse.current == null)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray pointerRay = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (!groundPlane.Raycast(pointerRay, out float enter))
            {
                return;
            }

            Vector3 pointerPosition = pointerRay.GetPoint(enter);
            Vector3 direction = pointerPosition - body.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                aimDirection = direction.normalized;
            }
        }

        private void FixedUpdate()
        {
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
            Quaternion nextRotation = Quaternion.Slerp(
                body.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime);

            body.MoveRotation(nextRotation);
        }
    }
}
