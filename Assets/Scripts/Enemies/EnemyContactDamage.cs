using Survivor.Player;
using UnityEngine;

namespace Survivor.Enemies
{
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.05f)] private float damageInterval = 1f;

        private float nextDamageTime;

        private void OnCollisionStay(Collision collision)
        {
            if (Time.time < nextDamageTime ||
                !collision.gameObject.TryGetComponent(out PlayerHealth playerHealth))
            {
                return;
            }

            playerHealth.TakeDamage(damage);
            nextDamageTime = Time.time + damageInterval;
        }
    }
}
