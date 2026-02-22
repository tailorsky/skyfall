using UnityEngine;
using System;

public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRecoveryRate = 15f;  // восстановление при 2 руках
    [SerializeField] private float staminaDrainOneHand = 8f;   // расход при 1 руке
    [SerializeField] private float criticalStaminaThreshold = 25f;

    private float currentStamina;
    private int handsGripped = 0;

    // –ежим в котором находитс€ игрок
    public enum StaminaMode
    {
        Idle,       // на земле Ч стамина не трогаетс€ вообще
        Recovering, // 2 руки на скале Ч восстанавливаетс€
        Draining,   // 1 рука на скале Ч тратитс€
    }

    private StaminaMode currentMode = StaminaMode.Idle;

    // √еттеры
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => currentStamina / maxStamina;
    public bool IsCritical => currentStamina <= criticalStaminaThreshold;
    public bool IsExhausted => currentStamina <= 0f;
    public StaminaMode CurrentMode => currentMode;

    // —обыти€
    public event Action OnStaminaExhausted;
    public event Action<float> OnStaminaChanged;
    public event Action OnCriticalStamina;

    private bool criticalEventFired = false;
    private bool exhaustedEventFired = false;

    private void Start()
    {
        currentStamina = maxStamina;
        // Ќачинаем в Idle Ч человек на земле
        SetMode(StaminaMode.Idle);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        UpdateStamina();
    }

    private void UpdateStamina()
    {
        float previousStamina = currentStamina;

        switch (currentMode)
        {
            case StaminaMode.Idle:
                currentStamina += staminaRecoveryRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);

                // —брасываем флаги когда стамина восстановилась
                if (!IsCritical) criticalEventFired = false;
                if (!IsExhausted) exhaustedEventFired = false;
                return;

            case StaminaMode.Recovering:
                // ƒве руки на скале Ч восстанавливаем
                currentStamina += staminaRecoveryRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);

                // —брасываем флаги когда стамина восстановилась
                if (!IsCritical) criticalEventFired = false;
                if (!IsExhausted) exhaustedEventFired = false;
                break;

            case StaminaMode.Draining:
                // ќдна рука Ч тратим
                currentStamina -= staminaDrainOneHand * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
                break;
        }

        // ”ведомл€ем об изменении
        if (Mathf.Abs(previousStamina - currentStamina) > 0.01f)
        {
            OnStaminaChanged?.Invoke(StaminaPercent);
        }

        //  ритический уровень
        if (IsCritical && !criticalEventFired)
        {
            criticalEventFired = true;
            OnCriticalStamina?.Invoke();
            Debug.Log(" ритически мало сил!");
        }

        // —тамина кончилась
        if (IsExhausted && !exhaustedEventFired)
        {
            exhaustedEventFired = true;
            OnStaminaExhausted?.Invoke();
            Debug.Log("—тамина на нуле Ч руки срываютс€!");
        }
    }

    // Ётот метод вызываетс€ из ClimbingManager
    public void SetHandsGripped(int count, bool isOnGround)
    {
        handsGripped = Mathf.Clamp(count, 0, 2);

        // ќпредел€ем режим
        if (isOnGround && count == 0)
        {
            // —тоим на земле и не держимс€ Ч Idle
            SetMode(StaminaMode.Idle);
        }
        else if (count == 2)
        {
            // ƒве руки Ч восстановление
            SetMode(StaminaMode.Recovering);
        }
        else if (count == 1)
        {
            // ќдна рука Ч расход
            SetMode(StaminaMode.Draining);
        }
        else
        {
            // 0 рук в воздухе Ч тоже Idle (падение обрабатываетс€ отдельно)
            SetMode(StaminaMode.Idle);
        }
    }

    private void SetMode(StaminaMode newMode)
    {
        if (currentMode == newMode) return;

        currentMode = newMode;
        Debug.Log($"—тамина режим: {newMode}");
    }

    // —тарый метод оставл€ем дл€ совместимости
    public void SetHandsGripped(int count)
    {
        SetHandsGripped(count, false);
    }

    public void RestoreStamina(float amount)
    {
        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
        OnStaminaChanged?.Invoke(StaminaPercent);
    }

    public void DrainStamina(float amount)
    {
        currentStamina = Mathf.Max(currentStamina - amount, 0f);
        OnStaminaChanged?.Invoke(StaminaPercent);
    }
}