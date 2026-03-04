using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает исключительно за вращение камеры по мыши.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookUpAngle   = 80f;
    [SerializeField] private float maxLookDownAngle = 80f;
    [SerializeField] private bool  invertMouseY     = false;
    [SerializeField] private bool  lockCursor       = true;

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    private float rotationX = 0f;
    private float rotationY = 0f;

    private void Start()
    {
        rotationY = transform.eulerAngles.y;
        ApplyCursorLock();
    }

    private void Update()
    {
        if (lockCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            // Не возвращаем лок если стоит пауза
            bool paused = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            if (!paused) ApplyCursorLock();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;
        bool paused = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        if (lockCursor && !paused) ApplyCursorLock();
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void Tick()
    {
        // Не вращаем камеру на паузе
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        var mouse = Mouse.current;
        if (mouse == null || playerCamera == null) return;

        Vector2 delta = mouse.delta.ReadValue();

        rotationY += delta.x * mouseSensitivity * 0.1f;

        float mouseY = delta.y * mouseSensitivity * 0.1f;
        if (!invertMouseY) mouseY = -mouseY;
        rotationX = Mathf.Clamp(rotationX + mouseY, -maxLookUpAngle, maxLookDownAngle);

        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    // ── Публичные настройки ───────────────────────────────────
    public void SetSensitivity(float value) => mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f);
    public void SetInvertY(bool invert)     => invertMouseY = invert;
    public void SetCursorLocked(bool locked)
    {
        lockCursor = locked;
        if (locked) ApplyCursorLock();
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
    }

    public Camera PlayerCamera => playerCamera;

    private void ApplyCursorLock()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
}