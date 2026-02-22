using UnityEngine;
using System;

/// <summary>
/// Игровые настройки
/// </summary>
public class GameplaySettings : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float defaultMouseSensitivity = 2f;
    [SerializeField] private float defaultFOV = 75f;
    [SerializeField] private bool defaultInvertY = false;

    [Header("Gameplay")]
    [SerializeField] private float defaultDifficulty = 1f;

    // Текущие значения
    private float mouseSensitivity;
    private float fieldOfView;
    private bool invertMouseY;
    private float difficulty;

    // События
    public event Action OnSettingsChanged;

    // ─────────────────────────────────────────
    //  СВОЙСТВА
    // ─────────────────────────────────────────

    public float MouseSensitivity
    {
        get => mouseSensitivity;
        set
        {
            mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f);
            ApplyMouseSensitivity();
            OnSettingsChanged?.Invoke();
        }
    }

    public float FieldOfView
    {
        get => fieldOfView;
        set
        {
            fieldOfView = Mathf.Clamp(value, 50f, 120f);
            ApplyFOV();
            OnSettingsChanged?.Invoke();
        }
    }

    public bool InvertMouseY
    {
        get => invertMouseY;
        set
        {
            invertMouseY = value;
            ApplyMouseInvert();
            OnSettingsChanged?.Invoke();
        }
    }

    public float Difficulty
    {
        get => difficulty;
        set
        {
            difficulty = Mathf.Clamp(value, 0.5f, 3f);
            OnSettingsChanged?.Invoke();
        }
    }

    // ─────────────────────────────────────────
    //  ПРИМЕНЕНИЕ НАСТРОЕК
    // ─────────────────────────────────────────

    private void ApplyMouseSensitivity()
    {
        var player = FindObjectOfType<ClimbingManager>();
        if (player != null)
        {
            player.SetMouseSensitivity(mouseSensitivity);
        }
    }

    private void ApplyFOV()
    {
        var camera = Camera.main;
        if (camera != null)
        {
            camera.fieldOfView = fieldOfView;
        }
    }

    private void ApplyMouseInvert()
    {
        var player = FindObjectOfType<ClimbingManager>();
        if (player != null)
        {
            player.SetInvertY(invertMouseY);
        }
    }

    public void ApplyAllSettings()
    {
        ApplyMouseSensitivity();
        ApplyFOV();
        ApplyMouseInvert();
    }

    // ─────────────────────────────────────────
    //  СОХРАНЕНИЕ / ЗАГРУЗКА
    // ─────────────────────────────────────────

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("GP_Sensitivity", mouseSensitivity);
        PlayerPrefs.SetFloat("GP_FOV", fieldOfView);
        PlayerPrefs.SetInt("GP_InvertY", invertMouseY ? 1 : 0);
        PlayerPrefs.SetFloat("GP_Difficulty", difficulty);
        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("GP_Sensitivity", defaultMouseSensitivity);
        fieldOfView = PlayerPrefs.GetFloat("GP_FOV", defaultFOV);
        invertMouseY = PlayerPrefs.GetInt("GP_InvertY", defaultInvertY ? 1 : 0) == 1;
        difficulty = PlayerPrefs.GetFloat("GP_Difficulty", defaultDifficulty);

        ApplyAllSettings();
    }

    public void ResetToDefaults()
    {
        mouseSensitivity = defaultMouseSensitivity;
        fieldOfView = defaultFOV;
        invertMouseY = defaultInvertY;
        difficulty = defaultDifficulty;

        ApplyAllSettings();
    }
}