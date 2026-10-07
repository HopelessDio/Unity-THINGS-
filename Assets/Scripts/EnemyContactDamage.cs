using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    public int damage = 1;
    public float damageInterval = 1f;

    private float nextDamageTime;
    private EnemyWalkAnimation attackAnimation;

    void Awake()
    {
        attackAnimation = GetComponentInChildren<EnemyWalkAnimation>();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        attackAnimation?.SetTouchingPlayer(true);
        TryDamage(playerHealth);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerHealth>() != null)
            attackAnimation?.SetTouchingPlayer(false);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        PlayerHealth playerHealth = collision.gameObject.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        attackAnimation?.SetTouchingPlayer(true);
        TryDamage(playerHealth);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponentInParent<PlayerHealth>() != null)
            attackAnimation?.SetTouchingPlayer(false);
    }

    void TryDamage(PlayerHealth playerHealth)
    {
        if (playerHealth == null || Time.time < nextDamageTime)
            return;

        attackAnimation?.PlayAttackAnimation();
        playerHealth.TakeDamage(damage);
        nextDamageTime = Time.time + damageInterval;
    }
}
