using UnityEngine;
using System;

public class StaminaSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxStamina       = 100f;
    [SerializeField] private float drainTwoHands    = 2f;
    [SerializeField] private float drainOneHand     = 6f;
    [SerializeField] private float drainSprint      = 10f;  // расход при беге
    [SerializeField] private float recoveryRate     = 14f;
    [SerializeField] private float criticalThreshold = 25f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private float currentStamina;
    private bool  isSprinting = false;

    public enum StaminaMode { Idle, Sprinting, TwoHands, OneHand }
    private StaminaMode currentMode = StaminaMode.Idle;

    public float CurrentStamina  => currentStamina;
    public float MaxStamina      => maxStamina;
    public float StaminaPercent  => currentStamina / maxStamina;
    public bool  IsCritical      => currentStamina <= criticalThreshold;
    public bool  IsExhausted     => currentStamina <= 0f;
    public StaminaMode CurrentMode => currentMode;

    public event Action<float> OnStaminaChanged;
    public event Action        OnCriticalStamina;
    public event Action        OnStaminaExhausted;

    private bool criticalFired  = false;
    private bool exhaustedFired = false;

    private void Awake() => currentStamina = maxStamina;

    private void Start() => OnStaminaChanged?.Invoke(StaminaPercent);

    private void Update()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        float prev = currentStamina;

        switch (currentMode)
        {
            case StaminaMode.Idle:
                currentStamina += recoveryRate * Time.deltaTime;
                currentStamina  = Mathf.Min(currentStamina, maxStamina);
                if (!IsCritical)  criticalFired  = false;
                if (!IsExhausted) exhaustedFired = false;
                break;

            case StaminaMode.Sprinting:
                currentStamina -= drainSprint * Time.deltaTime;
                currentStamina  = Mathf.Max(currentStamina, 0f);
                break;

            case StaminaMode.TwoHands:
                currentStamina -= drainTwoHands * Time.deltaTime;
                currentStamina  = Mathf.Max(currentStamina, 0f);
                break;

            case StaminaMode.OneHand:
                currentStamina -= drainOneHand * Time.deltaTime;
                currentStamina  = Mathf.Max(currentStamina, 0f);
                break;
        }

        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"[Stamina] {currentMode} | {currentStamina:F1}/{maxStamina} | {StaminaPercent * 100:F0}%");

        if (Mathf.Abs(prev - currentStamina) > 0.05f)
            OnStaminaChanged?.Invoke(StaminaPercent);

        if (IsCritical && !criticalFired)
        {
            criticalFired = true;
            OnCriticalStamina?.Invoke();
        }

        if (IsExhausted && !exhaustedFired)
        {
            exhaustedFired = true;
            OnStaminaExhausted?.Invoke();
        }
    }

    // ── Публичные методы ─────────────────────────────────────

    /// <summary>Вызывается из PlayerWalking каждый кадр.</summary>
    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
        RefreshMode();
    }

    /// <summary>Вызывается из ClimbingController.</summary>
    public void SetHandsGripped(int count, bool isOnGround = false)
    {
        StaminaMode newMode;

        if (isOnGround && count == 0)
            newMode = StaminaMode.Idle;
        else if (count == 2)
            newMode = StaminaMode.TwoHands;
        else if (count == 1)
            newMode = StaminaMode.OneHand;
        else
            newMode = StaminaMode.Idle;

        // Лазание имеет приоритет над бегом
        currentMode = newMode;
        if (showDebugInfo)
            Debug.Log($"[Stamina] Режим: {currentMode} | Стамина: {currentStamina:F1}");
    }

    public void DrainStamina(float amount)
    {
        currentStamina = Mathf.Max(currentStamina - amount, 0f);
        OnStaminaChanged?.Invoke(StaminaPercent);
    }

    // ── Приватные ─────────────────────────────────────────────

    private void RefreshMode()
    {
        // Бег только если не лазаем
        if (currentMode == StaminaMode.TwoHands || currentMode == StaminaMode.OneHand)
            return;

        currentMode = isSprinting ? StaminaMode.Sprinting : StaminaMode.Idle;
    }
}