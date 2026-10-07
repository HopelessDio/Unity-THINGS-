using System.Collections;
using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public static event Action<int> EnemyDamaged;
    public static event Action EnemyDefeated;

    public int maxHealth = 3;
    public Color hitGlowColor = new Color(1f, 0.78f, 0.48f);
    public float hitGlowDuration = 0.12f;

    private int currentHealth;
    private bool isDead;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer glowRenderer;
    private Color originalColor;
    private Coroutine hitGlowRoutine;
    private EnemyWalkAnimation walkAnimation;

    void Awake()
    {
        currentHealth = maxHealth;
        walkAnimation = GetComponentInChildren<EnemyWalkAnimation>();
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer candidate in spriteRenderers)
        {
            if (candidate.enabled && candidate.sprite != null)
            {
                spriteRenderer = candidate;
                break;
            }
        }

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            CreateGlowRenderer();
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector2.zero);
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isDead)
            return;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(currentHealth - damage, 0);
        int damageDealt = previousHealth - currentHealth;

        if (damageDealt > 0)
            EnemyDamaged?.Invoke(damageDealt);

        walkAnimation?.PlayHitReaction(hitDirection);
        if (spriteRenderer != null)
        {
            if (hitGlowRoutine != null)
                StopCoroutine(hitGlowRoutine);

            hitGlowRoutine = StartCoroutine(GlowOnHit());
        }

        if (currentHealth <= 0)
        {
            isDead = true;
            EnemyDefeated?.Invoke();
            GameHUD.Instance?.AddKill();
            Destroy(gameObject);
        }
    }

    IEnumerator GlowOnHit()
    {
        // Tint the enemy and show a slightly enlarged colored copy behind it,
        // making the hit flash visible even on dark or highly detailed sprites.
        spriteRenderer.color = hitGlowColor;
        glowRenderer.enabled = true;
        float elapsed = 0f;

        while (elapsed < hitGlowDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / Mathf.Max(hitGlowDuration, 0.01f));
            spriteRenderer.color = Color.Lerp(originalColor, hitGlowColor, fade);
            glowRenderer.sprite = spriteRenderer.sprite;
            Color glowColor = hitGlowColor;
            glowColor.a = fade * 0.85f;
            glowRenderer.color = glowColor;
            yield return null;
        }

        spriteRenderer.color = originalColor;
        glowRenderer.enabled = false;
        hitGlowRoutine = null;
    }

    void CreateGlowRenderer()
    {
        GameObject glowObject = new GameObject("Hit Glow");
        glowObject.transform.SetParent(spriteRenderer.transform, false);
        glowObject.transform.localScale = Vector3.one * 1.18f;

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = spriteRenderer.sprite;
        glowRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        glowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        glowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        glowRenderer.enabled = false;
    }
}
