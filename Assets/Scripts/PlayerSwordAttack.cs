using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSwordAttack : MonoBehaviour
{
    public PlayerRage playerRage;

    [Header("Sword Combat")]
    public int damage = 3;
    public float attackRange = 1.55f;
    [Range(20f, 180f)] public float attackArc = 110f;
    public float attackCooldown = 0.32f;
    public float swingDuration = 0.3f;
    public float swordLength = 1.15f;
    public float knockbackForce = 5f;

    [Header("Sword Fire")]
    public Color bladeCoreColor = new Color(1f, 0.9f, 0.55f, 1f);
    public Color flameColor = new Color(1f, 0.24f, 0.02f, 1f);
    public Color emberColor = new Color(0.65f, 0.01f, 0.005f, 1f);

    private Camera mainCamera;
    private Animator playerAnimator;
    private PlayerRunController runController;
    private Transform swordPivot;
    private LineRenderer bladeRenderer;
    private LineRenderer glowRenderer;
    private TrailRenderer arcTrail;
    private Coroutine swingRoutine;
    private float nextAttackTime;
    private bool isSwinging;
    private static Material sharedSwordMaterial;
    private static readonly int RageIdleAnimation = Animator.StringToHash("PlayerRageIdle");
    private static readonly int RageSwordAnimation = Animator.StringToHash("PlayerRageSword");
    private const float RageSwordClipDuration = 0.3f;

    void Awake()
    {
        if (playerRage == null)
            playerRage = GetComponent<PlayerRage>();
        runController = GetComponent<PlayerRunController>();
        playerAnimator = runController != null ? runController.animator : GetComponentInChildren<Animator>();
        mainCamera = Camera.main;
        CreateSwordVisual();
    }

    void OnEnable()
    {
        if (playerRage == null)
            playerRage = GetComponent<PlayerRage>();
        if (playerRage != null)
        {
            playerRage.RageStarted += EnterRageStance;
            playerRage.RageEnded += StopSwordImmediately;
        }
    }

    void OnDisable()
    {
        if (playerRage != null)
        {
            playerRage.RageStarted -= EnterRageStance;
            playerRage.RageEnded -= StopSwordImmediately;
        }
    }

    void Update()
    {
        if (playerRage == null || !playerRage.IsRaging || Mouse.current == null)
            return;

        if (!isSwinging && Time.time >= nextAttackTime && Mouse.current.leftButton.isPressed &&
            TryGetMouseWorldPosition(out Vector2 mouseWorldPosition))
        {
            Vector2 aimDirection = mouseWorldPosition - (Vector2)transform.position;
            if (aimDirection.sqrMagnitude > 0.001f)
                swingRoutine = StartCoroutine(SwingSword(aimDirection.normalized));
        }
    }

    IEnumerator SwingSword(Vector2 aimDirection)
    {
        isSwinging = true;
        nextAttackTime = Time.time + attackCooldown;
        bool useCharacterAnimation = playerAnimator != null &&
                                     playerAnimator.runtimeAnimatorController != null &&
                                     playerAnimator.HasState(0, RageSwordAnimation);

        if (useCharacterAnimation)
        {
            runController?.SetExternalAnimationLock(true);
            playerAnimator.enabled = true;
            playerAnimator.speed = RageSwordClipDuration / Mathf.Max(swingDuration, 0.01f);
            playerAnimator.Play(RageSwordAnimation, 0, 0f);
        }
        else
        {
            swordPivot.gameObject.SetActive(true);
            bladeRenderer.enabled = true;
            glowRenderer.enabled = true;
            arcTrail.Clear();
            arcTrail.emitting = true;
        }

        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        float startAngle = aimAngle - attackArc * 0.55f;
        float endAngle = aimAngle + attackArc * 0.55f;
        float elapsed = 0f;
        bool dealtDamage = false;

        while (elapsed < swingDuration && playerRage != null && playerRage.IsRaging)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(swingDuration, 0.01f));
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            if (!useCharacterAnimation)
                swordPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngle, endAngle, easedProgress));

            if (!dealtDamage && progress >= 0.42f)
            {
                dealtDamage = true;
                DamageEnemies(aimDirection);
            }

            yield return null;
        }

        if (useCharacterAnimation)
        {
            playerAnimator.speed = 1f;
            if (!TryPlayRageIdle())
                runController?.SetExternalAnimationLock(false);
        }
        else
        {
            arcTrail.emitting = false;
            bladeRenderer.enabled = false;
            glowRenderer.enabled = false;
            yield return new WaitForSeconds(0.12f);
            swordPivot.gameObject.SetActive(false);
        }

        isSwinging = false;
        swingRoutine = null;
    }

    void EnterRageStance()
    {
        TryPlayRageIdle();
    }

    bool TryPlayRageIdle()
    {
        if (playerRage == null || !playerRage.IsRaging ||
            playerAnimator == null || playerAnimator.runtimeAnimatorController == null ||
            !playerAnimator.HasState(0, RageIdleAnimation))
        {
            return false;
        }

        runController?.SetExternalAnimationLock(true);
        playerAnimator.enabled = true;
        playerAnimator.speed = 1f;
        playerAnimator.Play(RageIdleAnimation, 0, 0f);
        return true;
    }

    void DamageEnemies(Vector2 aimDirection)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, attackRange);
        HashSet<EnemyHealth> damagedEnemies = new HashSet<EnemyHealth>();
        float minimumDot = Mathf.Cos(attackArc * 0.5f * Mathf.Deg2Rad);

        foreach (Collider2D candidate in colliders)
        {
            EnemyHealth enemyHealth = candidate.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || !damagedEnemies.Add(enemyHealth))
                continue;

            Vector2 toEnemy = (Vector2)enemyHealth.transform.position - (Vector2)transform.position;
            if (toEnemy.sqrMagnitude < 0.001f || Vector2.Dot(aimDirection, toEnemy.normalized) < minimumDot)
                continue;

            EnemyFollow enemyFollow = enemyHealth.GetComponent<EnemyFollow>();
            if (enemyFollow != null)
                enemyFollow.ApplyKnockback(toEnemy.normalized, knockbackForce);

            enemyHealth.TakeDamage(damage, toEnemy.normalized);
        }
    }

    void CreateSwordVisual()
    {
        GameObject pivotObject = new GameObject("Rage Sword");
        pivotObject.transform.SetParent(transform, false);
        swordPivot = pivotObject.transform;

        if (sharedSwordMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                sharedSwordMaterial = new Material(shader);
        }

        glowRenderer = CreateBladeLine("Sword Flame Glow", 0.18f, 0.08f, flameColor, emberColor, 13);
        bladeRenderer = CreateBladeLine("Sword Core", 0.075f, 0.035f, bladeCoreColor, flameColor, 14);

        GameObject tipObject = new GameObject("Sword Tip Trail");
        tipObject.transform.SetParent(swordPivot, false);
        tipObject.transform.localPosition = new Vector3(swordLength, 0f, 0f);

        arcTrail = tipObject.AddComponent<TrailRenderer>();
        arcTrail.time = 0.18f;
        arcTrail.startWidth = 0.2f;
        arcTrail.endWidth = 0f;
        arcTrail.minVertexDistance = 0.025f;
        arcTrail.numCapVertices = 4;
        arcTrail.numCornerVertices = 3;
        arcTrail.alignment = LineAlignment.View;
        arcTrail.sharedMaterial = sharedSwordMaterial;
        arcTrail.sortingOrder = 12;

        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[]
            {
                new GradientColorKey(bladeCoreColor, 0f),
                new GradientColorKey(flameColor, 0.35f),
                new GradientColorKey(emberColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.65f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        arcTrail.colorGradient = trailGradient;
        arcTrail.emitting = false;
        swordPivot.gameObject.SetActive(false);
    }

    LineRenderer CreateBladeLine(
        string objectName,
        float startWidth,
        float endWidth,
        Color startColor,
        Color endColor,
        int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(swordPivot, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(0.22f, 0f, 0f));
        line.SetPosition(1, new Vector3(swordLength, 0f, 0f));
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.startColor = startColor;
        line.endColor = endColor;
        line.numCapVertices = 4;
        line.sharedMaterial = sharedSwordMaterial;
        line.sortingOrder = sortingOrder;
        return line;
    }

    bool TryGetMouseWorldPosition(out Vector2 worldPosition)
    {
        if (mainCamera == null)
        {
            worldPosition = default;
            return false;
        }

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

    void StopSwordImmediately()
    {
        if (swingRoutine != null)
            StopCoroutine(swingRoutine);

        swingRoutine = null;
        isSwinging = false;
        if (arcTrail != null)
            arcTrail.emitting = false;
        if (swordPivot != null)
            swordPivot.gameObject.SetActive(false);
        if (playerAnimator != null)
            playerAnimator.speed = 1f;
        runController?.SetExternalAnimationLock(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.02f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
