using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(StaminaSystem))]
[RequireComponent(typeof(CharacterController))]
public class ClimbingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandController leftHand;
    [SerializeField] private HandController rightHand;

    [Header("Climbing Settings")]
    [SerializeField] private float pullUpSpeed = 4f;        // Скорость подтягивания
    [SerializeField] private float pullUpOffset = 0.6f;     // На сколько ниже руки висит тело
    [SerializeField] private float lateralSpeed = 2f;       // Скорость бокового движения
    [SerializeField] private float fallAcceleration = 9.8f; // Ускорение падения

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Компоненты
    private StaminaSystem staminaSystem;
    private CharacterController characterController;

    // Состояние лазания
    private int grippedHandsCount = 0;
    private bool isFalling = false;
    private float currentFallVelocity = 0f;
    private bool gameStarted = false;
    private float noGripTimer = 0f;
    private float noGripGracePeriod = 0.5f;
    private float oneHandTimer = 0f;

    // Состояние подтягивания
    private bool isPullingUp = false;       // Сейчас идёт анимация подтягивания
    private Vector3 pullTargetPosition;     // Куда подтягиваемся

    // Запоминаем какая рука была зацеплена последней
    private HandController lastGrippedHand = null;
    private HandController anchorHand = null; // Рука которая держит пока другая ищет

    private Vector3 startPosition;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        staminaSystem = GetComponent<StaminaSystem>();
        startPosition = transform.position;
    }

    private void OnEnable()
    {
        if (leftHand != null)
        {
            leftHand.OnGrip += HandleHandGrip;
            leftHand.OnRelease += HandleHandRelease;
        }

        if (rightHand != null)
        {
            rightHand.OnGrip += HandleHandGrip;
            rightHand.OnRelease += HandleHandRelease;
        }

        if (staminaSystem != null)
            staminaSystem.OnStaminaExhausted += HandleStaminaExhausted;
    }

    private void OnDisable()
    {
        if (leftHand != null)
        {
            leftHand.OnGrip -= HandleHandGrip;
            leftHand.OnRelease -= HandleHandRelease;
        }

        if (rightHand != null)
        {
            rightHand.OnGrip -= HandleHandGrip;
            rightHand.OnRelease -= HandleHandRelease;
        }

        if (staminaSystem != null)
            staminaSystem.OnStaminaExhausted -= HandleStaminaExhausted;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        if (GameManager.Instance.CurrentState == GameManager.GameState.Falling)
        {
            HandleFall();
            return;
        }

        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        HandleInput();
        UpdateClimbingState();

        // Если идёт подтягивание — двигаемся к цели
        if (isPullingUp)
        {
            HandlePullUp();
        }

        CheckWinCondition();
    }

    // ─────────────────────────────────────────
    //  ВВОД
    // ─────────────────────────────────────────

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null || mouse == null) return;

        // ЛЕВАЯ РУКА — ЛКМ или Q
        if (mouse.leftButton.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
        {
            if (leftHand.IsGripped)
            {
                // Отпускаем только если вторая рука держится
                // (нельзя отпустить последнюю руку вручную — только падение)
                if (rightHand.IsGripped)
                    leftHand.Release();
                else
                    Debug.Log("Нельзя отпустить — вторая рука не держится!");
            }
            else
            {
                leftHand.TryGrip();
            }
        }

        // ПРАВАЯ РУКА — ПКМ или E
        if (mouse.rightButton.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
        {
            if (rightHand.IsGripped)
            {
                if (leftHand.IsGripped)
                    rightHand.Release();
                else
                    Debug.Log("Нельзя отпустить — вторая рука не держится!");
            }
            else
            {
                rightHand.TryGrip();
            }
        }

        // Боковое движение — только если обе руки держатся и нет подтягивания
        if (grippedHandsCount == 2 && !isPullingUp)
        {
            HandleLateralMovement(keyboard);
        }
    }

    private void HandleLateralMovement(Keyboard keyboard)
    {
        Vector3 moveDirection = Vector3.zero;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            Vector3 left = Camera.main.transform.right * -1f;
            left.y = 0;
            left.Normalize();
            moveDirection += left * lateralSpeed;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            Vector3 right = Camera.main.transform.right;
            right.y = 0;
            right.Normalize();
            moveDirection += right * lateralSpeed;
        }

        if (moveDirection != Vector3.zero)
            characterController.Move(moveDirection * Time.deltaTime);
    }

    // ─────────────────────────────────────────
    //  ПОДТЯГИВАНИЕ
    // ─────────────────────────────────────────

    private void HandleHandGrip(HandController hand)
    {
        // Считаем руки
        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        Debug.Log($"Захват {hand.Side}! Рук держится: {grippedHandsCount}");

        // Запускаем подтягивание к точке захвата
        StartPullUp(hand);

        lastGrippedHand = hand;
    }

    private void HandleHandRelease(HandController hand)
    {
        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        Debug.Log($"Отпустил {hand.Side}! Рук держится: {grippedHandsCount}");

        // Запоминаем какая рука сейчас держит (якорь)
        if (grippedHandsCount == 1)
        {
            anchorHand = leftHand.IsGripped ? leftHand : rightHand;
        }

        // Останавливаем подтягивание если отпустили
        isPullingUp = false;
    }

    private void StartPullUp(HandController grippedHand)
    {
        // Получаем точку куда зацепилась рука
        Vector3 gripPoint = grippedHand.GetGripPoint();

        // Целевая позиция игрока:
        // Тело висит НИЖЕ точки захвата на pullUpOffset
        Vector3 targetPos = new Vector3(
            transform.position.x,           // X не меняем (боковое движение отдельно)
            gripPoint.y - pullUpOffset,      // Y = точка захвата минус смещение тела
            transform.position.z             // Z не меняем
        );

        // Подтягиваемся только если точка ВЫШЕ текущей позиции
        if (targetPos.y > transform.position.y + 0.05f)
        {
            pullTargetPosition = targetPos;
            isPullingUp = true;
            Debug.Log($"Подтягиваемся! С Y={transform.position.y:F2} до Y={targetPos.y:F2}");
        }
        else if (targetPos.y < transform.position.y - 0.05f)
        {
            // Рука зацепилась НИЖЕ — слегка опускаемся
            pullTargetPosition = targetPos;
            isPullingUp = true;
            Debug.Log($"Опускаемся! С Y={transform.position.y:F2} до Y={targetPos.y:F2}");
        }
    }

    private void HandlePullUp()
    {
        // Двигаемся к целевой позиции плавно
        float step = pullUpSpeed * Time.deltaTime;
        Vector3 newPos = Vector3.MoveTowards(
            transform.position,
            pullTargetPosition,
            step
        );

        // Вычисляем дельту и двигаем через CharacterController
        Vector3 delta = newPos - transform.position;
        characterController.Move(delta);

        // Проверяем достигли ли цели
        float distanceLeft = Vector3.Distance(
            new Vector3(0, transform.position.y, 0),
            new Vector3(0, pullTargetPosition.y, 0)
        );

        if (distanceLeft < 0.05f)
        {
            isPullingUp = false;
            Debug.Log($"Подтянулись! Текущая Y={transform.position.y:F2}");
        }
    }

    // ─────────────────────────────────────────
    //  СОСТОЯНИЕ И ПАДЕНИЕ
    // ─────────────────────────────────────────

    private void UpdateClimbingState()
    {
        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        staminaSystem.SetHandsGripped(grippedHandsCount);

        if (grippedHandsCount == 1)
            oneHandTimer += Time.deltaTime;
        else
            oneHandTimer = 0f;

        if (grippedHandsCount > 0)
        {
            gameStarted = true;
            noGripTimer = 0f;
        }

        // Падение только если уже начали игру и обе руки отпущены
        if (gameStarted && grippedHandsCount == 0 && !isFalling)
        {
            noGripTimer += Time.deltaTime;

            if (noGripTimer >= noGripGracePeriod)
            {
                Debug.Log("Обе руки отпущены — падение!");
                isFalling = true;
                isPullingUp = false;
                GameManager.Instance.TriggerGameOver();
            }
        }
    }

    private void HandleFall()
    {
        currentFallVelocity += fallAcceleration * Time.deltaTime;
        characterController.Move(Vector3.down * currentFallVelocity * Time.deltaTime);

        if (transform.position.y < startPosition.y - 10f)
        {
            GameManager.Instance.RestartGame();
        }
    }

    private void HandleStaminaExhausted()
    {
        StartCoroutine(ForcedRelease());
    }

    private IEnumerator ForcedRelease()
    {
        Debug.Log("Стамина кончилась! Руки срываются!");
        leftHand?.Release();
        yield return new WaitForSeconds(0.3f);
        rightHand?.Release();
    }

    private void CheckWinCondition()
    {
        if (GameManager.Instance != null &&
            transform.position.y >= GameManager.Instance.WinHeight)
        {
            GameManager.Instance.TriggerWin();
        }
    }

    public int GetGrippedHandsCount() => grippedHandsCount;
    public bool IsFalling => isFalling;
    public bool IsPullingUp => isPullingUp;
}