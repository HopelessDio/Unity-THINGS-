using System.Collections;
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
    private Sprite muzzleFlashSprite;
    private Material muzzleFlashMaterial;
    private PlayerRage playerRage;

    void Awake()
    {
        mainCamera = Camera.main;
        playerRage = GetComponent<PlayerRage>();
        firePointDistance = Vector2.Distance(transform.position, firePoint.position);

        SpriteRenderer projectileRenderer = projectilePrefab != null
            ? projectilePrefab.GetComponent<SpriteRenderer>()
            : null;
        if (projectileRenderer != null)
        {
            muzzleFlashSprite = projectileRenderer.sprite;
            muzzleFlashMaterial = projectileRenderer.sharedMaterial;
        }
    }

    void Update()
    {
        if (mainCamera == null || Mouse.current == null)
            return;

        if (playerRage != null && playerRage.IsRaging)
            return;

        if (!TryGetMouseWorldPosition(out Vector2 mouseWorldPosition))
            return;

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
        StartCoroutine(ShowMuzzleFlash(direction));
    }

    IEnumerator ShowMuzzleFlash(Vector2 direction)
    {
        GameObject flashObject = new GameObject("Muzzle Flash");
        flashObject.transform.position = firePoint.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        flashObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer flash = flashObject.AddComponent<SpriteRenderer>();
        flash.sprite = muzzleFlashSprite;
        flash.sharedMaterial = muzzleFlashMaterial;
        flash.sortingOrder = 20;

        const float flashDuration = 0.08f;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flashDuration);
            flashObject.transform.localScale = new Vector3(
                Mathf.Lerp(0.16f, 0.42f, progress),
                Mathf.Lerp(0.22f, 0.04f, progress),
                1f);
            Color blueCore = new Color(0.3f, 0.9f, 1f, 1f);
            Color orangeFlame = new Color(1f, 0.38f, 0.04f, 1f);
            Color redEmber = new Color(1f, 0.05f, 0.02f, 1f);
            Color fireColor = progress < 0.4f
                ? Color.Lerp(blueCore, orangeFlame, progress / 0.4f)
                : Color.Lerp(orangeFlame, redEmber, (progress - 0.4f) / 0.6f);
            fireColor.a = 1f - progress;
            flash.color = fireColor;
            yield return null;
        }

        Destroy(flashObject);
    }

    bool TryGetMouseWorldPosition(out Vector2 worldPosition)
    {
        Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane gameplayPlane = new Plane(Vector3.forward, transform.position);

        if (gameplayPlane.Raycast(mouseRay, out float distance))
        {
            worldPosition = mouseRay.GetPoint(distance);
            return true;
        }

        worldPosition = default;
        return false;
    }
}
