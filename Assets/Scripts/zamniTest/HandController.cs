using UnityEngine;
using System;

public class HandController : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Hand Settings")]
    [SerializeField] private HandSide  handSide;
    [SerializeField] private float     reachDistance  = 2f;
    [SerializeField] private float     gripRadius     = 0.5f;
    [SerializeField] private float     maxGripDistance = 1.8f; // макс расстояние от игрока
    [SerializeField] private LayerMask climbableLayer;

    [Header("Follow")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float gripSpeed   = 10f;

    [Header("Visual")]
    [SerializeField] private Renderer  handRenderer;
    [SerializeField] private Material  grippedMaterial;
    [SerializeField] private Material  releasedMaterial;
    [SerializeField] private Transform gripAnchor;

    [Header("Anim")]
    [SerializeField] private Animator animator;

    // ── Публичное состояние ───────────────────────────────────
    public bool     IsGripped  { get; private set; }
    public HandSide Side       => handSide;

    public event Action<HandController> OnGrip;
    public event Action<HandController> OnRelease;
    public event Action<HandController> OnGripFailed;

    // ── Приватное ─────────────────────────────────────────────
    private Camera    playerCamera;
    private Transform cameraTransform;
    private Transform playerTransform; // корень игрока

    private Vector3    gripPoint;
    private Vector3    gripWorldPosition;
    private Quaternion initialLocalRotation;

    private float gripCooldown = 0.15f;
    private float lastGripTime = -1f;

    // ── Инициализация ─────────────────────────────────────────

    private void Awake()
    {
        // Позиция покоя по стороне
        Vector3 side = handSide == HandSide.Left
            ? new Vector3(-0.3f, -0.5f, -0.4f)
            : new Vector3( 0.3f, -0.5f, -0.4f);

        // restLocalPosition задаётся неявно через side в GetRestWorldPosition
        _restLocal = side;
    }

    private Vector3 _restLocal;

    private void Start()
    {
        FindCamera();
        initialLocalRotation = transform.localRotation;

        // Ищем корень игрока (объект с CharacterController)
        playerTransform = GetComponentInParent<CharacterController>()?.transform ?? transform.root;

        if (cameraTransform != null)
            transform.position = GetRestWorldPosition();
    }

    private void FindCamera()
    {
        playerCamera = Camera.main ?? GetComponentInParent<Camera>() ?? FindObjectOfType<Camera>();
        if (playerCamera != null)
            cameraTransform = playerCamera.transform;
        else
            Debug.LogError($"[{handSide}] Камера не найдена!");
    }

    // ── Update ────────────────────────────────────────────────

    private void Update()
    {
        if (playerCamera == null) { FindCamera(); return; }
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

        if (IsGripped)
            StayAtGripPoint();
        else
            FollowCamera();
    }

    // ── Движение руки ─────────────────────────────────────────

    private void FollowCamera()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            GetRestWorldPosition(),
            Time.deltaTime * followSpeed
        );
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            cameraTransform.rotation * initialLocalRotation,
            Time.deltaTime * followSpeed
        );
    }

    private void StayAtGripPoint()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            gripWorldPosition,
            Time.deltaTime * gripSpeed
        );
    }

    private Vector3 GetRestWorldPosition()
        => cameraTransform.TransformPoint(_restLocal);

    // ── Захват ────────────────────────────────────────────────

    public bool TryGrip()
    {
        if (IsGripped) return true;
        if (Time.time - lastGripTime < gripCooldown) return false;
        if (playerCamera == null) { FindCamera(); return false; }

        lastGripTime = Time.time;

        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            :  playerCamera.transform.right * 0.3f;

        Ray ray = new Ray(playerCamera.transform.position + offset, playerCamera.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.red, 0.5f);

        RaycastHit hit;
        if (!Physics.SphereCast(ray, gripRadius, out hit, reachDistance, climbableLayer))
        {
            OnGripFailed?.Invoke(this);
            LogGripFailReason(ray);
            return false;
        }

        // Проверка расстояния от игрока
        float distFromPlayer = Vector3.Distance(hit.point, playerTransform.position);
        if (distFromPlayer > maxGripDistance)
        {
            Debug.LogWarning($"[{handSide}] Слишком далеко от игрока: {distFromPlayer:F2}м > {maxGripDistance}м");
            OnGripFailed?.Invoke(this);
            return false;
        }

        // Захват успешен
        gripPoint = hit.point;
        gripWorldPosition = gripAnchor != null
            ? hit.point + (transform.position - gripAnchor.position)
            : hit.point;

        IsGripped = true;
        animator?.SetBool("IsGripped", true);

        if (handRenderer != null && grippedMaterial != null)
            handRenderer.material = grippedMaterial;

        OnGrip?.Invoke(this);
        Debug.Log($"[{handSide}] Захват! {hit.collider.name} | dist={distFromPlayer:F2}м");
        return true;
    }

    public void Release()
    {
        if (!IsGripped) return;

        IsGripped = false;
        animator?.SetBool("IsGripped", false);

        if (handRenderer != null && releasedMaterial != null)
            handRenderer.material = releasedMaterial;

        OnRelease?.Invoke(this);
    }

    // ── Публичные геттеры ─────────────────────────────────────

    public Vector3   GetGripPoint()      => gripPoint;
    public LayerMask GetClimbableLayer() => climbableLayer;
    public void      SetRestPosition(Vector3 localPos) => _restLocal = localPos;

    // ── Дебаг ─────────────────────────────────────────────────

    private void LogGripFailReason(Ray ray)
    {
        RaycastHit hitAny;
        if (Physics.SphereCast(ray, gripRadius, out hitAny, reachDistance))
            Debug.LogWarning($"[{handSide}] Нет захвата — объект '{hitAny.collider.name}' на слое '{LayerMask.LayerToName(hitAny.collider.gameObject.layer)}', нужен 'Climbable'");
        else
            Debug.LogWarning($"[{handSide}] Нет захвата — ничего не найдено в зоне досягаемости");
    }

    private void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            :  playerCamera.transform.right * 0.3f;

        Vector3 start = playerCamera.transform.position + offset;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, start + playerCamera.transform.forward * reachDistance);
        Gizmos.DrawWireSphere(start + playerCamera.transform.forward * reachDistance, gripRadius);

        Gizmos.color = IsGripped ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(playerCamera.transform.TransformPoint(_restLocal), 0.08f);

        if (IsGripped)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(gripPoint, 0.12f);
        }
    }
}