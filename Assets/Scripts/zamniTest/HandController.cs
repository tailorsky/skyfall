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

    [Header("Hand Position (������������ ������)")]
    [SerializeField] private Vector3 restLocalPosition = new Vector3(0.3f, -0.3f, 0.6f);
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float returnSpeed = 8f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer handRenderer;
    [SerializeField] private Material grippedMaterial;
    [SerializeField] private Material releasedMaterial;

    public bool IsGripped { get; private set; }
    public bool IsReaching { get; private set; }
    public HandSide Side => handSide;

    private Vector3 gripPoint;
    private Vector3 gripWorldPosition;

    public event Action<HandController> OnGrip;
    public event Action<HandController> OnRelease;
    public event Action<HandController> OnGripFailed;

    private float gripCooldown = 0.2f;
    private float lastGripTime = -1f;
    [Header ("ЗАЦЕП")]
    [SerializeField] private Transform gripAnchor;

    [Header ("Anim")]
    [SerializeField] private Animator animator;

    private Camera playerCamera;
    private Transform cameraTransform;
    private Quaternion initialLocalRotation;
    private void Awake()
    {
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

        initialLocalRotation = transform.localRotation;

        if (cameraTransform != null)
        {
            transform.position = GetRestWorldPosition();
        }
    }

    private void FindCamera()
    {
        playerCamera = Camera.main;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInParent<Camera>();
        }

        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        if (playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
            Debug.Log($"[{handSide}] ������ �������: {playerCamera.gameObject.name}");
        }
        else
        {
            Debug.LogError($"[{handSide}] ������ �� �������!");
        }
    }

    private void Update()
    {
        if (playerCamera == null || cameraTransform == null)
        {
            FindCamera();
            return;
        }

        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        if (IsGripped)
        {
            StayAtGripPoint();
        }
        else
        {
            FollowCamera();
        }
    }

    private void LateUpdate()
    {
        if (!IsGripped && cameraTransform != null)
        {
        }
    }

    /// <summary>
    /// круто3
    /// </summary>
    private void FollowCamera()
    {
        Vector3 targetWorldPos = GetRestWorldPosition();

        transform.position = Vector3.Lerp(
            transform.position,
            targetWorldPos,
            Time.deltaTime * followSpeed
        );

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            cameraTransform.rotation * initialLocalRotation,
            Time.deltaTime * followSpeed
        );
    }

    /// <summary>
    /// круто1
    /// </summary>
    private Vector3 GetRestWorldPosition()
    {
        return cameraTransform.TransformPoint(restLocalPosition);
    }

    /// <summary>
    /// круто
    /// </summary>
    private void StayAtGripPoint()
    {
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
                Debug.LogError($"[{handSide}] ������ �� �������, ������ ����������!");
                return false;
            }
        }

        lastGripTime = Time.time;
        IsReaching = true;
        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            : playerCamera.transform.right * 0.3f;

        Ray ray = new Ray(
            playerCamera.transform.position + offset,
            playerCamera.transform.forward
        );
        Debug.DrawRay(ray.origin, ray.direction * reachDistance, Color.red, 1f);

        RaycastHit hit;
        if (Physics.SphereCast(ray, gripRadius, out hit, reachDistance, climbableLayer))
        {
            gripPoint = hit.point;
            Vector3 anchorOffset = transform.position - gripAnchor.position;
            gripWorldPosition = hit.point + anchorOffset;
            IsGripped = true;
            IsReaching = false;

            if (animator != null)
                animator.SetBool("IsGripped", true);

            if (handRenderer != null && grippedMaterial != null)
                handRenderer.material = grippedMaterial;

            OnGrip?.Invoke(this);
            Debug.Log($"[{handSide}] ������! ������: {hit.collider.gameObject.name}");
            return true;
        }
        else
        {
            IsReaching = false;
            OnGripFailed?.Invoke(this);

            RaycastHit hitAny;
            if (Physics.SphereCast(ray, gripRadius, out hitAny, reachDistance))
            {
                Debug.LogWarning($"[{handSide}] ������ �� �����. " +
                    $"����� '{hitAny.collider.gameObject.name}' " +
                    $"�� ���� '{LayerMask.LayerToName(hitAny.collider.gameObject.layer)}'. " +
                    $"����� ���� 'Climbable'!");
            }
            else
            {
                Debug.LogWarning($"[{handSide}] ������. ������ ��� ����� �������.");
            }

            return false;
        }
    }

    public void Release()
    {
        if (!IsGripped) return;

        IsGripped = false;

        if (animator != null)
            animator.SetBool("IsGripped", false);

        if (handRenderer != null && releasedMaterial != null)
            handRenderer.material = releasedMaterial;

        OnRelease?.Invoke(this);
        Debug.Log($"[{handSide}] ���� ��������");
    }

    public Vector3 GetGripPoint() => gripPoint;

    public LayerMask GetClimbableLayer() => climbableLayer;

    /// <summary>
    /// круто34
    /// </summary>
    public void SetRestPosition(Vector3 localPos)
    {
        restLocalPosition = localPos;
    }

    private void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Gizmos.color = IsGripped ? Color.green : Color.yellow;

        Vector3 restPos = playerCamera.transform.TransformPoint(restLocalPosition);
        Gizmos.DrawWireSphere(restPos, 0.1f);

        Vector3 offset = handSide == HandSide.Left
            ? -playerCamera.transform.right * 0.3f
            : playerCamera.transform.right * 0.3f;

        Vector3 start = playerCamera.transform.position + offset;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, start + playerCamera.transform.forward * reachDistance);
        Gizmos.DrawWireSphere(start + playerCamera.transform.forward * reachDistance, gripRadius);

        if (IsGripped)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(gripPoint, 0.15f);
        }
    }
}