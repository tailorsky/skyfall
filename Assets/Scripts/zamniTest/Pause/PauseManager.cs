using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Управляет паузой. Показывает/скрывает UI через аниматор.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject settingsMenuRoot;
    [SerializeField] private Animator   menuAnimator;
    [SerializeField] private Animator   logoAnimator;
    [SerializeField] private Animator   settingsAnimator;

    [Header("Animation Triggers")]
    [SerializeField] private string openTrigger          = "Open";
    [SerializeField] private string closeTrigger         = "Close";
    [SerializeField] private string settingsOpenTrigger  = "Open";
    [SerializeField] private string settingsCloseTrigger = "Close";

    public bool IsPaused         { get; private set; } = false;
    public bool IsSettingsOpen   { get; private set; } = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        var state = GameManager.Instance.CurrentState;
        if (state == GameManager.GameState.GameOver || state == GameManager.GameState.Win) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsSettingsOpen) CloseSettings();
            else                TogglePause();
        }
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else          Pause();
    }

    public void Pause()
    {
        IsPaused       = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);

        menuAnimator?.SetTrigger(openTrigger);
        logoAnimator?.SetTrigger(openTrigger);
    }

    public void Resume()
    {
        if (IsSettingsOpen) CloseSettings();

        menuAnimator?.SetTrigger(closeTrigger);
        logoAnimator?.SetTrigger(closeTrigger);
        StartCoroutine(ResumeAfterAnimation());
    }

    private System.Collections.IEnumerator ResumeAfterAnimation()
    {
        yield return new WaitForSecondsRealtime(0.4f);

        IsPaused       = false;
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
    }

    // ─── Settings ───────────────────────────────────────────────

    public void OpenSettings()
    {
        if (IsSettingsOpen) return;
        IsSettingsOpen = true;

        if (settingsMenuRoot != null) settingsMenuRoot.SetActive(true);
        settingsAnimator?.SetTrigger(settingsOpenTrigger);

        // Скрываем основное меню паузы, пока открыты настройки
        menuAnimator?.SetTrigger(closeTrigger);
        logoAnimator?.SetTrigger(closeTrigger);
    }

    public void CloseSettings()
    {
        if (!IsSettingsOpen) return;
        IsSettingsOpen = false;

        settingsAnimator?.SetTrigger(settingsCloseTrigger);
        StartCoroutine(CloseSettingsAfterAnimation());
    }

    private System.Collections.IEnumerator CloseSettingsAfterAnimation()
    {
        yield return new WaitForSecondsRealtime(0.4f);

        if (settingsMenuRoot != null) settingsMenuRoot.SetActive(false);

        // Возвращаем основное меню паузы
        if (pauseMenuRoot != null) pauseMenuRoot.SetActive(true);
        menuAnimator?.SetTrigger(openTrigger);
        logoAnimator?.SetTrigger(openTrigger);
    }

    // ─── Button callbacks ────────────────────────────────────────

    public void OnResumeButton()   => Resume();
    public void OnSettingsButton() => OpenSettings();
    public void OnBackButton()     => CloseSettings();
    public void OnRestartButton()  => GameManager.Instance?.RestartGame();
    public void OnQuitButton()     => Application.Quit();
}