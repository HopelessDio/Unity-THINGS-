using Survivor.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor.UI
{
    public sealed class GameOverUI : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GameObject gameOverPanel;

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died += ShowGameOver;
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= ShowGameOver;
            }
        }

        private void ShowGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            Time.timeScale = 0f;
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
