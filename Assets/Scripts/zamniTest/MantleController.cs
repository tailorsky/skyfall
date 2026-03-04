using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за обнаружение и выполнение мантла (перелезания через край).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class MantleController : MonoBehaviour
{
    [Header("Mantle Settings")]
    [SerializeField] private float     mantleCheckDistance  = 1.0f;
    [SerializeField] private float     mantleCheckHeight    = 1.5f;
    [SerializeField] private float     mantleSpeed          = 3f;
    [SerializeField] private float     mantleUpOffset       = 0.5f;
    [SerializeField] private float     minHorizontalAngle   = 0.7f;
    [SerializeField] private LayerMask mantleSurfaceLayer;

    [Header("Debug")]
    [SerializeField] private bool showMantleDebug = true;

    // ── публичное состояние ───────────────────────────────────
    public bool CanMantle  => canMantle;
    public bool IsMantling => isMantling;

    // ── события ──────────────────────────────────────────────
    public event System.Action OnMantleStarted;
    public event System.Action OnMantleFinished;

    // ── приватное ────────────────────────────────────────────
    private CharacterController cc;

    private bool    canMantle          = false;
    private bool    isMantling         = false;
    private Vector3 mantleTargetPosition;
    private Vector3 mantleEdgePosition;
    private int     mantlePhase        = 0;

    private void Awake() => cc = GetComponent<CharacterController>();

    private void Start()
    {
        if (mantleSurfaceLayer == 0)
            mantleSurfaceLayer = ~(1 << gameObject.layer);
    }

    // ── Вызывается из ClimbingManager ────────────────────────

    /// <summary>Проверяет возможность мантла. Передать высшую точку захвата и направление вперёд.</summary>
    public void CheckPossibility(Vector3 gripPoint, Vector3 climbForward)
    {
        canMantle = false;

        Vector3 checkStart = gripPoint + Vector3.up * mantleCheckHeight + climbForward * mantleCheckDistance;

        RaycastHit hitDown;
        bool hasTopSurface = Physics.Raycast(
            checkStart, Vector3.down, out hitDown,
            mantleCheckHeight + 1f, mantleSurfaceLayer, QueryTriggerInteraction.Ignore
        );

        if (showMantleDebug)
            Debug.DrawRay(checkStart, Vector3.down * (mantleCheckHeight + 1f),
                hasTopSurface ? Color.green : Color.red);

        if (!hasTopSurface || hitDown.normal.y < minHorizontalAngle) return;

        Vector3 targetPos    = hitDown.point + Vector3.up * mantleUpOffset;
        float   playerRadius = cc.radius;
        float   playerHeight = cc.height;

        bool hasSpace = !Physics.CheckCapsule(
            targetPos + Vector3.up * playerRadius,
            targetPos + Vector3.up * (playerHeight - playerRadius),
            playerRadius * 0.9f, mantleSurfaceLayer, QueryTriggerInteraction.Ignore
        );

        if (!hasSpace) return;

        canMantle          = true;
        mantleEdgePosition  = gripPoint + Vector3.up * 0.3f;
        mantleTargetPosition = targetPos;

        if (showMantleDebug)
        {
            Debug.DrawLine(transform.position, mantleEdgePosition, Color.yellow);
            Debug.DrawLine(mantleEdgePosition, mantleTargetPosition, Color.cyan);
        }
    }

    /// <summary>Читает ввод и начинает мантл если возможно.</summary>
    public void HandleInput()
    {
        if (!canMantle || isMantling) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            StartMantle();
    }

    /// <summary>Тик выполнения мантла. Вызывать только пока IsMantling == true.</summary>
    public void Tick()
    {
        float step = mantleSpeed * Time.deltaTime;

        switch (mantlePhase)
        {
            case 0:
                Vector3 edgeTarget = new Vector3(transform.position.x, mantleEdgePosition.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, edgeTarget, step);
                if (Vector3.Distance(transform.position, edgeTarget) < 0.05f)
                    mantlePhase = 1;
                break;

            case 1:
                transform.position = Vector3.MoveTowards(transform.position, mantleTargetPosition, step);
                if (Vector3.Distance(transform.position, mantleTargetPosition) < 0.05f)
                    FinishMantle();
                break;
        }
    }

    // ── Приватные ─────────────────────────────────────────────
    private void StartMantle()
    {
        isMantling   = true;
        mantlePhase  = 0;
        OnMantleStarted?.Invoke();
    }

    private void FinishMantle()
    {
        isMantling  = false;
        canMantle   = false;
        mantlePhase = 0;

        cc.enabled         = false;
        transform.position = mantleTargetPosition;
        cc.enabled         = true;

        OnMantleFinished?.Invoke();
    }
}