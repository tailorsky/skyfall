using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Удерживай LMB/RMB чтобы переставить руку, отпусти чтобы закрепить.
/// W/S/A/D двигают игрока по стене. Игрок подтягивается к руке по всем осям.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ClimbingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandController leftHand;
    [SerializeField] private HandController rightHand;

    [Header("Movement")]
    [SerializeField] private float climbSpeed       = 2.5f;
    [SerializeField] private float maxClimbDistance = 1.5f; // макс расстояние от точки захвата

    [Header("Pull To Grip")]
    [SerializeField] private float pullSpeed   = 5f;
    [SerializeField] private float pullOffset  = 0.8f;
    [SerializeField] private float pullXZBlend = 0.4f;

    [Header("Free Hand Visual")]
    [SerializeField] private float freeHandAlpha = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // ── Публичное состояние ───────────────────────────────────
    public int  GrippedHandsCount => grippedHandsCount;
    public bool IsPullingUp       => isPullingUp;
    public bool HasAnyGrip        => grippedHandsCount > 0;

    public event System.Action OnGripChanged;

    // ── Приватное ─────────────────────────────────────────────
    private CharacterController cc;
    private StaminaSystem       staminaSystem;

    private int     grippedHandsCount = 0;
    private bool    isPullingUp       = false;
    private Vector3 pullTargetPosition;

    private Vector3 anchorGripPoint = Vector3.zero;
    private bool    hasAnchorPoint  = false;

    private bool leftHeld  = false;
    private bool rightHeld = false;

    private void Awake()
    {
        cc            = GetComponent<CharacterController>();
        staminaSystem = GetComponent<StaminaSystem>();
    }

    private void OnEnable()
    {
        if (leftHand  != null) { leftHand.OnGrip  += OnHandGrip; leftHand.OnRelease  += OnHandRelease; }
        if (rightHand != null) { rightHand.OnGrip += OnHandGrip; rightHand.OnRelease += OnHandRelease; }
        if (staminaSystem != null) staminaSystem.OnStaminaExhausted += OnStaminaExhausted;
    }

    private void OnDisable()
    {
        if (leftHand  != null) { leftHand.OnGrip  -= OnHandGrip; leftHand.OnRelease  -= OnHandRelease; }
        if (rightHand != null) { rightHand.OnGrip -= OnHandGrip; rightHand.OnRelease -= OnHandRelease; }
        if (staminaSystem != null) staminaSystem.OnStaminaExhausted -= OnStaminaExhausted;
    }

    // ── Вызывается из ClimbingManager ────────────────────────

    public void Tick(bool isGrounded)
    {
        HandleHoldInput();
        if (isPullingUp)
            TickPullToGrip();
        else
            HandleMovement();

        staminaSystem?.SetHandsGripped(grippedHandsCount, isGrounded);
    }

    public void TryGripFromWalking()
    {
        var mouse = Mouse.current;
        var kb    = Keyboard.current;
        if (mouse == null || kb == null) return;

        if (mouse.leftButton.wasPressedThisFrame  || kb.qKey.wasPressedThisFrame) leftHand?.TryGrip();
        if (mouse.rightButton.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) rightHand?.TryGrip();
    }

    public void ReleaseAll()
    {
        leftHand?.Release();
        rightHand?.Release();
        leftHeld  = false;
        rightHeld = false;
    }

    // ── Хелперы ───────────────────────────────────────────────

    public Vector3 GetHighestGripPoint()
    {
        bool l = leftHand  != null && leftHand.IsGripped;
        bool r = rightHand != null && rightHand.IsGripped;

        if (l && r)
            return leftHand.GetGripPoint().y > rightHand.GetGripPoint().y
                ? leftHand.GetGripPoint() : rightHand.GetGripPoint();
        if (l) return leftHand.GetGripPoint();
        if (r) return rightHand.GetGripPoint();
        return transform.position + Vector3.up;
    }

    public Vector3 GetClimbingForward()
    {
        if (grippedHandsCount > 0)
        {
            Vector3 toGrip = GetHighestGripPoint() - transform.position;
            toGrip.y = 0f;
            if (toGrip.magnitude > 0.1f) return toGrip.normalized;
        }
        return transform.forward;
    }

    public LayerMask GetClimbableLayer()
        => leftHand != null ? leftHand.GetClimbableLayer() : Physics.DefaultRaycastLayers;

    // ── Hold-логика ───────────────────────────────────────────

    private void HandleHoldInput()
    {
        var mouse = Mouse.current;
        var kb    = Keyboard.current;
        if (mouse == null || kb == null) return;

        bool leftDown  = mouse.leftButton.isPressed  || kb.qKey.isPressed;
        bool rightDown = mouse.rightButton.isPressed || kb.eKey.isPressed;

        HandleHand(leftHand,  ref leftHeld,  leftDown);
        HandleHand(rightHand, ref rightHeld, rightDown);
    }

    private void HandleHand(HandController hand, ref bool held, bool isDown)
    {
        if (hand == null) return;

        if (isDown && !held)
        {
            held = true;
            hand.Release();
            SetHandAlpha(hand, freeHandAlpha);
        }
        else if (!isDown && held)
        {
            held = false;
            SetHandAlpha(hand, 1f);
            hand.TryGrip();
        }
    }

    // ── Движение по стене ─────────────────────────────────────

    private void HandleMovement()
    {
        if (grippedHandsCount == 0) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;

        if (Mathf.Abs(h) < 0.1f && Mathf.Abs(v) < 0.1f) return;

        // Нельзя удалиться дальше maxClimbDistance от точки захвата в любую сторону
        float distFromGrip = Vector3.Distance(transform.position, GetHighestGripPoint());
        if (distFromGrip >= maxClimbDistance)
        {
            // Разрешаем только движение ОБРАТНО к точке захвата
            Vector3 toGrip   = (GetHighestGripPoint() - transform.position).normalized;
            Vector3 limitRight = transform.right; limitRight.y = 0f; limitRight.Normalize();
            Vector3 inputDir = (limitRight * h + Vector3.up * v).normalized;
            if (Vector3.Dot(inputDir, toGrip) < 0f) return;
        }

        Vector3 right   = transform.right; right.y = 0f; right.Normalize();
        Vector3 moveDir = (right * h + Vector3.up * v).normalized;

        cc.Move(moveDir * climbSpeed * Time.deltaTime);

        if (showDebugInfo) Debug.Log($"[Climb] h={h} v={v} distFromGrip={distFromGrip:F2}");
    }

    // ── Подтягивание к руке ───────────────────────────────────

    private void StartPullToGrip(HandController hand)
    {
        Vector3 grip = hand.GetGripPoint();

        Vector3 target = new Vector3(
            Mathf.Lerp(transform.position.x, grip.x, pullXZBlend),
            grip.y - pullOffset,
            Mathf.Lerp(transform.position.z, grip.z, pullXZBlend)
        );

        if (Vector3.Distance(transform.position, target) > 0.05f)
        {
            pullTargetPosition = target;
            isPullingUp        = true;
        }
    }

    private void TickPullToGrip()
    {
        Vector3 newPos = Vector3.MoveTowards(transform.position, pullTargetPosition, pullSpeed * Time.deltaTime);
        cc.Move(newPos - transform.position);

        if (Vector3.Distance(transform.position, pullTargetPosition) < 0.08f)
            isPullingUp = false;
    }

    // ── Захват / отпуск ───────────────────────────────────────

    private void OnHandGrip(HandController hand)
    {
        RefreshGripCount();
        if (grippedHandsCount >= 1) UpdateAnchorPoint();
        StartPullToGrip(hand);
        OnGripChanged?.Invoke();
    }

    private void OnHandRelease(HandController hand)
    {
        RefreshGripCount();
        isPullingUp = false;

        if (grippedHandsCount == 0)
            hasAnchorPoint = false;
        else
        {
            HandController anchor = leftHand.IsGripped ? leftHand : rightHand;
            anchorGripPoint = anchor.GetGripPoint();
            hasAnchorPoint  = true;
        }

        OnGripChanged?.Invoke();
    }

    private void RefreshGripCount()
    {
        grippedHandsCount = 0;
        if (leftHand  != null && leftHand.IsGripped)  grippedHandsCount++;
        if (rightHand != null && rightHand.IsGripped) grippedHandsCount++;
    }

    private void UpdateAnchorPoint()
    {
        anchorGripPoint = GetHighestGripPoint();
        hasAnchorPoint  = true;
    }

    // ── Визуал прозрачности ───────────────────────────────────

    private void SetHandAlpha(HandController hand, float alpha)
    {
        if (hand == null) return;
        var r = hand.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Color c = r.material.color;
        c.a = alpha;
        r.material.color = c;
    }

    // ── Стамина ───────────────────────────────────────────────

    private void OnStaminaExhausted() => StartCoroutine(ForcedRelease());

    private IEnumerator ForcedRelease()
    {
        leftHand?.Release();
        yield return new WaitForSeconds(0.3f);
        rightHand?.Release();
    }

    // ── Gizmos ────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!hasAnchorPoint) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchorGripPoint, 0.15f);
        Gizmos.DrawLine(transform.position, anchorGripPoint);

        // Показываем зону maxClimbDistance
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawWireSphere(anchorGripPoint, maxClimbDistance);
    }
}