using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI для меню настроек
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject gameplayPanel;

    [Header("Tab Buttons")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private Button gameplayTabButton;

    [Header("Audio Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider dialogueVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private TextMeshProUGUI dialogueVolumeText;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;

    [Header("Gameplay Controls")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Toggle invertYToggle;
    [SerializeField] private TextMeshProUGUI sensitivityText;
    [SerializeField] private TextMeshProUGUI fovText;

    [Header("Key Bindings")]
    [SerializeField] private Transform keyBindingsContainer;
    [SerializeField] private GameObject keyBindingPrefab;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;

    [Header("Rebind UI")]
    [SerializeField] private GameObject rebindOverlay;
    [SerializeField] private TextMeshProUGUI rebindText;

    private List<KeyBindingUI> keyBindingUIs = new List<KeyBindingUI>();
    private bool isOpen = false;

    private void Start()
    {
        SetupTabButtons();
        SetupAudioControls();
        SetupGameplayControls();
        SetupButtons();
        SetupKeyBindings();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (rebindOverlay != null)
            rebindOverlay.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  ОТКРЫТИЕ / ЗАКРЫТИЕ
    // ─────────────────────────────────────────

    public void Open()
    {
        isOpen = true;
        settingsPanel?.SetActive(true);
        ShowTab(0);
        RefreshAllValues();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
    }

    public void Close()
    {
        isOpen = false;
        settingsPanel?.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Time.timeScale = 1f;
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public bool IsOpen => isOpen;

    // ─────────────────────────────────────────
    //  ВКЛАДКИ
    // ─────────────────────────────────────────

    private void SetupTabButtons()
    {
        audioTabButton?.onClick.AddListener(() => ShowTab(0));
        controlsTabButton?.onClick.AddListener(() => ShowTab(1));
        gameplayTabButton?.onClick.AddListener(() => ShowTab(2));
    }

    private void ShowTab(int index)
    {
        audioPanel?.SetActive(index == 0);
        controlsPanel?.SetActive(index == 1);
        gameplayPanel?.SetActive(index == 2);

        SetTabButtonState(audioTabButton, index == 0);
        SetTabButtonState(controlsTabButton, index == 1);
        SetTabButtonState(gameplayTabButton, index == 2);
    }

    private void SetTabButtonState(Button button, bool active)
    {
        if (button == null) return;

        var colors = button.colors;
        colors.normalColor = active ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        button.colors = colors;
    }

    // ─────────────────────────────────────────
    //  АУДИО
    // ─────────────────────────────────────────

    private void SetupAudioControls()
    {
        var audio = SettingsManager.Instance?.Audio;
        if (audio == null) return;

        masterVolumeSlider?.onValueChanged.AddListener(value =>
        {
            audio.MasterVolume = value;
            UpdateVolumeText(masterVolumeText, value);
        });

        musicVolumeSlider?.onValueChanged.AddListener(value =>
        {
            audio.MusicVolume = value;
            UpdateVolumeText(musicVolumeText, value);
        });

        dialogueVolumeSlider?.onValueChanged.AddListener(value =>
        {
            audio.DialogueVolume = value;
            UpdateVolumeText(dialogueVolumeText, value);
        });

        sfxVolumeSlider?.onValueChanged.AddListener(value =>
        {
            audio.SFXVolume = value;
            UpdateVolumeText(sfxVolumeText, value);
        });
    }

    private void UpdateVolumeText(TextMeshProUGUI text, float value)
    {
        if (text != null)
            text.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    // ─────────────────────────────────────────
    //  ГЕЙМПЛЕЙ
    // ─────────────────────────────────────────

    private void SetupGameplayControls()
    {
        var gameplay = SettingsManager.Instance?.Gameplay;
        if (gameplay == null) return;

        sensitivitySlider?.onValueChanged.AddListener(value =>
        {
            gameplay.MouseSensitivity = value;
            if (sensitivityText != null)
                sensitivityText.text = value.ToString("F1");
        });

        fovSlider?.onValueChanged.AddListener(value =>
        {
            gameplay.FieldOfView = value;
            if (fovText != null)
                fovText.text = $"{Mathf.RoundToInt(value)}°";
        });

        invertYToggle?.onValueChanged.AddListener(value =>
        {
            gameplay.InvertMouseY = value;
        });
    }

    // ─────────────────────────────────────────
    //  КЛАВИШИ
    // ─────────────────────────────────────────

    private void SetupKeyBindings()
    {
        var keys = SettingsManager.Instance?.Keys;
        if (keys == null || keyBindingsContainer == null || keyBindingPrefab == null) return;

        foreach (Transform child in keyBindingsContainer)
        {
            Destroy(child.gameObject);
        }
        keyBindingUIs.Clear();

        foreach (var binding in keys.GetAllBindings())
        {
            var go = Instantiate(keyBindingPrefab, keyBindingsContainer);
            var ui = go.GetComponent<KeyBindingUI>();

            if (ui != null)
            {
                ui.Setup(binding, this);
                keyBindingUIs.Add(ui);
            }
        }
    }

    public void StartRebind(string actionId, Button button)
    {
        var keys = SettingsManager.Instance?.Keys;
        if (keys == null) return;

        if (rebindOverlay != null)
        {
            rebindOverlay.SetActive(true);
            if (rebindText != null)
            {
                var binding = keys.GetBinding(actionId);
                rebindText.text = $"Нажмите клавишу для\n\"{binding?.actionName}\"";
            }
        }

        keys.StartRebinding(actionId, true, newKey =>
        {
            rebindOverlay?.SetActive(false);

            if (button != null)
            {
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                    text.text = keys.GetKeyDisplayName(actionId);
            }
        });
    }

    // ─────────────────────────────────────────
    //  КНОПКИ
    // ─────────────────────────────────────────

    private void SetupButtons()
    {
        applyButton?.onClick.AddListener(() =>
        {
            SettingsManager.Instance?.SaveAllSettings();
            Close();
        });

        resetButton?.onClick.AddListener(() =>
        {
            SettingsManager.Instance?.ResetToDefaults();
            RefreshAllValues();
        });

        backButton?.onClick.AddListener(Close);
    }

    // ─────────────────────────────────────────
    //  ОБНОВЛЕНИЕ UI
    // ─────────────────────────────────────────

    private void RefreshAllValues()
    {
        var audio = SettingsManager.Instance?.Audio;
        var gameplay = SettingsManager.Instance?.Gameplay;

        if (audio != null)
        {
            if (masterVolumeSlider != null) masterVolumeSlider.value = audio.MasterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = audio.MusicVolume;
            if (dialogueVolumeSlider != null) dialogueVolumeSlider.value = audio.DialogueVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = audio.SFXVolume;
        }

        if (gameplay != null)
        {
            if (sensitivitySlider != null) sensitivitySlider.value = gameplay.MouseSensitivity;
            if (fovSlider != null) fovSlider.value = gameplay.FieldOfView;
            if (invertYToggle != null) invertYToggle.isOn = gameplay.InvertMouseY;
        }

        foreach (var ui in keyBindingUIs)
        {
            ui.Refresh();
        }
    }

    private void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            var keys = SettingsManager.Instance?.Keys;
            if (keys != null && keys.IsRebinding)
            {
                keys.CancelRebinding();
                rebindOverlay?.SetActive(false);
            }
            else
            {
                Close();
            }
        }
    }
}