using UnityEngine;
using System;

public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRecoveryRate = 15f;   // В сек при двух руках
    [SerializeField] private float staminaDrainOneHand = 8f;    // В сек при одной руке
    [SerializeField] private float staminaDrainNoHands = 50f;   // В сек без рук (быстро падает)
    [SerializeField] private float criticalStaminaThreshold = 25f;

    private float currentStamina;
    private int handsGripped = 2; // Начинаем как будто держимся двумя руками
    private bool climbingStarted = false; // Флаг что лазание началось

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => currentStamina / maxStamina;
    public bool IsCritical => currentStamina <= criticalStaminaThreshold;
    public bool IsExhausted => currentStamina <= 0f;

    // События
    public event Action OnStaminaExhausted;
    public event Action<float> OnStaminaChanged;
    public event Action OnCriticalStamina;

    private bool criticalEventFired = false;
    private bool exhaustedEventFired = false;

    private void Start()
    {
        currentStamina = maxStamina;
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        UpdateStamina();
    }

    private void UpdateStamina()
    {
        // Пока игрок не начал лазать — стамина не тратится
        if (!climbingStarted)
        {
            // Проверяем не начал ли игрок
            if (handsGripped < 2)
            {
                // Только если хоть раз зацепился
                climbingStarted = true;
            }
            else
            {
                return; // Ещё не начали, ничего не делаем
            }
        }

        float previousStamina = currentStamina;

        switch (handsGripped)
        {
            case 2:
                currentStamina += staminaRecoveryRate * Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
                criticalEventFired = false;
                exhaustedEventFired = false;
                break;

            case 1:
                currentStamina -= staminaDrainOneHand * Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
                break;

            case 0:
                currentStamina -= staminaDrainNoHands * Time.deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
                break;
        }

        if (Mathf.Abs(previousStamina - currentStamina) > 0.01f)
        {
            OnStaminaChanged?.Invoke(StaminaPercent);
        }

        if (IsCritical && !criticalEventFired)
        {
            criticalEventFired = true;
            OnCriticalStamina?.Invoke();
        }

        if (IsExhausted && !exhaustedEventFired)
        {
            exhaustedEventFired = true;
            OnStaminaExhausted?.Invoke();
        }
    }

    // И МЕНЯЕМ SetHandsGripped:
    public void SetHandsGripped(int count)
    {
        handsGripped = Mathf.Clamp(count, 0, 2);

        // Как только игрок взялся — лазание началось
        if (count > 0)
        {
            climbingStarted = true;
        }
    }

    public void RestoreStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);
        OnStaminaChanged?.Invoke(StaminaPercent);
    }

    public void DrainStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina);
        OnStaminaChanged?.Invoke(StaminaPercent);
    }
}