using UnityEngine;

public class RageVisuals : MonoBehaviour
{
    public PlayerRage playerRage;
    public SpriteRenderer groundRenderer;

    [Header("World Transformation")]
    public Color rageGroundColor = new Color(0.075f, 0.012f, 0.018f, 1f);
    public Color rageCameraColor = new Color(0.018f, 0.006f, 0.01f, 1f);
    public float transitionSpeed = 3.5f;

    [Header("Player Aura")]
    public Color auraColor = new Color(0.95f, 0.035f, 0.015f, 0.65f);
    public Color auraHotColor = new Color(1f, 0.35f, 0.02f, 0.8f);
    public float auraSize = 1.3f;
    public float auraPulseSpeed = 8f;

    private Camera mainCamera;
    private SpriteRenderer playerRenderer;
    private SpriteRenderer auraRenderer;
    private ParticleSystem emberParticles;
    private Color normalGroundColor;
    private Color normalCameraColor;
    private float targetStrength;
    private float currentStrength;

    void Awake()
    {
        if (playerRage == null)
            playerRage = GetComponent<PlayerRage>();

        mainCamera = Camera.main;
        if (groundRenderer == null)
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
                groundRenderer = ground.GetComponent<SpriteRenderer>();
        }

        if (mainCamera != null)
            normalCameraColor = mainCamera.backgroundColor;
        if (groundRenderer != null)
            normalGroundColor = groundRenderer.color;

        FindPlayerRenderer();
        CreateAura();
        CreateEmbers();
    }

    void OnEnable()
    {
        if (playerRage == null)
            playerRage = GetComponent<PlayerRage>();

        if (playerRage != null)
        {
            playerRage.RageStarted += OnRageStarted;
            playerRage.RageEnded += OnRageEnded;
        }
    }

    void Start()
    {
        targetStrength = playerRage != null && playerRage.IsRaging ? 1f : 0f;
    }

    void OnDisable()
    {
        if (playerRage != null)
        {
            playerRage.RageStarted -= OnRageStarted;
            playerRage.RageEnded -= OnRageEnded;
        }

        RestoreNormalLook();
    }

    void Update()
    {
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        currentStrength = Mathf.Lerp(currentStrength, targetStrength, blend);
        if (Mathf.Abs(currentStrength - targetStrength) < 0.002f)
            currentStrength = targetStrength;

        UpdateWorldColors();
        UpdateAura();
        UpdateEmbers();
    }

    void OnRageStarted()
    {
        targetStrength = 1f;
        if (emberParticles != null && !emberParticles.isPlaying)
            emberParticles.Play();
    }

    void OnRageEnded()
    {
        targetStrength = 0f;
    }

    void UpdateWorldColors()
    {
        if (groundRenderer != null)
            groundRenderer.color = Color.Lerp(normalGroundColor, rageGroundColor, currentStrength);
        if (mainCamera != null)
            mainCamera.backgroundColor = Color.Lerp(normalCameraColor, rageCameraColor, currentStrength);
    }

    void UpdateAura()
    {
        if (auraRenderer == null || playerRenderer == null)
            return;

        auraRenderer.enabled = currentStrength > 0.01f;
        if (!auraRenderer.enabled)
            return;

        auraRenderer.sprite = playerRenderer.sprite;
        auraRenderer.flipX = playerRenderer.flipX;
        auraRenderer.flipY = playerRenderer.flipY;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * auraPulseSpeed);
        float scale = auraSize + pulse * 0.12f;
        auraRenderer.transform.localScale = new Vector3(scale, scale, 1f);

        Color color = Color.Lerp(auraColor, auraHotColor, pulse);
        color.a *= currentStrength;
        auraRenderer.color = color;
    }

    void UpdateEmbers()
    {
        if (emberParticles == null)
            return;

        ParticleSystem.EmissionModule emission = emberParticles.emission;
        emission.rateOverTime = 22f * currentStrength;

        if (targetStrength <= 0f && currentStrength < 0.02f && emberParticles.isPlaying)
            emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void FindPlayerRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer candidate in renderers)
        {
            if (candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                playerRenderer = candidate;
                return;
            }
        }
    }

    void CreateAura()
    {
        if (playerRenderer == null)
            return;

        GameObject auraObject = new GameObject("Rage Aura");
        auraObject.transform.SetParent(playerRenderer.transform, false);

        auraRenderer = auraObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = playerRenderer.sprite;
        auraRenderer.sharedMaterial = playerRenderer.sharedMaterial;
        auraRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        auraRenderer.sortingOrder = playerRenderer.sortingOrder - 2;
        auraRenderer.enabled = false;
    }

    void CreateEmbers()
    {
        GameObject emberObject = new GameObject("Rage Embers");
        emberObject.transform.SetParent(transform, false);

        emberParticles = emberObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = emberParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(auraHotColor, auraColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 45;

        ParticleSystem.EmissionModule emission = emberParticles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = emberParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.48f;
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = emberParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.45f, 1.05f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = emberParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(auraHotColor, 0f),
                new GradientColorKey(auraColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ParticleSystemRenderer particleRenderer = emberObject.GetComponent<ParticleSystemRenderer>();
        if (playerRenderer != null)
        {
            particleRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            particleRenderer.sortingOrder = playerRenderer.sortingOrder + 2;
        }

        emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void RestoreNormalLook()
    {
        if (groundRenderer != null)
            groundRenderer.color = normalGroundColor;
        if (mainCamera != null)
            mainCamera.backgroundColor = normalCameraColor;
        if (auraRenderer != null)
            auraRenderer.enabled = false;
        if (emberParticles != null)
            emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
