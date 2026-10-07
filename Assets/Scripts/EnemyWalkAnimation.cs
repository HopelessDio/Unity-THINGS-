using UnityEngine;

public class EnemyWalkAnimation : MonoBehaviour
{
    public Transform visual;
    public Sprite stoppedSprite;
    public float bobSpeed = 7f;
    public float bobHeight = 0.08f;
    public float swayAngle = 6f;
    public float squashAmount = 0.05f;
    public float artworkAngleOffset = 90f;

    [Header("Hit Reaction")]
    public float hitReactionDuration = 0.16f;
    public float hitPushDistance = 0.12f;
    public float hitSquashAmount = 0.18f;
    public float hitShakeAngle = 8f;

    [Header("Attack Reaction")]
    public float attackDuration = 0.28f;
    public float attackLungeDistance = 0.2f;
    public float attackSquashAmount = 0.12f;

    private EnemyFollow enemyFollow;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Sprite walkingSprite;
    private Vector2 stoppedSpriteScale = Vector2.one;
    private Vector3 startingLocalPosition;
    private Vector3 startingLocalScale;
    private float animationOffset;
    private float hitReactionTime;
    private Vector2 localHitDirection;
    private float attackTime;
    private Vector2 localAttackDirection;
    private bool touchingPlayer;

    void Awake()
    {
        // This component lives on EnemyVisual, while EnemyFollow lives on the
        // parent Enemy object.
        enemyFollow = GetComponentInParent<EnemyFollow>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            walkingSprite = spriteRenderer.sprite;
        if (walkingSprite != null && stoppedSprite != null)
        {
            Vector2 walkingSize = walkingSprite.bounds.size;
            Vector2 stoppedSize = stoppedSprite.bounds.size;
            // Keep the idle PNG's original proportions. Its square canvas is
            // wider than the tall run frames, so match height with uniform scaling.
            float uniformScale = walkingSize.y / stoppedSize.y;
            stoppedSpriteScale = new Vector2(uniformScale, uniformScale);
        }
        animationOffset = Random.Range(0f, Mathf.PI * 2f);

        if (visual != null)
        {
            startingLocalPosition = visual.localPosition;
            startingLocalScale = visual.localScale;
        }
    }

    void Update()
    {
        if (visual == null)
            return;

        // Stop the walk bob and sway once the enemy reaches the player. The
        // attack reaction below can still animate while the enemy is stopped.
        bool hasReachedPlayer = touchingPlayer || (enemyFollow != null && enemyFollow.IsAtStoppingDistance);
        if (animator != null)
            animator.enabled = !hasReachedPlayer;
        if (hasReachedPlayer && stoppedSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = stoppedSprite;
        else if (spriteRenderer != null && spriteRenderer.sprite == stoppedSprite && walkingSprite != null)
            spriteRenderer.sprite = walkingSprite;

        float walkCycle = hasReachedPlayer
            ? 0f
            : Mathf.Sin(Time.time * bobSpeed + animationOffset);
        float step = Mathf.Abs(walkCycle);
        float hitStrength = hitReactionDuration > 0f
            ? Mathf.Clamp01(hitReactionTime / hitReactionDuration)
            : 0f;
        float attackProgress = attackDuration > 0f
            ? 1f - Mathf.Clamp01(attackTime / attackDuration)
            : 1f;
        float attackStrength = attackTime > 0f
            ? Mathf.Sin(attackProgress * Mathf.PI)
            : 0f;

        if (hitReactionTime > 0f)
            hitReactionTime -= Time.deltaTime;
        if (attackTime > 0f)
            attackTime -= Time.deltaTime;

        Vector3 hitOffset = (Vector3)(localHitDirection * hitPushDistance * hitStrength);
        // The first half pulls back slightly, then the enemy lunges forward.
        float attackMotion = attackProgress < 0.28f
            ? -attackStrength * 0.3f
            : attackStrength;
        Vector3 attackOffset = (Vector3)(localAttackDirection * attackLungeDistance * attackMotion);
        visual.localPosition = startingLocalPosition + Vector3.up * step * bobHeight + hitOffset + attackOffset;
        Vector2 spriteScale = hasReachedPlayer ? stoppedSpriteScale : Vector2.one;
        float attackSquash = hasReachedPlayer ? attackSquashAmount * 0.35f : attackSquashAmount;
        visual.localScale = new Vector3(
            startingLocalScale.x * spriteScale.x * (1f + step * squashAmount + hitStrength * hitSquashAmount - attackStrength * attackSquash),
            startingLocalScale.y * spriteScale.y * (1f - step * squashAmount - hitStrength * hitSquashAmount + attackStrength * attackSquash),
            startingLocalScale.z);

        float facingAngle = visual.eulerAngles.z;
        if (enemyFollow != null && enemyFollow.player != null)
        {
            Vector2 direction = enemyFollow.player.position - transform.position;
            facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + artworkAngleOffset;
        }

        float hitShake = Mathf.Sin(Time.time * 90f) * hitShakeAngle * hitStrength;
        visual.rotation = Quaternion.Euler(0f, 0f, facingAngle + walkCycle * swayAngle + hitShake);
    }

    public void PlayHitReaction(Vector2 worldHitDirection)
    {
        hitReactionTime = hitReactionDuration;

        Vector2 direction = worldHitDirection.sqrMagnitude > 0.001f
            ? worldHitDirection.normalized
            : Vector2.up;

        if (visual != null && visual.parent != null)
            localHitDirection = visual.parent.InverseTransformVector(direction);
        else
            localHitDirection = direction;
    }

    public void PlayAttackAnimation()
    {
        attackTime = attackDuration;

        Vector2 direction = Vector2.up;
        if (enemyFollow != null && enemyFollow.player != null)
            direction = ((Vector2)enemyFollow.player.position - (Vector2)transform.position).normalized;

        if (visual != null && visual.parent != null)
            localAttackDirection = visual.parent.InverseTransformVector(direction);
        else
            localAttackDirection = direction;
    }

    public void SetTouchingPlayer(bool isTouching)
    {
        touchingPlayer = isTouching;
    }
}
