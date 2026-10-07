using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRage : MonoBehaviour
{
    public int maxRage = 100;
    [Tooltip("Rage gained for each point of damage dealt to an enemy.")]
    public int ragePerDamageDealt = 5;
    public int ragePerKill = 15;
    public int rageWhenDamaged = 8;
    public float rageDuration = 8f;

    public float CurrentRage { get; private set; }
    public float NormalizedRage => maxRage > 0 ? CurrentRage / maxRage : 0f;
    public bool IsFull => CurrentRage >= maxRage;
    public bool IsRaging { get; private set; }

    public event Action<float, float> RageChanged;
    public event Action RageStarted;
    public event Action RageEnded;

    private PlayerHealth playerHealth;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        CurrentRage = 0;
    }

    void OnEnable()
    {
        EnemyHealth.EnemyDamaged += OnEnemyDamaged;
        EnemyHealth.EnemyDefeated += OnEnemyDefeated;

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.Damaged += OnPlayerDamaged;
    }

    void Start()
    {
        RageChanged?.Invoke(CurrentRage, maxRage);
    }

    void Update()
    {
        if (!IsRaging)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                TryActivateRage();
            return;
        }

        float drainPerSecond = maxRage / Mathf.Max(rageDuration, 0.01f);
        CurrentRage = Mathf.Max(0f, CurrentRage - drainPerSecond * Time.deltaTime);
        RageChanged?.Invoke(CurrentRage, maxRage);

        if (CurrentRage <= 0f)
            EndRage();
    }

    void OnDisable()
    {
        EnemyHealth.EnemyDamaged -= OnEnemyDamaged;
        EnemyHealth.EnemyDefeated -= OnEnemyDefeated;
        if (playerHealth != null)
            playerHealth.Damaged -= OnPlayerDamaged;
    }

    public void AddRage(int amount)
    {
        if (amount <= 0 || IsFull || IsRaging)
            return;

        CurrentRage = Mathf.Clamp(CurrentRage + amount, 0f, maxRage);
        RageChanged?.Invoke(CurrentRage, maxRage);
        Debug.Log($"Rage: {CurrentRage:0}/{maxRage}");
    }

    public void ResetRage()
    {
        bool wasRaging = IsRaging;
        IsRaging = false;
        CurrentRage = 0f;
        RageChanged?.Invoke(CurrentRage, maxRage);

        if (wasRaging)
            RageEnded?.Invoke();
    }

    public bool TryActivateRage()
    {
        if (IsRaging || !IsFull || (playerHealth != null && playerHealth.CurrentHealth <= 0))
            return false;

        IsRaging = true;
        CurrentRage = maxRage;
        RageChanged?.Invoke(CurrentRage, maxRage);
        RageStarted?.Invoke();
        Debug.Log("RAGE STARTED!");
        return true;
    }

    void EndRage()
    {
        if (!IsRaging)
            return;

        IsRaging = false;
        CurrentRage = 0f;
        RageChanged?.Invoke(CurrentRage, maxRage);
        RageEnded?.Invoke();
        Debug.Log("Rage ended.");
    }

    void OnEnemyDefeated()
    {
        AddRage(ragePerKill);
    }

    void OnEnemyDamaged(int damageDealt)
    {
        AddRage(damageDealt * ragePerDamageDealt);
    }

    void OnPlayerDamaged(int damageTaken)
    {
        AddRage(rageWhenDamaged);
    }
}
