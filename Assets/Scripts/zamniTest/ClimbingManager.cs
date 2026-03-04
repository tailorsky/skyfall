﻿using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Тонкий оркестратор. Делегирует всю логику специализированным контроллерам.
/// Сам не содержит игровой логики — только маршрутизацию между компонентами.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(StaminaSystem))]
[RequireComponent(typeof(FallDamageSystem))]
[RequireComponent(typeof(PlayerStateManager))]
[RequireComponent(typeof(PlayerLook))]
[RequireComponent(typeof(PlayerWalking))]
[RequireComponent(typeof(ClimbingController))]
[RequireComponent(typeof(MantleController))]
[RequireComponent(typeof(FallController))]
public class ClimbingManager : MonoBehaviour
{
    // ── Все зависимости ───────────────────────────────────────
    private PlayerStateManager stateManager;
    private PlayerLook         playerLook;
    private PlayerWalking      playerWalking;
    private ClimbingController climbing;
    private MantleController   mantle;
    private FallController     fall;
    private FallDamageSystem   fallDamageSystem;

    // ── Рабочее состояние ────────────────────────────────────
    private float climbingFallStartY = 0f;

    // ── Публичное API (обратная совместимость) ────────────────
    public PlayerStateManager.PlayerState CurrentState => stateManager.CurrentState;
    public bool IsFalling  => stateManager.CurrentState == PlayerStateManager.PlayerState.Falling;
    public bool IsPullingUp => climbing.IsPullingUp;
    public bool IsGrounded  => playerWalking.IsGrounded;
    public bool IsWalking   => stateManager.CurrentState == PlayerStateManager.PlayerState.Walking;
    public bool CanMantle   => mantle.CanMantle;
    public bool IsMantling  => mantle.IsMantling;
    public int  GetGrippedHandsCount() => climbing.GrippedHandsCount;

    public void SetMouseSensitivity(float v) => playerLook.SetSensitivity(v);
    public void SetInvertY(bool v)           => playerLook.SetInvertY(v);
    public void SetCursorLocked(bool v)      => playerLook.SetCursorLocked(v);
    public void Jump()                       => playerWalking.TryJump();

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        stateManager     = GetComponent<PlayerStateManager>();
        playerLook       = GetComponent<PlayerLook>();
        playerWalking    = GetComponent<PlayerWalking>();
        climbing         = GetComponent<ClimbingController>();
        mantle           = GetComponent<MantleController>();
        fall             = GetComponent<FallController>();
        fallDamageSystem = GetComponent<FallDamageSystem>();
    }

    private void OnEnable()
    {
        mantle.OnMantleStarted  += OnMantleStarted;
        mantle.OnMantleFinished += OnMantleFinished;
    }

    private void OnDisable()
    {
        mantle.OnMantleStarted  -= OnMantleStarted;
        mantle.OnMantleFinished -= OnMantleFinished;
    }

    // ── Главный Update ────────────────────────────────────────
    private void Update()
    {
        if (!ShouldProcess()) return;

        if (fallDamageSystem != null && !fallDamageSystem.CanMove())
        {
            playerWalking.ApplyGravityOnly();
            return;
        }

        // Мантл выполняется поверх любого состояния
        if (mantle.IsMantling)
        {
            mantle.Tick();
            return;
        }

        stateManager.UpdateState(
            playerWalking.IsGrounded,
            mantle.IsMantling,
            climbing.GrippedHandsCount,
            transform.position.y,
            out climbingFallStartY
        );

        switch (stateManager.CurrentState)
        {
            case PlayerStateManager.PlayerState.Walking:
                playerLook.Tick();
                climbing.TryGripFromWalking();
                playerWalking.Tick();
                fall.UpdateFOVNormal();
                break;

            case PlayerStateManager.PlayerState.Climbing:
                playerLook.Tick();
                climbing.Tick(playerWalking.IsGrounded);
                mantle.CheckPossibility(climbing.GetHighestGripPoint(), climbing.GetClimbingForward());
                mantle.HandleInput();
                fall.UpdateFOVNormal();
                break;

            case PlayerStateManager.PlayerState.Falling:
                playerLook.Tick();

                if (!IsGameOver())
                {
                    fall.Tick(playerWalking.Gravity, playerWalking.IsGrounded);

                    if (fall.IsGroundedAfterFall)
                    {
                        stateManager.ForceState(PlayerStateManager.PlayerState.Walking);
                        playerWalking.ResetVelocity();
                    }
                }
                break;
        }
    }

    // ── Игровые события ───────────────────────────────────────
    private void OnMantleStarted()
    {
        climbing.ReleaseAll();
        playerWalking.ResetVelocity();
        stateManager.ForceState(PlayerStateManager.PlayerState.Mantling);
    }

    private void OnMantleFinished()
    {
        stateManager.ForceState(PlayerStateManager.PlayerState.Walking);
        playerWalking.ResetVelocity();
    }

    // ── Вспомогательные ──────────────────────────────────────
    private bool ShouldProcess()
    {
        if (GameManager.Instance == null) return false;

        var gs = GameManager.Instance.CurrentState;

        if (gs == GameManager.GameState.Win || gs == GameManager.GameState.GameOver)
            return false;

        // В состоянии Falling в GameManager — только камера
        if (gs == GameManager.GameState.Falling)
        {
            playerLook.Tick();
            return false;
        }

        return gs == GameManager.GameState.Playing;
    }

    private bool IsGameOver()
    {
        if (GameManager.Instance == null) return false;
        var gs = GameManager.Instance.CurrentState;
        return gs == GameManager.GameState.Win || gs == GameManager.GameState.GameOver;
    }
}