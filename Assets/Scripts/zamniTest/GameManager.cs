using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Falling, GameOver, Win }

    [Header("Settings")]
    [SerializeField] private float fallDuration = 2f;
    [SerializeField] private float winHeight = 50f;

    public GameState CurrentState { get; private set; }
    public float WinHeight => winHeight;

    public event Action OnGameOver;
    public event Action OnWin;
    public event Action OnRestart;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start() => StartGame();

    public void StartGame()
    {
        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
    }

    public void TriggerGameOver()
    {
        if (CurrentState == GameState.GameOver || CurrentState == GameState.Falling) return;
        CurrentState = GameState.Falling;
        StartCoroutine(FallRoutine());
    }

    private System.Collections.IEnumerator FallRoutine()
    {
        OnGameOver?.Invoke();
        yield return new WaitForSeconds(fallDuration);
        CurrentState = GameState.GameOver;
    }

    public void TriggerWin()
    {
        if (CurrentState == GameState.Win) return;
        CurrentState = GameState.Win;
        OnWin?.Invoke();
        Debug.Log("[GameManager] WIN!");
    }

    public void RestartGame()
    {
        OnRestart?.Invoke();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}