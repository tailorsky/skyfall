using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Stamina UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Color fullStaminaColor = Color.green;
    [SerializeField] private Color lowStaminaColor = Color.red;
    [SerializeField] private Color criticalStaminaColor = Color.red;

    [Header("Hand Indicators")]
    [SerializeField] private Image leftHandIndicator;
    [SerializeField] private Image rightHandIndicator;
    [SerializeField] private Color grippedColor = new Color(0f, 1f, 0f, 0.8f);
    [SerializeField] private Color releasedColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] private Color reachingColor = new Color(1f, 1f, 0f, 0.8f);

    [Header("Info Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TextMeshProUGUI heightText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Crosshair")]
    [SerializeField] private Image crosshair;
    [SerializeField] private Color normalCrosshairColor = Color.white;
    [SerializeField] private Color gripCrosshairColor = Color.green;

    [Header("Warning Text")]
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private float warningFlashSpeed = 2f;

    // Компоненты
    private StaminaSystem staminaSystem;
    private ClimbingManager climbingManager;
    private HandController leftHand;
    private HandController rightHand;

    private bool showingWarning = false;
    private Coroutine warningCoroutine;

    private void Start()
    {
        staminaSystem = FindObjectOfType<StaminaSystem>();
        climbingManager = FindObjectOfType<ClimbingManager>();

        HandController[] hands = FindObjectsOfType<HandController>();
        foreach (var hand in hands)
        {
            if (hand.Side == HandController.HandSide.Left) leftHand = hand;
            if (hand.Side == HandController.HandSide.Right) rightHand = hand;
        }

        // Подписки
        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaChanged += UpdateStaminaUI;
            staminaSystem.OnCriticalStamina += ShowCriticalWarning;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += ShowGameOver;
            GameManager.Instance.OnWin += ShowWin;
        }

        // Инициализация UI
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateHandIndicators();
        UpdateHeightDisplay();
        UpdateCrosshair();
    }

    private void UpdateStaminaUI(float staminaPercent)
    {
        if (staminaSlider != null)
        {
            staminaSlider.value = staminaPercent;
        }

        if (staminaFill != null)
        {
            staminaFill.color = Color.Lerp(lowStaminaColor, fullStaminaColor, staminaPercent);

            // Мигание при критическом уровне
            if (staminaPercent < 0.25f)
            {
                float pulse = Mathf.Abs(Mathf.Sin(Time.time * 4f));
                staminaFill.color = Color.Lerp(criticalStaminaColor, Color.white, pulse * 0.3f);
            }
        }
    }

    private void UpdateHandIndicators()
    {
        if (leftHand != null && leftHandIndicator != null)
        {
            leftHandIndicator.color = leftHand.IsGripped ? grippedColor :
                                     leftHand.IsReaching ? reachingColor : releasedColor;
        }

        if (rightHand != null && rightHandIndicator != null)
        {
            rightHandIndicator.color = rightHand.IsGripped ? grippedColor :
                                      rightHand.IsReaching ? reachingColor : releasedColor;
        }
    }

    private void UpdateHeightDisplay()
    {
        if (heightText != null && climbingManager != null)
        {
            float height = climbingManager.transform.position.y;
            float maxHeight = GameManager.Instance.WinHeight;
            float percent = Mathf.Clamp01(height / maxHeight) * 100f;
            heightText.text = $"Высота: {height:F1}м ({percent:F0}%)";
        }
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null) return;

        // Меняем цвет прицела при зацеплении
        bool anyGripped = (leftHand != null && leftHand.IsGripped) ||
                          (rightHand != null && rightHand.IsGripped);

        crosshair.color = anyGripped ? gripCrosshairColor : normalCrosshairColor;
    }

    private void ShowCriticalWarning()
    {
        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
        }
        warningCoroutine = StartCoroutine(FlashWarning("МАЛО СИЛ!"));
    }

    private IEnumerator FlashWarning(string message)
    {
        if (warningText == null) yield break;

        warningText.text = message;
        warningText.gameObject.SetActive(true);

        float duration = 3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float alpha = Mathf.Abs(Mathf.Sin(elapsed * warningFlashSpeed * Mathf.PI));
            warningText.color = new Color(1f, 0.2f, 0.2f, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        warningText.gameObject.SetActive(false);
    }

    private void ShowGameOver()
    {
        StartCoroutine(ShowGameOverDelayed());
    }

    private IEnumerator ShowGameOverDelayed()
    {
        yield return new WaitForSeconds(1.5f);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Разблокируем курсор
        FindObjectOfType<CameraController>()?.UnlockCursor();
    }

    private void ShowWin()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
        FindObjectOfType<CameraController>()?.UnlockCursor();
    }

    public void OnRestartButton()
    {
        GameManager.Instance.RestartGame();
    }

    private void OnDestroy()
    {
        if (staminaSystem != null)
        {
            staminaSystem.OnStaminaChanged -= UpdateStaminaUI;
            staminaSystem.OnCriticalStamina -= ShowCriticalWarning;
        }
    }
}