using System;
using UnityEngine;

namespace Survivor.Enemies
{
    public sealed class EnemyHealth : MonoBehaviour
    {
        public static event Action EnemyKilled;

        [SerializeField, Min(1f)] private float maxHealth = 30f;

        public bool IsDead { get; private set; }

        private float currentHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ApplyHealthMultiplier(float multiplier)
        {
            maxHealth *= Mathf.Max(0.01f, multiplier);
            currentHealth = maxHealth;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);

            if (currentHealth <= 0f)
            {
                Die(true);
            }
        }

        public void KillWithoutScore()
        {
            Die(false);
        }

        private void Die(bool awardScore)
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            currentHealth = 0f;

            if (awardScore)
            {
                EnemyKilled?.Invoke();
            }

            Destroy(gameObject);
        }
    }
}
