using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    public GameObject gameOverPanel;

    private Canvas screenCanvas;
    private PlayerHealth playerHealth;
    private bool isShowing;

    void Awake()
    {
        screenCanvas = GetComponent<Canvas>();
        transform.localScale = Vector3.one;
        gameOverPanel.SetActive(false);
    }

    void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
    }

    void Update()
    {
        if (!isShowing && playerHealth != null && playerHealth.CurrentHealth <= 0)
            Show();
    }

    public void Show()
    {
        isShowing = true;
        screenCanvas.enabled = true;
        screenCanvas.overrideSorting = true;
        screenCanvas.sortingOrder = 1000;
        transform.localScale = Vector3.one;
        gameOverPanel.transform.localScale = Vector3.one;
        gameOverPanel.SetActive(true);
        Debug.Log("Game Over screen shown.");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
