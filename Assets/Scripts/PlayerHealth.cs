using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 5;
    public GameOverScreen gameOverScreen;

    public int CurrentHealth { get; private set; }

    void Awake()
    {
        Time.timeScale = 1f;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
        Debug.Log($"Player health: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth == 0)
        {
            Debug.Log("Game Over!");
            if (gameOverScreen != null)
                gameOverScreen.Show();

            Time.timeScale = 0f;
        }
    }
}
