using Survivor.Enemies;
using TMPro;
using UnityEngine;

namespace Survivor.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ScoreUI : MonoBehaviour
    {
        [SerializeField, Min(1)] private int pointsPerKill = 10;

        public int Score { get; private set; }

        private TMP_Text scoreText;

        private void Awake()
        {
            scoreText = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            EnemyHealth.EnemyKilled += AddKillScore;
        }

        private void Start()
        {
            Score = 0;
            UpdateText();
        }

        private void OnDisable()
        {
            EnemyHealth.EnemyKilled -= AddKillScore;
        }

        private void AddKillScore()
        {
            Score += pointsPerKill;
            UpdateText();
        }

        private void UpdateText()
        {
            scoreText.text = $"Score: {Score}";
        }
    }
}
