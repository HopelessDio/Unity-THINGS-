using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    public Text timerText;
    public Text killCountText;

    public int KillCount { get; private set; }
    public float SurvivalTime { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateHUDIfMissing()
    {
        if (FindAnyObjectByType<GameHUD>() != null)
            return;

        GameObject hudObject = new GameObject("Game HUD");
        hudObject.AddComponent<GameHUD>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateKillCounterIfMissing();
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
        if (killCountText != null)
            killCountText.text = $"Kills: {KillCount}";
    }

    void UpdateTimerText()
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.FloorToInt(SurvivalTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    void CreateKillCounterIfMissing()
    {
        if (killCountText != null)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        GameObject textObject = new GameObject(
            "Kill Counter",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text),
            typeof(Outline));
        textObject.transform.SetParent(canvas.transform, false);

        killCountText = textObject.GetComponent<Text>();
        killCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        killCountText.fontSize = 30;
        killCountText.fontStyle = FontStyle.Bold;
        killCountText.alignment = TextAnchor.UpperLeft;
        killCountText.color = Color.white;
        killCountText.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(300f, 50f);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
