using TMPro;
using UnityEngine;

namespace Survivor.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class SurvivalTimerUI : MonoBehaviour
    {
        public float ElapsedTime { get; private set; }

        private TMP_Text timerText;

        private void Awake()
        {
            timerText = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            ElapsedTime = 0f;
            UpdateText();
        }

        private void Update()
        {
            ElapsedTime += Time.deltaTime;
            UpdateText();
        }

        private void UpdateText()
        {
            int totalSeconds = Mathf.FloorToInt(ElapsedTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"Time {minutes:00}:{seconds:00}";
        }
    }
}
