using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    public int damage = 1;
    public float damageInterval = 1f;

    private float nextDamageTime;

    void OnTriggerStay2D(Collider2D other)
    {
        if (Time.time < nextDamageTime || !other.TryGetComponent(out PlayerHealth playerHealth))
            return;

        playerHealth.TakeDamage(damage);
        nextDamageTime = Time.time + damageInterval;
    }
}
