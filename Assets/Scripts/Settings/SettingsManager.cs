using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Хранит и применяет все настройки игры. Синглтон — живёт между сценами.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // ── Ключи PlayerPrefs ─────────────────────────────────────
    private const string KEY_SENSITIVITY  = "sensitivity";
    private const string KEY_VOLUME       = "volume";
    private const string KEY_QUALITY      = "quality";
    private const string KEY_FULLSCREEN   = "fullscreen";
    private const string KEY_RES_WIDTH    = "res_width";
    private const string KEY_RES_HEIGHT   = "res_height";

    // ── Текущие значения ──────────────────────────────────────
    public float Sensitivity  { get; private set; } = 2f;
    public float Volume       { get; private set; } = 1f;
    public int   Quality      { get; private set; } = 2;
    public bool  Fullscreen   { get; private set; } = true;
    public int   ResWidth     { get; private set; } = 1920;
    public int   ResHeight    { get; private set; } = 1080;

    // Все доступные разрешения
    public Resolution[] Resolutions { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Resolutions = Screen.resolutions;
            LoadAll();
            ApplyAll();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── Загрузка / сохранение ─────────────────────────────────

    private void LoadAll()
    {
        Sensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, 2f);
        Volume      = PlayerPrefs.GetFloat(KEY_VOLUME, 1f);
        Quality     = PlayerPrefs.GetInt(KEY_QUALITY, QualitySettings.GetQualityLevel());
        Fullscreen  = PlayerPrefs.GetInt(KEY_FULLSCREEN, 1) == 1;
        ResWidth    = PlayerPrefs.GetInt(KEY_RES_WIDTH,  Screen.currentResolution.width);
        ResHeight   = PlayerPrefs.GetInt(KEY_RES_HEIGHT, Screen.currentResolution.height);
    }

    public void SaveAll()
    {
        PlayerPrefs.SetFloat(KEY_SENSITIVITY, Sensitivity);
        PlayerPrefs.SetFloat(KEY_VOLUME,      Volume);
        PlayerPrefs.SetInt(KEY_QUALITY,       Quality);
        PlayerPrefs.SetInt(KEY_FULLSCREEN,    Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KEY_RES_WIDTH,     ResWidth);
        PlayerPrefs.SetInt(KEY_RES_HEIGHT,    ResHeight);
        PlayerPrefs.Save();
    }

    // ── Применение всех настроек сразу ───────────────────────

    public void ApplyAll()
    {
        ApplyVolume();
        ApplyQuality();
        ApplyDisplay();
        ApplySensitivity(); // применяется когда игрок загружен
    }

    // ── Сеттеры (вызываются из UI) ────────────────────────────

    public void SetSensitivity(float value)
    {
        Sensitivity = Mathf.Clamp(value, 0.1f, 10f);

        // Применяем к PlayerLook если он есть на сцене
        var look = FindObjectOfType<PlayerLook>();
        look?.SetSensitivity(Sensitivity);
    }

    public void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        ApplyVolume();
    }

    public void SetQuality(int index)
    {
        Quality = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        ApplyQuality();
    }

    public void SetFullscreen(bool value)
    {
        Fullscreen = value;
        ApplyDisplay();
    }

    public void SetResolution(int width, int height)
    {
        ResWidth  = width;
        ResHeight = height;
        ApplyDisplay();
    }

    public void SetResolutionByIndex(int index)
    {
        if (index < 0 || index >= Resolutions.Length) return;
        SetResolution(Resolutions[index].width, Resolutions[index].height);
    }

    // ── Приватное применение ──────────────────────────────────

    private void ApplyVolume()
        => AudioListener.volume = Volume;

    private void ApplyQuality()
        => QualitySettings.SetQualityLevel(Quality, true);

    private void ApplyDisplay()
        => Screen.SetResolution(ResWidth, ResHeight, Fullscreen);

    private void ApplySensitivity()
    {
        var look = FindObjectOfType<PlayerLook>();
        look?.SetSensitivity(Sensitivity);
    }

    // Применяем чувствительность когда загружается новая сцена
    private void OnEnable()
        => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
        => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        => ApplySensitivity();
}