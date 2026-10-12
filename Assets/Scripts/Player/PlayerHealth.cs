using System;
using UnityEngine;

namespace Survivor.Player
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        private PlayerMovement playerMovement;
        private float invulnerableUntil;

        private void Awake()
        {
            playerMovement = GetComponent<PlayerMovement>();
            CurrentHealth = maxHealth;
        }

        private void OnEnable()
        {
            if (playerMovement != null)
            {
                playerMovement.DashStarted += GrantDashInvulnerability;
            }
        }

        private void OnDisable()
        {
            if (playerMovement != null)
            {
                playerMovement.DashStarted -= GrantDashInvulnerability;
            }
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || IsInvulnerable || damage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            Debug.Log($"Player health: {CurrentHealth}/{maxHealth}", this);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            IsDead = true;
            Died?.Invoke();
            Debug.Log("Player defeated!", this);
        }

        private void GrantDashInvulnerability(float duration)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
        }
    }
}
