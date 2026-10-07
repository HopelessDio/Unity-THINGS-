using UnityEngine;

public class EnemyFollow : MonoBehaviour
{
    public float moveSpeed = 2f;
    [Tooltip("How close the enemy can get to the player, in world units.")]
    public float stoppingDistance = 0.9f;
    public float knockbackRecovery = 14f;
    public Transform player;

    private Rigidbody2D body;
    private Vector2 knockbackVelocity;

    public bool IsAtStoppingDistance
    {
        get
        {
            if (player == null)
                return false;

            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
            Vector2 offsetToPlayer = (Vector2)player.position - currentPosition;
            return offsetToPlayer.sqrMagnitude <= stoppingDistance * stoppingDistance;
        }
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    void FixedUpdate()
    {
        if (player == null)
            return;

        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
        Vector2 direction = (Vector2)player.position - currentPosition;
        float distance = direction.magnitude;

        Vector2 movementVelocity = Vector2.zero;
        if (distance > stoppingDistance)
            movementVelocity = direction / distance * moveSpeed;

        Vector2 nextPosition = currentPosition +
            (movementVelocity + knockbackVelocity) * Time.fixedDeltaTime;
        if (body != null)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        knockbackVelocity = Vector2.MoveTowards(
            knockbackVelocity,
            Vector2.zero,
            knockbackRecovery * Time.fixedDeltaTime);
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (direction.sqrMagnitude < 0.001f || force <= 0f)
            return;

        knockbackVelocity += direction.normalized * force;
    }
}
