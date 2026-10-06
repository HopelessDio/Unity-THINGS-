using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public Text timerText;
    public Text killCountText;

    public int KillCount { get; private set; }
    public float SurvivalTime { get; private set; }

    void Awake()
    {
        Instance = this;
        UpdateKillText();
        UpdateTimerText();
    }

    void Update()
    {
        SurvivalTime += Time.deltaTime;
        UpdateTimerText();
    }

    public void AddKill()
    {
        KillCount++;
        UpdateKillText();
    }

    void UpdateKillText()
    {
        killCountText.text = $"Kills: {KillCount}";
    }

    void UpdateTimerText()
    {
        int totalSeconds = Mathf.FloorToInt(SurvivalTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
