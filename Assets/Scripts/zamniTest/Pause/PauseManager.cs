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
    [SerializeField] private Animator   menuAnimator;
    [SerializeField] private Animator   logoAnimator;

    [Header("Animation Triggers")]
    [SerializeField] private string openTrigger  = "Open";
    [SerializeField] private string closeTrigger = "Close";

    public bool IsPaused { get; private set; } = false;

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
            TogglePause();
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

    public void OnResumeButton()  => Resume();
    public void OnRestartButton() => GameManager.Instance?.RestartGame();
    public void OnQuitButton()    => Application.Quit();
}