using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

[RequireComponent(typeof(StaminaSystem))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(FallDamageSystem))]
public class ClimbingManager : MonoBehaviour
{
    [Header("Reach Settings")]
    [SerializeField] private float maxReachDistance = 1.5f;

    private Vector3 anchorGripPoint = Vector3.zero;
    private bool hasAnchorPoint = false;

    [Header("References")]
    [SerializeField] private HandController leftHand;
    [SerializeField] private HandController rightHand;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraHolder;

    [Header("Climbing Settings")]
    [SerializeField] private float pullUpSpeed = 4f;
    [SerializeField] private float pullUpOffset = 0.8f;
    [SerializeField] private float lateralSpeed = 2f;

    [Header("Walking Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float jumpCooldown = 0.2f;
    private float lastJumpTime = -10f;

    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookUpAngle = 80f;
    [SerializeField] private float maxLookDownAngle = 80f;
    [SerializeField] private bool invertMouseY = false;
    [SerializeField] private bool lockCursor = true;

    [Header("Fall Camera Shake")]
    [SerializeField] private float shakeFrequency = 25f;   // как быстро трясётся
    [SerializeField] private float shakeAmplitude = 0.08f; // насколько сильно
    [SerializeField] private float shakeRampUpSpeed = 2f;  // как быстро нарастает

    private float currentShakeIntensity = 0f;  // текущая интенсивность тряски
    private float fallTime = 0f;               // сколько времени падаем
    private Vector3 cameraOriginalLocalPos;    // исходная позиция камеры
    private bool isCameraShaking = false;

    private float rotationX = 0f;
    private float rotationY = 0f;

    [Header("Mantle Settings (Перелезание)")]
    [SerializeField] private float mantleCheckDistance = 1.0f;
    [SerializeField] private float mantleCheckHeight = 1.5f;
    [SerializeField] private float mantleSpeed = 3f;
    [SerializeField] private float mantleForwardOffset = 0.8f;
    [SerializeField] private float mantleUpOffset = 0.5f;
    [SerializeField] private LayerMask mantleSurfaceLayer;

    private bool canMantle = false;
    private bool isMantling = false;
    private Vector3 mantleTargetPosition;
    private Vector3 mantleEdgePosition;
    private int mantlePhase = 0;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Climbing Fall Settings")]
    [SerializeField] private float noGripGracePeriod = 0.5f;  // Время до начала падения

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool showMantleDebug = true;

    // Компоненты
    private StaminaSystem staminaSystem;
    private CharacterController characterController;
    private FallDamageSystem fallDamageSystem;

    // Состояние
    public enum PlayerState
    {
        Walking,
        Climbing,
        Falling,
        Mantling
    }

    public PlayerState CurrentState { get; private set; } = PlayerState.Walking;

    // Ходьба
    private Vector3 walkVelocity;
    private bool isGrounded;

    // Лазание
    private int grippedHandsCount = 0;
    private bool isPullingUp = false;
    private Vector3 pullTargetPosition;
    private float noGripTimer = 0f;
    private float oneHandTimer = 0f;
    private bool climbingStarted = false;
    private float climbingFallStartY = 0f;  // Высота начала падения при лазании

    // ─────────────────────────────────────────
    //  ИНИЦИАЛИЗАЦИЯ
    // ─────────────────────────────────────────

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        staminaSystem = GetComponent<StaminaSystem>();
        fallDamageSystem = GetComponent<FallDamageSystem>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogError("Camera.main не найдена!");
            }
        }

