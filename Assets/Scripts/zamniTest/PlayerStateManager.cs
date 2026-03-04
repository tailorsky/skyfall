using UnityEngine;

/// <summary>
/// Отвечает за хранение и переключение состояний игрока.
/// Другие контроллеры читают CurrentState и сообщают об изменениях через методы.
/// </summary>
public class PlayerStateManager : MonoBehaviour
{
    public enum PlayerState
    {
        Walking,
        Climbing,
        Falling,
        Mantling
    }

    public PlayerState CurrentState { get; private set; } = PlayerState.Walking;

    [Header("Grace Period")]
    [SerializeField] private float noGripGracePeriod = 0.5f;

    private float noGripTimer = 0f;
    private bool climbingStarted = false;

    // ── события ────────────────────────────────
    public event System.Action<PlayerState, PlayerState> OnStateChanged; // (prev, next)

    // ── зависимости ────────────────────────────
    private FallDamageSystem fallDamageSystem;

    private void Awake()
    {
        fallDamageSystem = GetComponent<FallDamageSystem>();
    }

    /// <summary>Вызывается каждый кадр из ClimbingManager после CheckGrounded.</summary>
    public void UpdateState(bool isGrounded, bool isMantling, int grippedHandsCount,
                            float playerY, out float fallStartY)
    {
        fallStartY = 0f;

        if (isMantling)
        {
            SetState(PlayerState.Mantling);
            return;
        }

        if (grippedHandsCount > 0)
        {
            climbingStarted = true;
            noGripTimer = 0f;
            SetState(PlayerState.Climbing);
            return;
        }

        if (isGrounded)
        {
            climbingStarted = false;
            noGripTimer = 0f;
            SetState(PlayerState.Walking);
            return;
        }

        if (climbingStarted && !isGrounded)
        {
            noGripTimer += Time.deltaTime;

            if (noGripTimer >= noGripGracePeriod && CurrentState != PlayerState.Falling)
            {
                fallStartY = playerY;
                fallDamageSystem?.StartClimbingFall(fallStartY);
                SetState(PlayerState.Falling);
            }
            return;
        }

        SetState(PlayerState.Walking);
    }

    public void ForceState(PlayerState state)
    {
        SetState(state);
        if (state == PlayerState.Walking)
        {
            climbingStarted = false;
            noGripTimer = 0f;
        }
    }

    public void NotifyClimbingStarted() => climbingStarted = true;
    public void ResetNoGripTimer() => noGripTimer = 0f;

    private void SetState(PlayerState next)
    {
        if (next == CurrentState) return;
        var prev = CurrentState;
        CurrentState = next;
        OnStateChanged?.Invoke(prev, next);
    }
}