using UnityEngine;

public class EnemyWalkAnimation : MonoBehaviour
{
    public Transform visual;
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
        // Freeze whichever walk frame is currently visible. Swapping to the
        // separate stopped PNG changed the apparent character size because its
        // artwork occupies a different amount of canvas space.
        if (animator != null && animator.enabled == hasReachedPlayer)
            animator.enabled = !hasReachedPlayer;

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
        // Contact attacks keep the same visual scale as the walk animation.
        float attackSquash = hasReachedPlayer ? 0f : attackSquashAmount;
        visual.localScale = new Vector3(
            startingLocalScale.x * (1f + step * squashAmount + hitStrength * hitSquashAmount - attackStrength * attackSquash),
            startingLocalScale.y * (1f - step * squashAmount - hitStrength * hitSquashAmount + attackStrength * attackSquash),
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
