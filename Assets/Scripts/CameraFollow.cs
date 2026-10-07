using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 8f;

    [Header("Angled Orthographic View")]
    [Range(0f, 45f)] public float tiltAngle = 30f;
    public float cameraDistance = 10f;
    public Vector2 framingOffset = Vector2.zero;

    void Start()
    {
        UpdateCamera(true);
    }

    void LateUpdate()
    {
        UpdateCamera(false);
    }

    void UpdateCamera(bool snapImmediately)
    {
        if (target == null)
            return;

        Quaternion targetRotation = Quaternion.Euler(tiltAngle, 0f, 0f);
        Vector3 focusPoint = target.position + new Vector3(framingOffset.x, framingOffset.y, 0f);
        Vector3 targetPosition = focusPoint - targetRotation * Vector3.forward * cameraDistance;

        if (snapImmediately)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            return;
        }

        float smoothing = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing);
    }
}
