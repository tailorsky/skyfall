using UnityEngine;
using System;

public class HandController : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Hand Settings")]
    [SerializeField] private HandSide handSide;
    [SerializeField] private float reachDistance = 3f;
    [SerializeField] private float gripRadius = 0.5f;
    [SerializeField] private LayerMask climbableLayer;

    [Header("Hand Position (относительно камеры)")]
    [SerializeField] private Vector3 restLocalPosition = new Vector3(0.3f, -0.3f, 0.6f);
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float returnSpeed = 8f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer handRenderer;
    [SerializeField] private Material grippedMaterial;
    [SerializeField] private Material releasedMaterial;

    // Состояние
    public bool IsGripped { get; private set; }
    public bool IsReaching { get; private set; }
    public HandSide Side => handSide;

    private Vector3 gripPoint;
    private Vector3 gripWorldPosition;

    // События
    public event Action<HandController> OnGrip;
    public event Action<HandController> OnRelease;
    public event Action<HandController> OnGripFailed;

    private float gripCooldown = 0.2f;
    private float lastGripTime = -1f;

    // Камера
    private Camera playerCamera;
    private Transform cameraTransform;

    private void Awake()
    {
        // Устанавливаем правильную позицию для левой/правой руки
        if (handSide == HandSide.Left)
        {
            restLocalPosition = new Vector3(-0.4f, -0.3f, 0.6f);
        }
        else
        {
            restLocalPosition = new Vector3(0.4f, -0.3f, 0.6f);
        }
    }

    private void Start()
    {
        FindCamera();

        // Сразу ставим руку на место
        if (cameraTransform != null)
        {
            transform.position = GetRestWorldPosition();
        }
    }

    private void FindCamera()
    {
        // Способ 1 — ищем по тегу
        playerCamera = Camera.main;

        // Способ 2 — если не нашли по тегу, ищем в родителях
        if (playerCamera == null)
        {
            playerCamera = GetComponentInParent<Camera>();
        }

        // Способ 3 — ищем вообще любую камеру на сцене
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        if (playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
            Debug.Log($"[{handSide}] Камера найдена: {playerCamera.gameObject.name}");
        }
        else
        {
            Debug.LogError($"[{handSide}] КАМЕРА НЕ НАЙДЕНА!");
        }
    }

    private void Update()
    {
        // Если камера пропала — пробуем найти снова
        if (playerCamera == null || cameraTransform == null)
        {
            FindCamera();
            return;
        }

        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        if (IsGripped)
        {
            // Рука остаётся в точке захвата (мировые координаты)
            StayAtGripPoint();
        }
        else
        {
            // Рука следует за камерой
            FollowCamera();
        }
    }

    private void LateUpdate()
    {
        // LateUpdate для более плавного следования за камерой
        if (!IsGripped && cameraTransform != null)
        {
            // Дополнительное сглаживание после всех Update
        }
    }

    /// <summary>
    /// Рука следует за камерой (когда не зацеплена)
    /// </summary>
    private void FollowCamera()
    {
        Vector3 targetWorldPos = GetRestWorldPosition();

        transform.position = Vector3.Lerp(
            transform.position,
            targetWorldPos,
            Time.deltaTime * followSpeed
        );

        // Рука смотрит в том же направлении что и камера
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            cameraTransform.rotation,
            Time.deltaTime * followSpeed
        );
    }

    /// <summary>
    /// Получить мировую позицию руки "в покое" (перед камерой)
    /// </summary>
    private Vector3 GetRestWorldPosition()
    {
        // Конвертируем локальную позицию относительно камеры в мировую
        return cameraTransform.TransformPoint(restLocalPosition);
    }

    /// <summary>
    /// Рука остаётся в точке захвата (когда зацеплена)
    /// </summary>
    private void StayAtGripPoint()
    {
        // Плавно двигаемся к точке захвата (для красивой анимации)
        transform.position = Vector3.Lerp(
            transform.position,
            gripWorldPosition,
            Time.deltaTime * returnSpeed
        );
    }

    public bool TryGrip()
    {
        if (IsGripped) return true;
        if (Time.time - lastGripTime < gripCooldown) return false;

        if (playerCamera == null)
        {
            FindCamera();
            if (playerCamera == null)
            {
                Debug.LogError($"[{handSide}] Камера не найдена, захват невозможен!");
                return false;
            }
        }

        lastGripTime = Time.time;
        IsReaching = true;

        // Строим луч от камеры
        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            : playerCamera.transform.right * 0.3f;

        Ray ray = new Ray(
            playerCamera.transform.position + offset,
            playerCamera.transform.forward
        );

        // Отладка
        Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.red, 1f);

        RaycastHit hit;
        if (Physics.SphereCast(ray, gripRadius, out hit, reachDistance, climbableLayer))
        {
            // Нашли поверхность
            gripPoint = hit.point;
            gripWorldPosition = hit.point;
            IsGripped = true;
            IsReaching = false;

            if (handRenderer != null && grippedMaterial != null)
                handRenderer.material = grippedMaterial;

            OnGrip?.Invoke(this);
            Debug.Log($"[{handSide}] ЗАХВАТ! Объект: {hit.collider.gameObject.name}");
            return true;
        }
        else
        {
            IsReaching = false;
            OnGripFailed?.Invoke(this);

            // Проверяем без маски слоя
            RaycastHit hitAny;
            if (Physics.SphereCast(ray, gripRadius, out hitAny, reachDistance))
            {
                Debug.LogWarning($"[{handSide}] ПРОМАХ по маске. " +
                    $"Нашли '{hitAny.collider.gameObject.name}' " +
                    $"на слое '{LayerMask.LayerToName(hitAny.collider.gameObject.layer)}'. " +
                    $"Нужен слой 'Climbable'!");
            }
            else
            {
                Debug.LogWarning($"[{handSide}] ПРОМАХ. Ничего нет перед камерой.");
            }

            return false;
        }
    }

    public void Release()
    {
        if (!IsGripped) return;

        IsGripped = false;

        if (handRenderer != null && releasedMaterial != null)
            handRenderer.material = releasedMaterial;

        OnRelease?.Invoke(this);
        Debug.Log($"[{handSide}] Рука отпущена");
    }

    public Vector3 GetGripPoint() => gripPoint;

    public LayerMask GetClimbableLayer() => climbableLayer;

    /// <summary>
    /// Установить позицию руки относительно камеры
    /// </summary>
    public void SetRestPosition(Vector3 localPos)
    {
        restLocalPosition = localPos;
    }

    private void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Gizmos.color = IsGripped ? Color.green : Color.yellow;

        // Показываем позицию "в покое"
        Vector3 restPos = playerCamera.transform.TransformPoint(restLocalPosition);
        Gizmos.DrawWireSphere(restPos, 0.1f);

        // Показываем луч захвата
        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            : playerCamera.transform.right * 0.3f;

        Vector3 start = playerCamera.transform.position + offset;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, start + playerCamera.transform.forward * reachDistance);
        Gizmos.DrawWireSphere(start + playerCamera.transform.forward * reachDistance, gripRadius);

        // Показываем точку захвата
        if (IsGripped)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(gripPoint, 0.15f);
        }
    }
}