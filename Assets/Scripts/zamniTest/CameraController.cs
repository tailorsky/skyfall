using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalClampMin = -70f;
    [SerializeField] private float verticalClampMax = 80f;
    [SerializeField] private float horizontalClampMin = -60f;
    [SerializeField] private float horizontalClampMax = 60f;

    [Header("Sway Settings")]
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAmount = 0.5f;
    [SerializeField] private float swayReturnSpeed = 3f;

    [Header("Breathing Effect")]
    [SerializeField] private bool enableBreathing = true;
    [SerializeField] private float breathingAmount = 0.03f;
    [SerializeField] private float breathingSpeed = 1f;

    [Header("Stress Effect")]
    [SerializeField] private float stressShakeIntensity = 0.02f;

    [Header("Fall Effect")]
    [SerializeField] private float fallTiltAmount = 45f;

    // Компоненты
    private StaminaSystem staminaSystem;

    // Состояние камеры
    private float verticalRotation = 0f;
    private float horizontalRotation = 0f;
    private Vector3 baseLocalPosition;

    // Для плавности
    private float swayX = 0f;
    private float swayY = 0f;
    private float fallRotation = 0f;
    private float stressLevel = 0f;

    // Новый Input System
    private Vector2 mouseDelta;

    private void Start()
    {
        // Блокируем и скрываем курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        baseLocalPosition = transform.localPosition;

        staminaSystem = FindObjectOfType<StaminaSystem>();

        if (staminaSystem != null)
        {
            staminaSystem.OnCriticalStamina += HandleCriticalStamina;
        }
    }

    private void Update()
    {
        // Читаем мышь через новый Input System
        mouseDelta = Mouse.current.delta.ReadValue();

        if (GameManager.Instance.CurrentState == GameManager.GameState.Falling)
        {
            HandleFallCamera();
            return;
        }

        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        HandleMouseLook();
        HandleSway();
        HandleBreathing();
        HandleStressEffect();
    }

    private void HandleMouseLook()
    {
        // Делим на 10 потому что новый Input System возвращает пиксели
        // а не нормализованные значения как старый
        float mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, verticalClampMin, verticalClampMax);

        horizontalRotation += mouseX;
        horizontalRotation = Mathf.Clamp(horizontalRotation, horizontalClampMin, horizontalClampMax);

        Quaternion targetRotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f);
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * 20f
        );
    }

    private void HandleSway()
    {
        if (!enableSway) return;

        float mouseX = mouseDelta.x * 0.01f;
        float mouseY = mouseDelta.y * 0.01f;

        swayX = Mathf.Lerp(swayX, mouseX * swayAmount * 0.01f, Time.deltaTime * swayReturnSpeed);
        swayY = Mathf.Lerp(swayY, mouseY * swayAmount * 0.01f, Time.deltaTime * swayReturnSpeed);

        Vector3 swayOffset = new Vector3(swayX, swayY, 0f);
        transform.localPosition = baseLocalPosition + swayOffset;
    }

    private void HandleBreathing()
    {
        if (!enableBreathing) return;

        float stressMultiplier = 1f;
        if (staminaSystem != null)
        {
            stressLevel = 1f - staminaSystem.StaminaPercent;
            stressMultiplier = Mathf.Lerp(1f, 2.5f, stressLevel);
        }

        float breathY = Mathf.Sin(Time.time * breathingSpeed * stressMultiplier) * breathingAmount;
        float breathX = Mathf.Sin(Time.time * breathingSpeed * stressMultiplier * 0.5f)
                        * breathingAmount * 0.5f;

        transform.localPosition += new Vector3(breathX, breathY, 0f) * (stressMultiplier * 0.5f);
    }

    private void HandleStressEffect()
    {
        if (staminaSystem == null) return;

        if (staminaSystem.IsCritical)
        {
            float shakeX = Random.Range(-stressShakeIntensity, stressShakeIntensity);
            float shakeY = Random.Range(-stressShakeIntensity, stressShakeIntensity);
            transform.localPosition += new Vector3(shakeX, shakeY, 0f)
                                       * (1f - staminaSystem.StaminaPercent);
        }
    }

    private void HandleFallCamera()
    {
        fallRotation = Mathf.Lerp(fallRotation, fallTiltAmount, Time.deltaTime * 2f);

        transform.localRotation = Quaternion.Euler(
            verticalRotation,
            horizontalRotation + fallRotation * Mathf.Sin(Time.time * 3f),
            fallRotation * 0.5f
        );

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            baseLocalPosition + Vector3.down * 0.5f,
            Time.deltaTime * 3f
        );
    }

    private void HandleCriticalStamina()
    {
        Debug.Log("Камера: критическая стамина!");
    }

    public void ResetLook()
    {
        verticalRotation = 0f;
        horizontalRotation = 0f;
        transform.localRotation = Quaternion.identity;
        transform.localPosition = baseLocalPosition;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (staminaSystem != null)
        {
            staminaSystem.OnCriticalStamina -= HandleCriticalStamina;
        }
    }
}