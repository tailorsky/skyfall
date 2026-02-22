using UnityEngine;
using System;

/// <summary>
/// Главный менеджер настроек
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AudioSettings audioSettings;
    [SerializeField] private KeyBindingsManager keyBindings;
    [SerializeField] private GameplaySettings gameplaySettings;

    public event Action OnSettingsLoaded;
    public event Action OnSettingsSaved;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (audioSettings == null) audioSettings = GetComponent<AudioSettings>();
            if (keyBindings == null) keyBindings = GetComponent<KeyBindingsManager>();
            if (gameplaySettings == null) gameplaySettings = GetComponent<GameplaySettings>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadAllSettings();
    }

    public void LoadAllSettings()
    {
        Debug.Log("📂 Загрузка настроек...");

        audioSettings?.LoadSettings();
        keyBindings?.LoadBindings();
        gameplaySettings?.LoadSettings();

        OnSettingsLoaded?.Invoke();
        Debug.Log("✅ Настройки загружены!");
    }

    public void SaveAllSettings()
    {
        Debug.Log("💾 Сохранение настроек...");

        audioSettings?.SaveSettings();
        keyBindings?.SaveBindings();
        gameplaySettings?.SaveSettings();

        OnSettingsSaved?.Invoke();
        Debug.Log("✅ Настройки сохранены!");
    }

    public void ResetToDefaults()
    {
        Debug.Log("🔄 Сброс настроек...");

        audioSettings?.ResetToDefaults();
        keyBindings?.ResetToDefaults();
        gameplaySettings?.ResetToDefaults();

        SaveAllSettings();
    }

    public AudioSettings Audio => audioSettings;
    public KeyBindingsManager Keys => keyBindings;
    public GameplaySettings Gameplay => gameplaySettings;
}