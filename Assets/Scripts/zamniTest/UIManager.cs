using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Stamina UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Color fullColor = Color.green;
    [SerializeField] private Color lowColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Hand Indicators")]
    [SerializeField] private Image leftHandIndicator;
    [SerializeField] private Image rightHandIndicator;
    [SerializeField] private Sprite grippedSprite;   // спрайт сжатой руки
    [SerializeField] private Sprite releasedSprite;  // спрайт разжатой руки

    [Header("Info")]
    [SerializeField] private TextMeshProUGUI heightText;
    [SerializeField] private TextMeshProUGUI staminaText;  // необязательно — цифры стамины

    [Header("Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject tutorialPanel;

    [Header("Warning")]
    [SerializeField] private TextMeshProUGUI warningText;

    [Header("Crosshair")]
    [SerializeField] private Image crosshair;

    // Компоненты
    private StaminaSystem staminaSystem;
    private ClimbingManager climbingManager;
    private HandController leftHand;
    private HandController rightHand;

    // Плавное изменение слайдера
    private float displayedStamina = 1f;
    private float targetStamina = 1f;

    private Coroutine warningCoroutine;

    private void Start()
    {
        if (rightHandIndicator != null)
        {
            rightHandIndicator.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        // ИЩЕМ ЧЕРЕЗ CLIMBINGMANAGER — ОН ТОЧНО НА PLAYER
        climbingManager = FindObjectOfType<ClimbingManager>();

        if (climbingManager != null)
        {
            staminaSystem = climbingManager.GetComponent<StaminaSystem>();
            Debug.Log($"[UI] StaminaSystem найден на: {staminaSystem?.gameObject.name}");
        }
        else
        {
            Debug.LogError("[UI] ClimbingManager не найден!");
        }
        // Находим компоненты
        if (staminaSystem != null)
        {
            Debug.Log($"[UI] Нашёл StaminaSystem на объекте: {staminaSystem.gameObject.name}");
        }

        HandController[] hands = FindObjectsOfType<HandController>();
        foreach (var h in hands)
        {
            if (h.Side == HandController.HandSide.Left) leftHand = h;
            if (h.Side == HandController.HandSide.Right) rightHand = h;
        }

        // Подписки на StaminaSystem
        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaChanged += OnStaminaChanged;
            staminaSystem.OnCriticalStamina += OnCriticalStamina;
            staminaSystem.OnStaminaExhausted += OnStaminaExhausted;

            // Инициализируем слайдер сразу
            targetStamina = staminaSystem.StaminaPercent;
            displayedStamina = targetStamina;
            ApplyStaminaToSlider(displayedStamina);

            Debug.Log($"[UI] Стамина при старте: {targetStamina * 100:F0}%");
        }
        else
        {
            Debug.LogError("[UI] StaminaSystem НЕ НАЙДЕН!");
        }

        // Подписки на GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += ShowGameOver;
            GameManager.Instance.OnWin += ShowWin;
        }

        // Прячем панели
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        // Читаем стамину НАПРЯМУЮ каждый кадр
        if (staminaSystem != null)
        {
            float newTarget = staminaSystem.StaminaPercent;

            // Логируем только когда меняется
            if (Mathf.Abs(newTarget - targetStamina) > 0.01f)
            {
                Debug.Log($"[UI] Стамина изменилась: {newTarget * 100:F0}%");
            }

            targetStamina = newTarget;
        }
        else
        {
            // Пробуем найти снова
            if (climbingManager != null)
            {
                staminaSystem = climbingManager.GetComponent<StaminaSystem>();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (tutorialPanel != null && tutorialPanel.activeSelf)
                CloseTutorial();
        }

        SmoothStaminaSlider();
        UpdateHandIndicators();
        UpdateHeightText();
        UpdateCrosshair();
    }

    // ─── СТАМИНА ───

    private void OnStaminaChanged(float percent)
    {
        targetStamina = percent;
        Debug.Log($"[UI] Получили OnStaminaChanged: {percent * 100:F0}%");
    }

    private void SmoothStaminaSlider()
    {
        // Плавно двигаем к цели
        displayedStamina = Mathf.MoveTowards(
            displayedStamina,
            targetStamina,
            Time.deltaTime * 2f
        );

        ApplyStaminaToSlider(displayedStamina);

        // ВРЕМЕННЫЙ ТЕСТ — каждые 60 кадров выводим всё
        // if (Time.frameCount % 60 == 0)
        // {
        //     Debug.Log($"[SLIDER TEST] target={targetStamina:F2} displayed={displayedStamina:F2}");

        //     if (staminaSlider != null)
        //     {
        //         Debug.Log($"[SLIDER TEST] slider.value={staminaSlider.value:F2} " +
        //                   $"slider.minValue={staminaSlider.minValue} " +
        //                   $"slider.maxValue={staminaSlider.maxValue}");
        //     }
        //     else
        //     {
        //         Debug.LogError("[SLIDER TEST] staminaSlider = NULL!");
        //     }
        // }
    }

    private void ApplyStaminaToSlider(float percent)
    {
        // Обновляем слайдер
        if (staminaSlider != null)
            staminaSlider.value = percent;

        // Обновляем цвет
        if (staminaFill != null)
        {
            Color c;
            if (percent > 0.5f)
                c = Color.Lerp(lowColor, fullColor, (percent - 0.5f) * 2f);
            else
                c = Color.Lerp(criticalColor, lowColor, percent * 2f);

            // Мигание при критическом уровне
            if (percent < 0.25f)
            {
                float pulse = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                c = Color.Lerp(c, Color.white, pulse * 0.4f);
            }

            staminaFill.color = c;
        }

        // Текст с цифрами (необязательно)
        if (staminaText != null && staminaSystem != null)
        {
            staminaText.text = $"{staminaSystem.CurrentStamina:F0} / {staminaSystem.MaxStamina:F0}";
        }
    }

    private void OnCriticalStamina()
    {
        ShowWarning("NOT ENOUGH STRENGTH!");
    }

    private void OnStaminaExhausted()
    {
        ShowWarning("MY HANDS ARE TIRED!");
    }

    public void OnRestartButton()
    {
        Debug.Log("ВСЁ НАХУЙ!");
        Time.timeScale = 1f; // Сбрасываем паузу если была
        GameManager.Instance?.RestartGame();
    }

    public void OnQuitButton()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // ─── РУКИ ───

    private void UpdateHandIndicators()
    {
        if (leftHand != null && leftHandIndicator != null)
        {
            leftHandIndicator.sprite = leftHand.IsGripped ? grippedSprite : releasedSprite;
        }

        if (rightHand != null && rightHandIndicator != null)
        {
            rightHandIndicator.sprite = rightHand.IsGripped ? grippedSprite : releasedSprite;
        }
    }

    // ─── ВЫСОТА ───

    private void UpdateHeightText()
    {
        if (heightText == null || climbingManager == null) return;

        float h = climbingManager.transform.position.y;

        if (GameManager.Instance != null)
        {
            float maxH = GameManager.Instance.WinHeight;
            float pct = Mathf.Clamp01(h / maxH) * 100f;
            heightText.text = $"{h:F1} meters";
        }
        else
        {
            heightText.text = $"{h:F1} meters";
        }
    }

    // ─── ПРИЦЕЛ ───

    private void UpdateCrosshair()
    {
        if (crosshair == null) return;

        bool gripped = (leftHand != null && leftHand.IsGripped) ||
                       (rightHand != null && rightHand.IsGripped);

        crosshair.color = gripped ? Color.green : Color.white;
    }

    // ─── ПРЕДУПРЕЖДЕНИЕ ───

    public void ShowWarning(string message)
    {
        if (warningCoroutine != null)
            StopCoroutine(warningCoroutine);

        warningCoroutine = StartCoroutine(FlashWarning(message));
    }

    private IEnumerator FlashWarning(string message)
    {
        if (warningText == null) yield break;

        warningText.text = message;
        warningText.gameObject.SetActive(true);

        float t = 0f;
        while (t < 3f)
        {
            float alpha = Mathf.Abs(Mathf.Sin(t * 3f));
            warningText.color = new Color(1f, 0.2f, 0.2f, alpha);
            t += Time.deltaTime;
            yield return null;
        }

        warningText.gameObject.SetActive(false);
    }

    // ─── ПАНЕЛИ ───

    private void ShowGameOver()
    {
        StartCoroutine(ShowGameOverDelayed());
    }

    private IEnumerator ShowGameOverDelayed()
    {
        yield return new WaitForSeconds(1.5f);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowWin()
    {
        if (winPanel != null) winPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaChanged -= OnStaminaChanged;
            staminaSystem.OnCriticalStamina -= OnCriticalStamina;
            staminaSystem.OnStaminaExhausted -= OnStaminaExhausted;
        }
    }

    public void CloseTutorial()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.visible = false;
    }
}