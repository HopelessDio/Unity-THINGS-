using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float shotsPerSecond = 4f;

    private Camera mainCamera;
    private float firePointDistance;
    private float nextShotTime;

    void Awake()
    {
        mainCamera = Camera.main;
        firePointDistance = Vector2.Distance(transform.position, firePoint.position);
    }

    void Update()
    {
        if (mainCamera == null || Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        Vector2 aimDirection = (mouseWorldPosition - (Vector2)transform.position).normalized;

        if (aimDirection == Vector2.zero)
            return;

        firePoint.position = (Vector2)transform.position + aimDirection * firePointDistance;

        if (Mouse.current.leftButton.isPressed && Time.time >= nextShotTime)
        {
            Shoot(aimDirection);
            nextShotTime = Time.time + 1f / shotsPerSecond;
        }
    }

    void Shoot(Vector2 direction)
    {
        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = projectileObject.GetComponent<Projectile>();
        projectile.Initialize(direction);
    }
}
