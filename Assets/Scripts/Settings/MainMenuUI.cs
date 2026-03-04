using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// UI главного меню. Подключи все элементы в инспекторе.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings — Mouse")]
    [SerializeField] private Slider    sensitivitySlider;
    [SerializeField] private TMP_Text  sensitivityValue;

    [Header("Settings — Audio")]
    [SerializeField] private Slider    volumeSlider;
    [SerializeField] private TMP_Text  volumeValue;

    [Header("Settings — Graphics")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle       fullscreenToggle;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        ShowMain();
        InitSettings();
    }

    // ── Навигация ─────────────────────────────────────────────

    public void ShowMain()
    {
        mainPanel?.SetActive(true);
        settingsPanel?.SetActive(false);
    }

    public void ShowSettings()
    {
        mainPanel?.SetActive(false);
        settingsPanel?.SetActive(true);
    }

    // ── Кнопки главного меню ──────────────────────────────────

    public void OnPlayButton()
        => SceneManager.LoadScene(gameSceneName);

    public void OnSettingsButton()
        => ShowSettings();

    public void OnQuitButton()
        => Application.Quit();

    // ── Кнопки настроек ───────────────────────────────────────

    public void OnBackButton()
    {
        SettingsManager.Instance?.SaveAll();
        ShowMain();
    }

    // ── Инициализация UI значениями из SettingsManager ────────

    private void InitSettings()
    {
        var s = SettingsManager.Instance;
        if (s == null) return;

        // Чувствительность
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 10f;
            sensitivitySlider.value    = s.Sensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            UpdateSensitivityLabel(s.Sensitivity);
        }

        // Громкость
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value    = s.Volume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            UpdateVolumeLabel(s.Volume);
        }

        // Качество
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.value = s.Quality;
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        // Разрешение
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            var options = new System.Collections.Generic.List<string>();
            int currentIndex = 0;

            for (int i = 0; i < s.Resolutions.Length; i++)
            {
                var r = s.Resolutions[i];
                options.Add($"{r.width} x {r.height} @ {r.refreshRateRatio}Hz");

                if (r.width == s.ResWidth && r.height == s.ResHeight)
                    currentIndex = i;
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentIndex;
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        // Полный экран
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = s.Fullscreen;
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
    }

    // ── Обработчики изменений ─────────────────────────────────

    private void OnSensitivityChanged(float value)
    {
        SettingsManager.Instance?.SetSensitivity(value);
        UpdateSensitivityLabel(value);
    }

    private void OnVolumeChanged(float value)
    {
        SettingsManager.Instance?.SetVolume(value);
        UpdateVolumeLabel(value);
    }

    private void OnQualityChanged(int index)
        => SettingsManager.Instance?.SetQuality(index);

    private void OnResolutionChanged(int index)
        => SettingsManager.Instance?.SetResolutionByIndex(index);

    private void OnFullscreenChanged(bool value)
        => SettingsManager.Instance?.SetFullscreen(value);

    // ── Подписи слайдеров ─────────────────────────────────────

    private void UpdateSensitivityLabel(float value)
    {
        if (sensitivityValue != null)
            sensitivityValue.text = value.ToString("F1");
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValue != null)
            volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}