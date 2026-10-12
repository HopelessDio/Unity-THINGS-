using Survivor.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    [RequireComponent(typeof(Slider))]
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;

        private Slider healthSlider;

        private void Awake()
        {
            healthSlider = GetComponent<Slider>();
            healthSlider.minValue = 0f;
            healthSlider.interactable = false;
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged += UpdateHealthBar;
            }
        }

        private void Start()
        {
            if (playerHealth == null)
            {
                Debug.LogError("Player Health Bar needs a Player Health reference.", this);
                enabled = false;
                return;
            }

            UpdateHealthBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= UpdateHealthBar;
            }
        }

        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }
}
