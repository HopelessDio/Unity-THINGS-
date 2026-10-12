using UnityEngine;

namespace Survivor.CameraSystem
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Position")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        [Header("View")]
        [SerializeField, Min(0f)] private float lookAtHeight = 1f;

        private Vector3 followVelocity;
        private float fixedHeight;

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("Camera Follow needs a target.", this);
                enabled = false;
                return;
            }

            Vector3 startingPosition = target.position + offset;
            fixedHeight = startingPosition.y;
            transform.position = startingPosition;
            UpdateRotation();
        }

        private void LateUpdate()
        {
            Vector3 desiredPosition = target.position + offset;
            desiredPosition.y = fixedHeight;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                smoothTime);

            UpdateRotation();
        }

        private void UpdateRotation()
        {
            // Keep a fixed viewing angle even while the camera is catching up.
            // Aiming directly at the moving target would make the world appear to wobble.
            Vector3 lookDirection = Vector3.up * lookAtHeight - offset;
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }
    }
}
