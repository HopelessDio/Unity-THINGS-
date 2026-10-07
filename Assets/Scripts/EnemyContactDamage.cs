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
        TryDamage(other.GetComponentInParent<PlayerHealth>());
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.gameObject.GetComponentInParent<PlayerHealth>());
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
