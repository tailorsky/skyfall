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

    [Header("Hand Animation Positions")]
    [SerializeField] private Vector3 grippedLocalPosition = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 releasedLocalPosition = new Vector3(0, -0.2f, 0);
    [SerializeField] private float handMoveSpeed = 8f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer handRenderer;
    [SerializeField] private Material grippedMaterial;
    [SerializeField] private Material releasedMaterial;

    // Состояние
    public bool IsGripped { get; private set; }
    public bool IsReaching { get; private set; }
    public HandSide Side => handSide;

    private Vector3 gripPoint;
    private Vector3 targetLocalPosition;

    // События
    public event Action<HandController> OnGrip;
    public event Action<HandController> OnRelease;
    public event Action<HandController> OnGripFailed;

    private float gripCooldown = 0.2f;
    private float lastGripTime = -1f;

    // Камера — ищем сами надёжным способом
    private Camera playerCamera;

    private void Awake()
    {
        targetLocalPosition = releasedLocalPosition;
    }

    private void Start()
    {
        // Start надёжнее чем Awake для поиска камеры
        // потому что к этому моменту все объекты уже созданы
        FindCamera();
    }

    private void FindCamera()
    {
        // Способ 1 — ищем по тегу
        playerCamera = Camera.main;

        // Способ 2 — если не нашли по тегу, ищем в родителях
        if (playerCamera == null)
        {
            playerCamera = GetComponentInParent<Camera>();
            Debug.Log($"[{handSide}] Камера найдена через GetComponentInParent");
        }

        // Способ 3 — ищем вообще любую камеру на сцене
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
            Debug.Log($"[{handSide}] Камера найдена через FindObjectOfType");
        }

        // Итог
        if (playerCamera != null)
        {
            Debug.Log($"[{handSide}] Камера успешно найдена: {playerCamera.gameObject.name}");
        }
        else
        {
            Debug.LogError($"[{handSide}] КАМЕРА НЕ НАЙДЕНА ВООБЩЕ! " +
                           $"Проверь что в сцене есть объект с компонентом Camera");
        }
    }

    private void Update()
    {
        // Если камера пропала — пробуем найти снова
        if (playerCamera == null)
        {
            FindCamera();
            return;
        }

        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        // Анимируем движение руки
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetLocalPosition,
            Time.deltaTime * handMoveSpeed
        );
    }

    public bool TryGrip()
    {
        if (IsGripped) return true;
        if (Time.time - lastGripTime < gripCooldown) return false;

        // Проверяем камеру перед использованием
        if (playerCamera == null)
        {
            Debug.LogError($"[{handSide}] TryGrip: камера null! Пробуем найти...");
            FindCamera();

            // Если всё равно не нашли — выходим
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

        // Отладка — рисуем луч в Scene View
        Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.red, 1f);
        Debug.Log($"[{handSide}] Луч из {ray.origin} вперёд на {reachDistance}м");

        RaycastHit hit;
        if (Physics.SphereCast(ray, gripRadius, out hit, reachDistance, climbableLayer))
        {
            // Нашли поверхность
            gripPoint = hit.point;
            IsGripped = true;
            IsReaching = false;
            targetLocalPosition = grippedLocalPosition;

            if (handRenderer != null && grippedMaterial != null)
                handRenderer.material = grippedMaterial;

            OnGrip?.Invoke(this);
            Debug.Log($"[{handSide}] ЗАХВАТ! Объект: {hit.collider.gameObject.name}");
            return true;
        }
        else
        {
            // Промах — выясняем почему
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
                Debug.LogWarning($"[{handSide}] ПРОМАХ. Ничего нет перед камерой " +
                    $"на расстоянии {reachDistance}м. Скала слишком далеко?");
            }

            return false;
        }
    }

    public void Release()
    {
        if (!IsGripped) return;

        IsGripped = false;
        targetLocalPosition = releasedLocalPosition;

        if (handRenderer != null && releasedMaterial != null)
            handRenderer.material = releasedMaterial;

        OnRelease?.Invoke(this);
        Debug.Log($"[{handSide}] Рука отпущена");
    }

    public Vector3 GetGripPoint() => gripPoint;

    private void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Gizmos.color = IsGripped ? Color.green : Color.red;

        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            : playerCamera.transform.right * 0.3f;

        Vector3 start = playerCamera.transform.position + offset;
        Gizmos.DrawLine(start, start + playerCamera.transform.forward * reachDistance);
        Gizmos.DrawWireSphere(
            start + playerCamera.transform.forward * reachDistance,
            gripRadius
        );

        if (IsGripped)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(gripPoint, 0.1f);
        }
    }
    public LayerMask GetClimbableLayer()
    {
        return climbableLayer;
    }
}