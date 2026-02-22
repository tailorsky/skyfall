using UnityEngine;
using System;

public class StaminaSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainTwoHands = 2f;   // расход при двух руках
    [SerializeField] private float drainOneHand = 6f;  // расход при одной руке
    [SerializeField] private float recoveryRate = 14f;  // восстановление на земле
    [SerializeField] private float criticalThreshold = 25f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private float currentStamina;

    public enum StaminaMode { Idle, TwoHands, OneHand }
    private StaminaMode currentMode = StaminaMode.Idle;

    // Геттеры
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => currentStamina / maxStamina;
    public bool IsCritical => currentStamina <= criticalThreshold;
    public bool IsExhausted => currentStamina <= 0f;
    public StaminaMode CurrentMode => currentMode;

    // ОДНО событие — просто float от 0 до 1
    public event Action<float> OnStaminaChanged;
    public event Action OnCriticalStamina;
    public event Action OnStaminaExhausted;

    private bool criticalFired = false;
    private bool exhaustedFired = false;

    private void Awake()
    {
        currentStamina = maxStamina;
    }

    private void Start()
    {
        // Сразу обновляем UI
        OnStaminaChanged?.Invoke(StaminaPercent);
        Debug.Log($"[Stamina] Старт: {currentStamina:F0}/{maxStamina}");
    }

    private void Update()
    {
        Debug.Log($"[Stamina Update] mode={currentMode} stamina={currentStamina:F1}");
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        float prev = currentStamina;

        switch (currentMode)
        {
            case StaminaMode.Idle:
                // На земле — восстанавливаем
                currentStamina += recoveryRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
                if (!IsCritical) criticalFired = false;
                if (!IsExhausted) exhaustedFired = false;
                break;

            case StaminaMode.TwoHands:
                // Две руки — медленный расход
                currentStamina -= drainTwoHands * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
                break;

            case StaminaMode.OneHand:
                // Одна рука — быстрый расход
                currentStamina -= drainOneHand * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
                break;
        }

        // Логируем каждые 30 кадров
        if (showDebugInfo && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Stamina] {currentMode} | " +
                      $"{currentStamina:F1}/{maxStamina} | " +
                      $"{StaminaPercent * 100:F0}%");
        }

        // Уведомляем UI если изменилось
        if (Mathf.Abs(prev - currentStamina) > 0.05f)
        {
            OnStaminaChanged?.Invoke(StaminaPercent);
        }

        // Критический уровень
        if (IsCritical && !criticalFired)
        {
            criticalFired = true;
            OnCriticalStamina?.Invoke();
            Debug.Log("[Stamina] КРИТИЧЕСКИЙ УРОВЕНЬ!");
        }

        // Истощение
        if (IsExhausted && !exhaustedFired)
        {
            exhaustedFired = true;
            OnStaminaExhausted?.Invoke();
            Debug.Log("[Stamina] СТАМИНА КОНЧИЛАСЬ!");
        }
    }

    public void SetHandsGripped(int count, bool isOnGround)
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

        if (newMode == currentMode) return;

        currentMode = newMode;
        Debug.Log($"[Stamina] Режим: {currentMode} | Стамина: {currentStamina:F1}");
    }

    public void SetHandsGripped(int count)
    {
        SetHandsGripped(count, false);
    }

    public void DrainStamina(float amount)
    {
        currentStamina = Mathf.Max(currentStamina - amount, 0f);
        OnStaminaChanged?.Invoke(StaminaPercent);
    }
}