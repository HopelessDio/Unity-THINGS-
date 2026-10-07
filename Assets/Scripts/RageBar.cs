using UnityEngine;
using UnityEngine.UI;

public class RageBar : MonoBehaviour
{
    public PlayerRage playerRage;
    public Slider slider;
    public Color rageColor = new Color(0.55f, 0.035f, 0.045f, 1f);
    public Color fullRageColor = new Color(1f, 0.16f, 0.04f, 1f);
    public Color activeRageColor = new Color(1f, 0.42f, 0.02f, 1f);
    public float fillSmoothing = 10f;
    public float fullPulseSpeed = 7f;

    private Image fillImage;

    void Start()
    {
        if (playerRage == null)
            playerRage = FindAnyObjectByType<PlayerRage>();
        if (slider == null)
            slider = GetComponent<Slider>();

        if (slider == null || playerRage == null)
        {
            Debug.LogWarning("RageBar needs a Slider and PlayerRage reference.");
            enabled = false;
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = playerRage.maxRage;
        slider.value = playerRage.CurrentRage;
        slider.interactable = false;

        if (slider.fillRect != null)
            fillImage = slider.fillRect.GetComponent<Image>();
        if (fillImage != null)
            fillImage.color = rageColor;
    }

    void Update()
    {
        float smoothing = 1f - Mathf.Exp(-fillSmoothing * Time.deltaTime);
        slider.value = Mathf.Lerp(slider.value, playerRage.CurrentRage, smoothing);

        if (fillImage == null)
            return;

        if (playerRage.IsRaging)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * fullPulseSpeed * 1.5f);
            fillImage.color = Color.Lerp(fullRageColor, activeRageColor, pulse);
        }
        else if (playerRage.IsFull)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * fullPulseSpeed);
            fillImage.color = Color.Lerp(rageColor, fullRageColor, pulse);
        }
        else
        {
            fillImage.color = rageColor;
        }
    }
}
