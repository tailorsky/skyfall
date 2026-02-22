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

    [Header("Lateral Movement Limits")]
    [SerializeField] private float maxLateralDistance = 3f;  // Увеличил дефолт
    [SerializeField] private float maxLateralFromGrip = 1.5f;
    [SerializeField] private bool limitLateralMovement = true;  // Можно отключить для теста

    private Vector3 climbStartPosition;
    private bool hasClimbStartPosition = false;

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
    [SerializeField] private float shakeFrequency = 25f;
    [SerializeField] private float shakeAmplitude = 0.08f;
    [SerializeField] private float shakeRampUpSpeed = 2f;

    private float currentShakeIntensity = 0f;
    private float fallTime = 0f;
    private Vector3 cameraOriginalLocalPos;
    private bool isCameraShaking = false;

    private float rotationX = 0f;
    private float rotationY = 0f;

    [Header("Mantle Settings")]
    [SerializeField] private float mantleCheckDistance = 1.0f;
    [SerializeField] private float mantleCheckHeight = 1.5f;
    [SerializeField] private float mantleSpeed = 3f;
    [SerializeField] private float mantleUpOffset = 0.5f;
    [SerializeField] private float minHorizontalAngle = 0.7f;
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
    [SerializeField] private float noGripGracePeriod = 0.5f;

    [Header("Fall FOV Effect")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float maxFallFOV = 80f;
    [SerializeField] private float fovChangeSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;  // Включил по умолчанию
    [SerializeField] private bool showMantleDebug = true;

    // Компоненты
    private StaminaSystem staminaSystem;
    private CharacterController characterController;
    private FallDamageSystem fallDamageSystem;

    public enum PlayerState
    {
        Walking,
        Climbing,
        Falling,
        Mantling
    }

    public PlayerState CurrentState { get; private set; } = PlayerState.Walking;

    private Vector3 walkVelocity;
    private bool isGrounded;

    private int grippedHandsCount = 0;
    private bool isPullingUp = false;
    private Vector3 pullTargetPosition;
    private float noGripTimer = 0f;
    private float oneHandTimer = 0f;
    private bool climbingStarted = false;
    private float climbingFallStartY = 0f;

    private int playerLayer;

    // ─────────────────────────────────────────
    //  ИНИЦИАЛИЗАЦИЯ
    // ─────────────────────────────────────────

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        staminaSystem = GetComponent<StaminaSystem>();
        fallDamageSystem = GetComponent<FallDamageSystem>();

        playerLayer = gameObject.layer;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (mantleSurfaceLayer == 0)
        {
            mantleSurfaceLayer = ~(1 << playerLayer);
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

        if (GameManager.Instance.CurrentState == GameManager.GameState.Win ||
            GameManager.Instance.CurrentState == GameManager.GameState.GameOver)
        {
            return;
        }

        if (GameManager.Instance.CurrentState == GameManager.GameState.Falling)
        {
            HandleMouseLook();
            return;
        }

        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        if (fallDamageSystem != null && !fallDamageSystem.CanMove())
        {
            ApplyGravityOnly();
            return;
        }

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
                HandleMouseLook();
                HandleClimbingFall();
                break;
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
    //  ЛАЗАНИЕ — ВВОД
    // ─────────────────────────────────────────

    private void HandleClimbingInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // Захват/отпуск руками
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

        // ═══════════════════════════════════════════════════
        // БОКОВОЕ ДВИЖЕНИЕ
        // ═══════════════════════════════════════════════════

        // Двигаемся если хотя бы ОДНА рука держится (не обязательно две!)
        if (grippedHandsCount >= 1 && !isPullingUp)
        {
            HandleLateralMovement(keyboard);
        }
    }

    // ─────────────────────────────────────────
    //  БОКОВОЕ ДВИЖЕНИЕ
    // ─────────────────────────────────────────

    private void HandleLateralMovement(Keyboard keyboard)
    {
        float horizontal = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal = -1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal = 1f;

        if (Mathf.Abs(horizontal) < 0.1f) return;

        if (showDebugInfo)
        {
            Debug.Log($"[Lateral] Input: {horizontal}, Hands: {grippedHandsCount}");
        }

        // Направление движения
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();
        Vector3 moveDir = right * horizontal;

        // ═══════════════════════════════════════════════════
        // ПРОВЕРКА РАССТОЯНИЯ ОТ ДАЛЬНЕЙ РУКИ
        // ═══════════════════════════════════════════════════

        if (grippedHandsCount >= 1)
        {
            // Находим позицию дальней руки (та что дальше от направления движения)
            Vector3 farthestGripPoint = GetFarthestGripPointInDirection(-moveDir);

            // Позиция игрока после движения
            Vector3 nextPosition = transform.position + moveDir * lateralSpeed * Time.deltaTime;

            // Расстояние по горизонтали от дальней руки до новой позиции
            Vector3 fromGrip = nextPosition - farthestGripPoint;
            fromGrip.y = 0f; // только горизонтальное расстояние
            float distanceFromGrip = fromGrip.magnitude;

            if (distanceFromGrip > maxLateralFromGrip)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[Lateral] BLOCKED! Расстояние от руки: {distanceFromGrip:F2}м > {maxLateralFromGrip}м");
                }
                return;
            }
        }

        // ═══════════════════════════════════════════════════
        // ПРОВЕРКА ЛИМИТА (только если включено)
        // ═══════════════════════════════════════════════════

        if (limitLateralMovement && hasClimbStartPosition)
        {
            Vector3 currentOffset = transform.position - climbStartPosition;
            currentOffset.y = 0f;

            Vector3 nextOffset = currentOffset + moveDir * lateralSpeed * Time.deltaTime;
            float nextDistance = nextOffset.magnitude;

            if (nextDistance > maxLateralDistance)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[Lateral] BLOCKED! Distance: {nextDistance:F2}m > {maxLateralDistance}m");
                }
                return;
            }
        }

        // ═══════════════════════════════════════════════════
        // ПРОВЕРКА ПОВЕРХНОСТИ
        // ═══════════════════════════════════════════════════

        bool hasSurface = HasClimbableSurfaceInDirection(moveDir);

        if (showDebugInfo)
        {
            Debug.Log($"[Lateral] Surface check: {hasSurface}");
        }

        // ═══════════════════════════════════════════════════
        // ДВИГАЕМ ПЕРСОНАЖА
        // ═══════════════════════════════════════════════════

        Vector3 movement = moveDir * lateralSpeed * Time.deltaTime;
        characterController.Move(movement);

        // Обновляем якорь после движения
        UpdateAnchorPoint();

        if (showDebugInfo)
        {
            Debug.Log($"[Lateral] Moved: {movement.magnitude:F3}m");
        }
    }

    /// <summary>
    /// Возвращает точку захвата руки которая дальше всего в указанном направлении
    /// </summary>
    private Vector3 GetFarthestGripPointInDirection(Vector3 direction)
    {
        direction.y = 0f;
        direction.Normalize();

        Vector3 leftPoint = Vector3.zero;
        Vector3 rightPoint = Vector3.zero;
        bool hasLeft = false;
        bool hasRight = false;

        if (leftHand != null && leftHand.IsGripped)
        {
            leftPoint = leftHand.GetGripPoint();
            hasLeft = true;
        }

        if (rightHand != null && rightHand.IsGripped)
        {
            rightPoint = rightHand.GetGripPoint();
            hasRight = true;
        }

        // Если только одна рука — возвращаем её
        if (hasLeft && !hasRight) return leftPoint;
        if (hasRight && !hasLeft) return rightPoint;

        // Если обе руки — выбираем ту что дальше в указанном направлении
        if (hasLeft && hasRight)
        {
            Vector3 leftFlat = leftPoint;
            Vector3 rightFlat = rightPoint;
            leftFlat.y = 0f;
            rightFlat.y = 0f;

            float leftDot = Vector3.Dot(leftFlat, direction);
            float rightDot = Vector3.Dot(rightFlat, direction);

            return leftDot > rightDot ? leftPoint : rightPoint;
        }

        // Нет рук — возвращаем позицию игрока
        return transform.position;
    }

    private float GetLateralOffset()
    {
        if (!hasClimbStartPosition) return 0f;

        Vector3 offset = transform.position - climbStartPosition;
        offset.y = 0f;
        return offset.magnitude;
    }

    private bool HasClimbableSurfaceInDirection(Vector3 direction)
    {
        Vector3 checkOrigin = transform.position + Vector3.up * 1f;

        // Ищем стену впереди + в направлении движения
        Vector3 forwardDir = GetClimbingForward();
        Vector3 checkDir = (forwardDir + direction * 0.3f).normalized;

        float checkDistance = 2f;
        LayerMask climbableLayer = GetClimbableLayer();

        RaycastHit hit;
        bool found = Physics.SphereCast(
            checkOrigin,
            0.3f,
            checkDir,
            out hit,
            checkDistance,
            climbableLayer
        );

        // Debug визуализация
        Debug.DrawRay(checkOrigin, checkDir * checkDistance, found ? Color.green : Color.red, 0.1f);

        return found;
    }

    // ─────────────────────────────────────────
    //  ЗАХВАТ
    // ─────────────────────────────────────────

    private void HandleHandGrip(HandController hand)
    {
        int prevCount = grippedHandsCount;

        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        climbingStarted = true;
        noGripTimer = 0f;

        // Запоминаем начальную позицию при первом захвате
        if (prevCount == 0 && grippedHandsCount > 0)
        {
            climbStartPosition = transform.position;
            hasClimbStartPosition = true;

            if (showDebugInfo)
            {
                Debug.Log($"[Climb] Start position set: {climbStartPosition}");
            }
        }

        if (CurrentState == PlayerState.Falling)
        {
            CurrentState = PlayerState.Climbing;
            walkVelocity = Vector3.zero;
            StopCameraShake();
        }

        if (hasAnchorPoint)
        {
            float distFromAnchor = hand.GetGripPoint().y - anchorGripPoint.y;

            if (distFromAnchor > maxReachDistance)
            {
                Debug.Log($"Слишком далеко от якоря! {distFromAnchor:F2}м");
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

    private void HandleHandRelease(HandController hand)
    {
        grippedHandsCount = 0;
        if (leftHand != null && leftHand.IsGripped) grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;

        isPullingUp = false;

        if (grippedHandsCount == 0)
        {
            hasClimbStartPosition = false;
            hasAnchorPoint = false;
        }
        else if (grippedHandsCount == 1)
        {
            HandController anchor = leftHand.IsGripped ? leftHand : rightHand;
            anchorGripPoint = anchor.GetGripPoint();
            hasAnchorPoint = true;
        }
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

            // Обновляем базовую позицию после подтягивания
            if (hasClimbStartPosition)
            {
                climbStartPosition = transform.position;
            }
        }
    }

    // ─────────────────────────────────────────
    //  MANTLE
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
            mantleSurfaceLayer,
            QueryTriggerInteraction.Ignore
        );

        if (showMantleDebug)
        {
            Debug.DrawRay(checkStart, Vector3.down * (mantleCheckHeight + 1f),
                hasTopSurface ? Color.green : Color.red);
        }

        if (!hasTopSurface) return;
        if (hitDown.normal.y < minHorizontalAngle) return;

        Vector3 targetPos = hitDown.point + Vector3.up * mantleUpOffset;

        float playerRadius = characterController.radius;
        float playerHeight = characterController.height;

        bool hasSpace = !Physics.CheckCapsule(
            targetPos + Vector3.up * playerRadius,
            targetPos + Vector3.up * (playerHeight - playerRadius),
            playerRadius * 0.9f,
            mantleSurfaceLayer,
            QueryTriggerInteraction.Ignore
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

        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            StartMantle();
        }
    }

    private void StartMantle()
    {
        if (!canMantle || isMantling) return;

        isMantling = true;
        mantlePhase = 0;
        CurrentState = PlayerState.Mantling;

        leftHand?.Release();
        rightHand?.Release();

        walkVelocity = Vector3.zero;
        hasClimbStartPosition = false;
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

    // ─────────────────────────────────────────
    //  ПАДЕНИЕ
    // ─────────────────────────────────────────

    private void HandleClimbingFall()
    {
        UpdateFallFOV(true);
        fallTime += Time.deltaTime;
        ApplyFallCameraShake(fallTime);

        walkVelocity.y += gravity * Time.deltaTime;
        walkVelocity.x = 0f;
        walkVelocity.z = 0f;

        characterController.Move(walkVelocity * Time.deltaTime);
        CheckGrounded();

        if (isGrounded)
        {
            float fallDistance = climbingFallStartY - transform.position.y;
            StopCameraShake();

            if (fallDamageSystem != null)
                fallDamageSystem.ProcessClimbingLanding(climbingFallStartY, transform.position.y);

            CurrentState = PlayerState.Walking;
            climbingStarted = false;
            walkVelocity = Vector3.zero;
            noGripTimer = 0f;
            fallTime = 0f;
            hasClimbStartPosition = false;
        }
    }

    private void ApplyFallCameraShake(float timeInFall)
    {
        if (playerCamera == null) return;

        if (!isCameraShaking)
        {
            cameraOriginalLocalPos = playerCamera.transform.localPosition;
            isCameraShaking = true;
        }

        currentShakeIntensity = Mathf.Min(
            timeInFall * shakeRampUpSpeed * shakeAmplitude,
            shakeAmplitude
        );

        float shakeX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f * currentShakeIntensity;
        float shakeY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f * currentShakeIntensity;

        playerCamera.transform.localPosition = cameraOriginalLocalPos + new Vector3(shakeX, shakeY, 0f);
    }

    private void StopCameraShake()
    {
        if (playerCamera == null) return;

        isCameraShaking = false;
        currentShakeIntensity = 0f;
        playerCamera.transform.localPosition = cameraOriginalLocalPos;
    }

    // ─────────────────────────────────────────
    //  СОСТОЯНИЕ
    // ─────────────────────────────────────────

    private void CheckGrounded()
    {
        if (groundLayer != 0)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
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
            hasClimbStartPosition = false;
            return;
        }

        if (climbingStarted && !isGrounded)
        {
            noGripTimer += Time.deltaTime;

            if (noGripTimer >= noGripGracePeriod && CurrentState != PlayerState.Falling)
            {
                climbingFallStartY = transform.position.y;
                CurrentState = PlayerState.Falling;

                if (fallDamageSystem != null)
                    fallDamageSystem.StartClimbingFall(climbingFallStartY);
            }
            return;
        }

        CurrentState = PlayerState.Walking;
    }

    private void UpdateClimbingState()
    {
        staminaSystem?.SetHandsGripped(grippedHandsCount, isGrounded);

        if (grippedHandsCount == 1)
            oneHandTimer += Time.deltaTime;
        else
            oneHandTimer = 0f;
    }

    // ─────────────────────────────────────────
    //  ПОВОРОТ
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

        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
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

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = (forward * vertical + right * horizontal);

        if (moveDir.magnitude > 0.1f)
        {
            moveDir.Normalize();
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
    //  ВСПОМОГАТЕЛЬНЫЕ
    // ─────────────────────────────────────────

    private Vector3 GetHighestGripPoint()
    {
        if (leftHand != null && leftHand.IsGripped && rightHand != null && rightHand.IsGripped)
        {
            return leftHand.GetGripPoint().y > rightHand.GetGripPoint().y
                ? leftHand.GetGripPoint()
                : rightHand.GetGripPoint();
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

    private LayerMask GetClimbableLayer()
    {
        if (leftHand != null)
        {
            return leftHand.GetClimbableLayer();
        }
        return Physics.DefaultRaycastLayers;
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

    // ─────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!hasClimbStartPosition) return;

        // Зона лимита
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(new Vector3(climbStartPosition.x, transform.position.y, climbStartPosition.z), maxLateralDistance);

        // Текущее смещение
        float offset = GetLateralOffset();
        Gizmos.color = offset > maxLateralDistance * 0.8f ? Color.red : Color.green;
        Gizmos.DrawLine(
            new Vector3(climbStartPosition.x, transform.position.y, climbStartPosition.z),
            transform.position
        );
    }

    // ─────────────────────────────────────────
    //  ПУБЛИЧНЫЕ
    // ─────────────────────────────────────────

    public int GetGrippedHandsCount() => grippedHandsCount;
    public bool IsFalling => CurrentState == PlayerState.Falling;
    public bool IsPullingUp => isPullingUp;
    public bool IsGrounded => isGrounded;
    public bool IsWalking => CurrentState == PlayerState.Walking;
    public bool CanMantle => canMantle;
    public bool IsMantling => isMantling;

    public void SetMouseSensitivity(float sensitivity) => mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 10f);
    public void SetInvertY(bool invert) => invertMouseY = invert;
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void Jump() => TryJump();
}