using Survivor.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor.UI
{
    [RequireComponent(typeof(Slider))]
    public sealed class DashCooldownUI : MonoBehaviour
    {
        [SerializeField] private PlayerMovement playerMovement;

        private Slider cooldownSlider;

        private void Awake()
        {
            cooldownSlider = GetComponent<Slider>();
            cooldownSlider.minValue = 0f;
            cooldownSlider.maxValue = 1f;
            cooldownSlider.interactable = false;
        }

        private void Start()
        {
            if (playerMovement == null)
            {
                Debug.LogError("Dash Cooldown UI needs a Player Movement reference.", this);
                enabled = false;
                return;
            }

            UpdateCooldownBar();
        }

        private void Update()
        {
            UpdateCooldownBar();
        }

        private void UpdateCooldownBar()
        {
            cooldownSlider.value = playerMovement.DashCooldownProgress;
        }
    }
}
