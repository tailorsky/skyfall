using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Мантл через CharacterController — без телепортации, уважает коллайдеры.
/// Фазы: 0 — подтянуться вверх, 1 — выдвинуться вперёд, 2 — встать.
/// Отмена: S или Escape. Таймаут если застрял.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class MantleController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float checkDistance   = 0.8f;
    [SerializeField] private float checkHeight     = 1.5f;
    [SerializeField] private float minSurfaceAngle = 0.7f;
    [SerializeField] private LayerMask surfaceLayer;

    [Header("Movement")]
    [SerializeField] private float upSpeed      = 6f;
    [SerializeField] private float forwardSpeed = 4f;
    [SerializeField] private float upOffset     = 0.2f;

    [Header("Safety")]
    [SerializeField] private float mantleTimeout  = 2f;   // максимальное время на мантл
    [SerializeField] private float stuckThreshold = 0.02f; // если за кадр двигаемся меньше — застряли
    [SerializeField] private float stuckTime      = 0.5f;  // сколько секунд стоим прежде чем отменить

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ── Публичное состояние ───────────────────────────────────
    public bool CanMantle  => canMantle;
    public bool IsMantling => isMantling;

    public event System.Action OnMantleStarted;
    public event System.Action OnMantleFinished;
    public event System.Action OnMantieCancelled;

    // ── Приватное ─────────────────────────────────────────────
    private CharacterController cc;

    private bool    canMantle  = false;
    private bool    isMantling = false;
    private int     phase      = 0;

    private Vector3 edgePoint;
    private Vector3 targetTop;
    private Vector3 forwardDir;

    // Безопасность
    private float   mantleTimer    = 0f;
    private float   stuckTimer     = 0f;
    private Vector3 lastPosition;

    private void Awake() => cc = GetComponent<CharacterController>();

    private void Start()
    {
        if (surfaceLayer == 0)
            surfaceLayer = ~(1 << gameObject.layer);
    }

    // ── Вызывается из ClimbingManager ────────────────────────

    public void CheckPossibility(Vector3 gripPoint, Vector3 climbForward)
    {
        canMantle  = false;
        forwardDir = climbForward;

        Vector3 checkOrigin = gripPoint + Vector3.up * checkHeight + climbForward * checkDistance;

        RaycastHit hitDown;
        bool hasSurface = Physics.Raycast(
            checkOrigin, Vector3.down, out hitDown,
            checkHeight + 1f, surfaceLayer, QueryTriggerInteraction.Ignore
        );

        if (showDebug)
            Debug.DrawRay(checkOrigin, Vector3.down * (checkHeight + 1f),
                hasSurface ? Color.green : Color.red);

        if (!hasSurface || hitDown.normal.y < minSurfaceAngle) return;

        Vector3 standPos = hitDown.point + Vector3.up * upOffset;

        bool hasSpace = !Physics.CheckCapsule(
            standPos + Vector3.up * cc.radius,
            standPos + Vector3.up * (cc.height - cc.radius),
            cc.radius * 0.9f,
            surfaceLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!hasSpace) return;

        canMantle = true;
        edgePoint = hitDown.point;
        targetTop = standPos;

        if (showDebug)
        {
            Debug.DrawLine(transform.position, edgePoint, Color.yellow);
            Debug.DrawLine(edgePoint, targetTop, Color.cyan);
        }
    }

    public void HandleInput()
    {
        if (isMantling)
        {
            // Отмена мантла по S или Escape
            var kb = Keyboard.current;
            if (kb != null &&
                kb.sKey.wasPressedThisFrame)
            {
                CancelMantle();
                return;
            }
            return;
        }

        if (!canMantle) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
            StartMantle();
    }

    public void Tick()
    {
        if (!isMantling) return;

        // ── Таймаут ───────────────────────────────────────────
        mantleTimer += Time.deltaTime;
        if (mantleTimer >= mantleTimeout)
        {
            Debug.LogWarning("[Mantle] Таймаут — отменяем");
            CancelMantle();
            return;
        }

        // ── Проверка застревания ──────────────────────────────
        float moved = Vector3.Distance(transform.position, lastPosition);
        if (moved < stuckThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTime)
            {
                Debug.LogWarning("[Mantle] Застрял — отменяем");
                CancelMantle();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPosition = transform.position;

        // ── Фазы ─────────────────────────────────────────────
        switch (phase)
        {
            case 0: // Подтягиваемся вверх
            {
                float targetY = edgePoint.y + upOffset;
                float deltaY  = targetY - transform.position.y;
                float stepY   = upSpeed * Time.deltaTime;

                cc.Move(Vector3.up * Mathf.Min(deltaY, stepY));

                if (transform.position.y >= targetY - 0.05f)
                    phase = 1;
                break;
            }

            case 1: // Выдвигаемся вперёд
            {
                Vector3 flatTarget = new Vector3(targetTop.x, transform.position.y, targetTop.z);
                Vector3 dir        = flatTarget - transform.position;

                if (dir.magnitude < 0.05f) { phase = 2; break; }

                cc.Move(dir.normalized * forwardSpeed * Time.deltaTime);
                break;
            }

            case 2: // Опускаемся на поверхность
            {
                float deltaY = targetTop.y - transform.position.y;

                if (Mathf.Abs(deltaY) < 0.05f || cc.isGrounded)
                {
                    FinishMantle();
                    break;
                }

                cc.Move(Vector3.down * forwardSpeed * Time.deltaTime);
                break;
            }
        }
    }

    // ── Приватные ─────────────────────────────────────────────

    private void StartMantle()
    {
        isMantling   = true;
        phase        = 0;
        mantleTimer  = 0f;
        stuckTimer   = 0f;
        lastPosition = transform.position;
        OnMantleStarted?.Invoke();
    }

    private void FinishMantle()
    {
        isMantling = false;
        canMantle  = false;
        phase      = 0;
        OnMantleFinished?.Invoke();
    }

    private void CancelMantle()
    {
        isMantling = false;
        canMantle  = false;
        phase      = 0;
        mantleTimer = 0f;
        stuckTimer  = 0f;
        OnMantieCancelled?.Invoke();
        if (showDebug) Debug.Log("[Mantle] Отменён");
    }
}