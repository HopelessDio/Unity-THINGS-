using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    public Transform visual;
    public float turnSpeed = 20f;
    public float artworkAngleOffset = 90f;

    public Vector2 AimDirection { get; private set; } = Vector2.down;

    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (visual == null || mainCamera == null || Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        Vector2 aimDirection = mouseWorldPosition - (Vector2)transform.position;

        if (aimDirection.sqrMagnitude < 0.001f)
            return;

        AimDirection = aimDirection.normalized;

        float targetAngle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg + artworkAngleOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        float smoothing = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);

        visual.rotation = Quaternion.Slerp(visual.rotation, targetRotation, smoothing);
    }
}
