using UnityEngine;
using UnityEngine.Audio;
using System;

/// <summary>
/// Управление громкостью через AudioMixer
/// </summary>
public class AudioSettings : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Mixer Parameters (названия параметров в Mixer)")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string dialogueVolumeParam = "DialogueVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";

    [Header("Default Values (0-1)")]
    [SerializeField] private float defaultMasterVolume = 1f;
    [SerializeField] private float defaultMusicVolume = 0.8f;
    [SerializeField] private float defaultDialogueVolume = 1f;
    [SerializeField] private float defaultSFXVolume = 1f;

    // Текущие значения (0-1)
    private float masterVolume;
    private float musicVolume;
    private float dialogueVolume;
    private float sfxVolume;

    // События
    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnDialogueVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;

    // Ключи для сохранения
    private const string MASTER_KEY = "Audio_Master";
    private const string MUSIC_KEY = "Audio_Music";
    private const string DIALOGUE_KEY = "Audio_Dialogue";
    private const string SFX_KEY = "Audio_SFX";

    // ─────────────────────────────────────────
    //  ПУБЛИЧНЫЕ СВОЙСТВА
    // ─────────────────────────────────────────

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyVolume(masterVolumeParam, masterVolume);
            OnMasterVolumeChanged?.Invoke(masterVolume);
        }
    }

    public float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyVolume(musicVolumeParam, musicVolume);
            OnMusicVolumeChanged?.Invoke(musicVolume);
        }
    }

    public float DialogueVolume
    {
        get => dialogueVolume;
        set
        {
            dialogueVolume = Mathf.Clamp01(value);
            ApplyVolume(dialogueVolumeParam, dialogueVolume);
            OnDialogueVolumeChanged?.Invoke(dialogueVolume);
        }
    }

    public float SFXVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyVolume(sfxVolumeParam, sfxVolume);
            OnSFXVolumeChanged?.Invoke(sfxVolume);
        }
    }

    // ─────────────────────────────────────────
    //  ПРИМЕНЕНИЕ К MIXER
    // ─────────────────────────────────────────

    private void ApplyVolume(string parameter, float normalizedValue)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("AudioMixer не назначен!");
            return;
        }

        // Конвертируем 0-1 в децибелы (-80 до 0)
        float dbValue = normalizedValue > 0.001f
            ? Mathf.Log10(normalizedValue) * 20f
            : -80f;

        audioMixer.SetFloat(parameter, dbValue);
    }

    // ─────────────────────────────────────────
    //  СОХРАНЕНИЕ / ЗАГРУЗКА
    // ─────────────────────────────────────────

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MASTER_KEY, masterVolume);
        PlayerPrefs.SetFloat(MUSIC_KEY, musicVolume);
        PlayerPrefs.SetFloat(DIALOGUE_KEY, dialogueVolume);
        PlayerPrefs.SetFloat(SFX_KEY, sfxVolume);
        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(MASTER_KEY, defaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, defaultMusicVolume);
        DialogueVolume = PlayerPrefs.GetFloat(DIALOGUE_KEY, defaultDialogueVolume);
        SFXVolume = PlayerPrefs.GetFloat(SFX_KEY, defaultSFXVolume);
    }

    public void ResetToDefaults()
    {
        MasterVolume = defaultMasterVolume;
        MusicVolume = defaultMusicVolume;
        DialogueVolume = defaultDialogueVolume;
        SFXVolume = defaultSFXVolume;
    }
}