        if (mantleSurfaceLayer == 0)
        {
            mantleSurfaceLayer = groundLayer;
        }
    }

    private void Start()
    {
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.parent = transform;
            gc.transform.localPosition = new Vector3(0, -0.9f, 0);
            groundCheck = gc.transform;
        }

        SetupCamera();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        rotationY = transform.eulerAngles.y;
        rotationX = 0f;
    }

    private void SetupCamera()
    {
        if (playerCamera == null) return;

        if (cameraHolder == null)
        {
            if (playerCamera.transform.parent == transform)
            {
                cameraHolder = playerCamera.transform;
            }
            else
            {
                GameObject holder = new GameObject("CameraHolder");
                holder.transform.SetParent(transform);
                holder.transform.localPosition = new Vector3(0, 1.6f, 0);
                holder.transform.localRotation = Quaternion.identity;
                cameraHolder = holder.transform;

                playerCamera.transform.SetParent(cameraHolder);
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }
        }

        playerCamera.transform.localRotation = Quaternion.identity;
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ─────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // Игра окончена — ничего не делаем
        if (GameManager.Instance.CurrentState == GameManager.GameState.Win ||
            GameManager.Instance.CurrentState == GameManager.GameState.GameOver)
        {
            return;
        }

        // При падении — только камера и тряска, движение не обрабатываем
        if (GameManager.Instance.CurrentState == GameManager.GameState.Falling)
        {
            HandleMouseLook();
            return;
        }

        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        // Проверяем оглушение от падения
        if (fallDamageSystem != null && !fallDamageSystem.CanMove())
        {
            // Оглушены — только гравитация
            ApplyGravityOnly();
            return;
        }

        // Перелезание
        if (isMantling)
        {
            HandleMantling();
            return;
        }

        CheckGrounded();
        UpdatePlayerState();

        switch (CurrentState)
        {
            case PlayerState.Walking:
                HandleMouseLook();
                HandleWalkingInput();
                HandleWalking();
                HandleJump();
                break;

            case PlayerState.Climbing:
                HandleMouseLook();
                HandleClimbingInput();
                CheckMantlePossibility();
                HandleMantleInput();
                if (isPullingUp) HandlePullUp();
                UpdateClimbingState();
                break;

            case PlayerState.Falling:
                HandleMouseLook(); // камера работает при падении
                HandleClimbingFall();
                break;
        }

        if (showDebugInfo)
        {
            Debug.Log($"Состояние: {CurrentState} | " +
                      $"Mantle: {canMantle} | " +
                      $"Рук: {grippedHandsCount}");
        }
    }

    private void ApplyGravityOnly()
    {
        if (isGrounded && walkVelocity.y < 0f)
        {
            walkVelocity.y = -2f;
        }
        else
        {
            walkVelocity.y += gravity * Time.deltaTime;
        }

        walkVelocity.x = 0f;
        walkVelocity.z = 0f;

        characterController.Move(walkVelocity * Time.deltaTime);
        CheckGrounded();
    }

    // ─────────────────────────────────────────
    //  ПАДЕНИЕ ПРИ ЛАЗАНИИ
    // ─────────────────────────────────────────

    private void HandleClimbingFall()
    {
        // Нарастающая тряска камеры
        UpdateFallFOV(true);
        fallTime += Time.deltaTime;
        ApplyFallCameraShake(fallTime);

        // Применяем гравитацию
        walkVelocity.y += gravity * Time.deltaTime;
        walkVelocity.x = 0f;
        walkVelocity.z = 0f;

        characterController.Move(walkVelocity * Time.deltaTime);
        CheckGrounded();

        if (isGrounded)
        {
            float fallDistance = climbingFallStartY - transform.position.y;
            Debug.Log($"Приземлились! Падение: {fallDistance:F2}м");

            // Останавливаем тряску
            StopCameraShake();

            if (fallDamageSystem != null)
                fallDamageSystem.ProcessClimbingLanding(climbingFallStartY, transform.position.y);

            CurrentState = PlayerState.Walking;
            climbingStarted = false;
            walkVelocity = Vector3.zero;
            noGripTimer = 0f;
            fallTime = 0f;
        }
    }

    private void ApplyFallCameraShake(float timeInFall)
    {
        if (playerCamera == null) return;

        // Запоминаем исходную позицию один раз
        if (!isCameraShaking)
        {
            cameraOriginalLocalPos = playerCamera.transform.localPosition;
            isCameraShaking = true;
        }

        // Интенсивность нарастает чем дольше падаем
        currentShakeIntensity = Mathf.Min(
            timeInFall * shakeRampUpSpeed * shakeAmplitude,
            shakeAmplitude
        );

        // Генерируем тряску через Perlin Noise — плавнее чем Random
        float shakeX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f)
                       * 2f * currentShakeIntensity;
        float shakeY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f)
                       * 2f * currentShakeIntensity;

        // Применяем смещение к локальной позиции камеры
        playerCamera.transform.localPosition = cameraOriginalLocalPos + new Vector3(shakeX, shakeY, 0f);
    }

    private void StopCameraShake()
    {
        if (playerCamera == null) return;

        isCameraShaking = false;
        currentShakeIntensity = 0f;

        // Возвращаем камеру на место
        playerCamera.transform.localPosition = cameraOriginalLocalPos;
    }


    // ─────────────────────────────────────────
    //  ОПРЕДЕЛЕНИЕ СОСТОЯНИЯ
    // ─────────────────────────────────────────

    private void CheckGrounded()
    {
        if (groundLayer != 0)
        {
            isGrounded = Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }

        if (!isGrounded)
        {
            isGrounded = characterController.isGrounded;
        }
    }

    private void UpdatePlayerState()
    {
        if (isMantling)
        {
            CurrentState = PlayerState.Mantling;
            return;
        }

        if (grippedHandsCount > 0)
        {
            CurrentState = PlayerState.Climbing;
            return;
        }

        if (isGrounded)
        {
            CurrentState = PlayerState.Walking;
            climbingStarted = false;
            noGripTimer = 0f;
            return;
        }

        // Если начали лазать и отпустили руки в воздухе
        if (climbingStarted && !isGrounded)
        {
            noGripTimer += Time.deltaTime;

            if (noGripTimer >= noGripGracePeriod && CurrentState != PlayerState.Falling)
            {
                // Запоминаем высоту ОДИН РАЗ в момент перехода в падение
                climbingFallStartY = transform.position.y;
                CurrentState = PlayerState.Falling;

                if (fallDamageSystem != null)
                    fallDamageSystem.StartClimbingFall(climbingFallStartY);

                Debug.Log($"Переход в падение! Высота зафиксирована: {climbingFallStartY:F2}");
            }
            return;
        }

        CurrentState = PlayerState.Walking;
    }

    // ─────────────────────────────────────────
    //  ПЕРЕЛЕЗАНИЕ (MANTLE)
    // ─────────────────────────────────────────

    private void CheckMantlePossibility()
    {
        canMantle = false;

        if (grippedHandsCount == 0) return;

        Vector3 gripPoint = GetHighestGripPoint();
        Vector3 forwardDir = GetClimbingForward();

        Vector3 checkStart = gripPoint + Vector3.up * mantleCheckHeight + forwardDir * mantleCheckDistance;

        RaycastHit hitDown;
        bool hasTopSurface = Physics.Raycast(
            checkStart,
            Vector3.down,
            out hitDown,
            mantleCheckHeight + 1f,
            mantleSurfaceLayer
        );

        if (showMantleDebug)
        {
            Debug.DrawRay(checkStart, Vector3.down * (mantleCheckHeight + 1f),
                hasTopSurface ? Color.green : Color.red);
        }

        if (!hasTopSurface) return;

        if (hitDown.normal.y < 0.7f) return;

        Vector3 targetPos = hitDown.point + Vector3.up * mantleUpOffset;

        float playerRadius = characterController.radius;
        float playerHeight = characterController.height;

        bool hasSpace = !Physics.CheckCapsule(
            targetPos + Vector3.up * playerRadius,
            targetPos + Vector3.up * (playerHeight - playerRadius),
            playerRadius * 0.9f,
            mantleSurfaceLayer
        );

        if (!hasSpace) return;

        canMantle = true;
        mantleEdgePosition = gripPoint + Vector3.up * 0.3f;
        mantleTargetPosition = targetPos;

        if (showMantleDebug)
        {
            Debug.DrawLine(transform.position, mantleEdgePosition, Color.yellow);
            Debug.DrawLine(mantleEdgePosition, mantleTargetPosition, Color.cyan);
        }
    }

    private void HandleMantleInput()
    {
        if (!canMantle) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool mantlePressed = keyboard.spaceKey.wasPressedThisFrame ||
                            keyboard.wKey.wasPressedThisFrame;

        if (mantlePressed)
        {
            StartMantle();
        }
    }

    private void StartMantle()
    {
        if (!canMantle || isMantling) return;

        Debug.Log("Начинаем перелезание!");

        isMantling = true;
        mantlePhase = 0;
        CurrentState = PlayerState.Mantling;

        leftHand?.Release();
        rightHand?.Release();

        walkVelocity = Vector3.zero;
    }

    private void HandleMantling()
    {
        float step = mantleSpeed * Time.deltaTime;

        switch (mantlePhase)
        {
            case 0:
                Vector3 edgeTarget = new Vector3(transform.position.x, mantleEdgePosition.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, edgeTarget, step);

                if (Vector3.Distance(transform.position, edgeTarget) < 0.05f)
                {
                    mantlePhase = 1;
                }
                break;

            case 1:
                transform.position = Vector3.MoveTowards(transform.position, mantleTargetPosition, step);

                if (Vector3.Distance(transform.position, mantleTargetPosition) < 0.05f)
                {
                    FinishMantle();
                }
                break;
        }
    }

    private void FinishMantle()
    {
        Debug.Log("Перелезание завершено!");

        isMantling = false;
        canMantle = false;
        mantlePhase = 0;

        characterController.enabled = false;
        transform.position = mantleTargetPosition;
        characterController.enabled = true;

        CurrentState = PlayerState.Walking;
        climbingStarted = false;
        walkVelocity = Vector3.zero;
    }

    private Vector3 GetHighestGripPoint()
    {
        if (leftHand != null && leftHand.IsGripped && rightHand != null && rightHand.IsGripped)
        {
            Vector3 leftPoint = leftHand.GetGripPoint();
            Vector3 rightPoint = rightHand.GetGripPoint();
            return leftPoint.y > rightPoint.y ? leftPoint : rightPoint;
        }
        else if (leftHand != null && leftHand.IsGripped)
        {
            return leftHand.GetGripPoint();
        }
        else if (rightHand != null && rightHand.IsGripped)
        {
            return rightHand.GetGripPoint();
        }

        return transform.position + Vector3.up;
    }

    private Vector3 GetClimbingForward()
    {
        if (grippedHandsCount > 0)
        {
            Vector3 gripPoint = GetHighestGripPoint();
            Vector3 toGrip = gripPoint - transform.position;
            toGrip.y = 0;

            if (toGrip.magnitude > 0.1f)
            {
                return toGrip.normalized;
            }
        }

        return transform.forward;
    }

    // ─────────────────────────────────────────
    //  ПОВОРОТ МЫШЬЮ
    // ─────────────────────────────────────────

    private void HandleMouseLook()
    {
        var mouse = Mouse.current;
        if (mouse == null || playerCamera == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        rotationY += mouseDelta.x * mouseSensitivity * 0.1f;

        float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;
        if (!invertMouseY) mouseY = -mouseY;
        rotationX += mouseY;

        rotationX = Mathf.Clamp(rotationX, -maxLookUpAngle, maxLookDownAngle);

        transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    // ─────────────────────────────────────────
    //  ПРЫЖОК
    // ─────────────────────────────────────────

    private void HandleJump()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            TryJump();
        }
    }

    private void TryJump()
    {
        if (!isGrounded) return;
        if (Time.time - lastJumpTime < jumpCooldown) return;

        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        walkVelocity.y = jumpVelocity;
        lastJumpTime = Time.time;
    }

    public void Jump() => TryJump();

    // ─────────────────────────────────────────
    //  ХОДЬБА
    // ─────────────────────────────────────────

    private void HandleWalkingInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
        {
            leftHand?.TryGrip();
        }

        if (mouse.rightButton.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
        {
            rightHand?.TryGrip();
        }
    }

    private void HandleWalking()
    {
        UpdateFallFOV(false);
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal = -1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal = 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical = 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical = -1f;

        Vector3 inputDir = new Vector3(horizontal, 0f, vertical);

        if (inputDir.magnitude > 0.1f)
        {
            inputDir.Normalize();
            Vector3 moveDir = Quaternion.Euler(0f, rotationY, 0f) * inputDir;

            walkVelocity.x = moveDir.x * walkSpeed;
            walkVelocity.z = moveDir.z * walkSpeed;
        }
        else
        {
            walkVelocity.x = 0f;
            walkVelocity.z = 0f;
        }

        if (isGrounded && walkVelocity.y < 0f)
        {
            walkVelocity.y = -2f;
        }
        else
        {
            walkVelocity.y += gravity * Time.deltaTime;
        }

        characterController.Move(walkVelocity * Time.deltaTime);
    }

    // ─────────────────────────────────────────
    //  ЛАЗАНИЕ
    // ─────────────────────────────────────────

    private void HandleClimbingInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
        {
            if (leftHand.IsGripped)
                leftHand.Release();
            else
                leftHand.TryGrip();
        }

        if (mouse.rightButton.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
        {
            if (rightHand.IsGripped)
                rightHand.Release();
            else
                rightHand.TryGrip();
        }

        if (grippedHandsCount == 2 && !isPullingUp)
        {
            HandleLateralMovement(keyboard);
        }
    }

    private void HandleLateralMovement(Keyboard keyboard)
    {
        float horizontal = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal = -1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal = 1f;

        if (Mathf.Abs(horizontal) < 0.1f) return;

        Vector3 moveDir = Quaternion.Euler(0f, rotationY, 0f) * Vector3.right * horizontal;

        // Проверяем есть ли скала в направлении движения
        if (!HasClimbableSurfaceInDirection(moveDir))
        {
            Debug.Log("Нет поверхности сбоку — нельзя двигаться!");
            return;
        }

        characterController.Move(moveDir * lateralSpeed * Time.deltaTime);
    }

    private bool HasClimbableSurfaceInDirection(Vector3 direction)
    {
        // Берём позицию между игроком и скалой
        Vector3 checkOrigin = transform.position + Vector3.up * 1f;

        // Смотрим вперёд (в сторону скалы) + немного в сторону движения
        Vector3 toWall = transform.forward;
        Vector3 checkDir = (toWall + direction * 0.5f).normalized;

        float checkDistance = 2f;

        // Ищем поверхность с тегом Climbable
        RaycastHit hit;
        bool found = Physics.SphereCast(
            checkOrigin,
            0.3f,
            checkDir,
            out hit,
            checkDistance,
            GetClimbableLayer()
        );

        if (showDebugInfo)
        {
            Debug.DrawRay(checkOrigin, checkDir * checkDistance,
                found ? Color.green : Color.red, 0.1f);
        }

        return found;
    }

    private LayerMask GetClimbableLayer()
    {
        // Берём слой из HandController
        if (leftHand != null)
        {
            // HandController хранит свой climbableLayer
            // Получаем его через рефлексию или просто дублируем поле
            return leftHand.GetClimbableLayer();
        }
        return Physics.DefaultRaycastLayers;
    }

    private void HandleHandGrip(HandController hand)
    {
        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        climbingStarted = true;
        noGripTimer = 0f;

        // Если были в режиме падения — отменяем
        if (CurrentState == PlayerState.Falling)
        {
            CurrentState = PlayerState.Climbing;
            walkVelocity = Vector3.zero;
        }

        if (hasAnchorPoint)
        {
            float distFromAnchor = hand.GetGripPoint().y - anchorGripPoint.y;

            if (distFromAnchor > maxReachDistance)
            {
                Debug.Log($"Слишком далеко от якоря! Расстояние: {distFromAnchor:F2}м");
                hand.Release();
                return;
            }
        }

        if (grippedHandsCount == 2)
        {
            UpdateAnchorPoint();
        }

        StartPullUp(hand);
    }

    private void UpdateAnchorPoint()
    {
        if (leftHand.IsGripped && rightHand.IsGripped)
        {
            float leftY = leftHand.GetGripPoint().y;
            float rightY = rightHand.GetGripPoint().y;

            anchorGripPoint = leftY < rightY
                ? leftHand.GetGripPoint()
                : rightHand.GetGripPoint();

            hasAnchorPoint = true;
        }
    }

    private void HandleHandRelease(HandController hand)
    {
        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        isPullingUp = false;

        if (grippedHandsCount == 1)
        {
            HandController anchor = leftHand.IsGripped ? leftHand : rightHand;
            anchorGripPoint = anchor.GetGripPoint();
            hasAnchorPoint = true;
        }
        else if (grippedHandsCount == 0)
        {
            hasAnchorPoint = false;
        }
    }

    private void StartPullUp(HandController grippedHand)
    {
        Vector3 gripPoint = grippedHand.GetGripPoint();

        Vector3 targetPos = new Vector3(
            transform.position.x,
            gripPoint.y - pullUpOffset,
            transform.position.z
        );

        if (Mathf.Abs(targetPos.y - transform.position.y) > 0.05f)
        {
            pullTargetPosition = targetPos;
            isPullingUp = true;
        }
    }

    private void HandlePullUp()
    {
        UpdateFallFOV(false);
        Vector3 newPos = Vector3.MoveTowards(
            transform.position,
            pullTargetPosition,
            pullUpSpeed * Time.deltaTime
        );

        characterController.Move(newPos - transform.position);

        float dist = Mathf.Abs(transform.position.y - pullTargetPosition.y);
        if (dist < 0.05f)
        {
            isPullingUp = false;
        }
    }

    private void UpdateClimbingState()
    {
        staminaSystem?.SetHandsGripped(grippedHandsCount, isGrounded);

        if (grippedHandsCount == 1)
            oneHandTimer += Time.deltaTime;
        else
            oneHandTimer = 0f;
    }

    private void HandleStaminaExhausted()
    {
        StartCoroutine(ForcedRelease());
    }

    private IEnumerator ForcedRelease()
    {
        Debug.Log("Стамина кончилась!");
        leftHand?.Release();
        yield return new WaitForSeconds(0.3f);
        rightHand?.Release();
    }

    // ─────────────────────────────────────────
    //  ПУБЛИЧНЫЕ МЕТОДЫ
    // ─────────────────────────────────────────

    public int GetGrippedHandsCount() => grippedHandsCount;
    public bool IsFalling => CurrentState == PlayerState.Falling;
    public bool IsPullingUp => isPullingUp;
    public bool IsGrounded => isGrounded;
    public bool IsWalking => CurrentState == PlayerState.Walking;
    public bool CanMantle => canMantle;
    public bool IsMantling => isMantling;

    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 10f);
    }

    public void SetInvertY(bool invert)
    {
        invertMouseY = invert;
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    [Header("Fall FOV Effect")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float maxFallFOV = 80f;
    [SerializeField] private float fovChangeSpeed = 3f;

    private void UpdateFallFOV(bool falling)
    {
        if (playerCamera == null) return;

        float targetFOV = falling ? maxFallFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Time.deltaTime * fovChangeSpeed
        );
    }
}