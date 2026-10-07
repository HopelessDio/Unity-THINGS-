using System.Collections;
using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public event Action<int> Damaged;

    public int maxHealth = 5;
    public GameOverScreen gameOverScreen;

    [Header("Damage Glow")]
    public Color damageGlowColor = new Color(1f, 0.2f, 0.15f, 1f);
    public float damageGlowDuration = 0.22f;
    public float damageGlowSize = 1.18f;
    public float damageShakeDistance = 0.07f;
    public float damageShakeSpeed = 75f;
    public float damagePunchScale = 0.1f;

    public int CurrentHealth { get; private set; }

    private SpriteRenderer playerRenderer;
    private SpriteRenderer glowRenderer;
    private Transform playerVisual;
    private Color originalColor;
    private Vector3 originalVisualPosition;
    private Vector3 originalVisualScale;
    private Coroutine damageGlowRoutine;

    void Awake()
    {
        Time.timeScale = 1f;
        CurrentHealth = maxHealth;
        FindPlayerRenderer();

        if (playerRenderer != null)
        {
            playerVisual = playerRenderer.transform;
            originalColor = playerRenderer.color;
            originalVisualPosition = playerVisual.localPosition;
            originalVisualScale = playerVisual.localScale;
            CreateGlowRenderer();
        }
    }

    public void TakeDamage(int damage)
    {
        int previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
        int damageTaken = previousHealth - CurrentHealth;

        if (damageTaken <= 0)
            return;

        Damaged?.Invoke(damageTaken);
        Debug.Log($"Player health: {CurrentHealth}/{maxHealth}");

        if (playerRenderer != null)
        {
            if (damageGlowRoutine != null)
            {
                StopCoroutine(damageGlowRoutine);
                ResetDamageVisual();
            }

            damageGlowRoutine = StartCoroutine(ShowDamageGlow());
        }

        if (CurrentHealth == 0)
        {
            Debug.Log("Game Over!");
            if (gameOverScreen != null)
                gameOverScreen.Show();

            Time.timeScale = 0f;
        }
    }

    void FindPlayerRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer candidate in renderers)
        {
            if (candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                playerRenderer = candidate;
                return;
            }
        }
    }

    void CreateGlowRenderer()
    {
        GameObject glowObject = new GameObject("Player Damage Glow");
        glowObject.transform.SetParent(playerRenderer.transform, false);
        glowObject.transform.localScale = Vector3.one * damageGlowSize;

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sharedMaterial = playerRenderer.sharedMaterial;
        glowRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        glowRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
        glowRenderer.enabled = false;
    }

    IEnumerator ShowDamageGlow()
    {
        playerRenderer.color = damageGlowColor;
        glowRenderer.enabled = true;
        float elapsed = 0f;

        while (elapsed < damageGlowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(damageGlowDuration, 0.01f));
            float strength = 1f - progress;

            glowRenderer.sprite = playerRenderer.sprite;
            glowRenderer.flipX = playerRenderer.flipX;
            glowRenderer.flipY = playerRenderer.flipY;

            float shakeX = Mathf.Sin(Time.unscaledTime * damageShakeSpeed) * damageShakeDistance * strength;
            float shakeY = Mathf.Cos(Time.unscaledTime * damageShakeSpeed * 0.73f) * damageShakeDistance * strength;
            playerVisual.localPosition = originalVisualPosition + new Vector3(shakeX, shakeY, 0f);

            float punch = Mathf.Sin(progress * Mathf.PI) * damagePunchScale;
            playerVisual.localScale = new Vector3(
                originalVisualScale.x * (1f + punch),
                originalVisualScale.y * (1f - punch),
                originalVisualScale.z);

            playerRenderer.color = Color.Lerp(originalColor, damageGlowColor, strength);
            Color glowColor = damageGlowColor;
            glowColor.a = strength * 0.85f;
            glowRenderer.color = glowColor;
            yield return null;
        }

        ResetDamageVisual();
        damageGlowRoutine = null;
    }

    void ResetDamageVisual()
    {
        if (playerRenderer != null)
            playerRenderer.color = originalColor;
        if (glowRenderer != null)
            glowRenderer.enabled = false;
        if (playerVisual != null)
        {
            playerVisual.localPosition = originalVisualPosition;
            playerVisual.localScale = originalVisualScale;
        }
    }
}
