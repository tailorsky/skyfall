using UnityEngine;

/// <summary>
/// Отвечает за физику падения, тряску камеры и эффект FOV при падении.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FallController : MonoBehaviour
{
    [Header("Fall Camera Shake")]
    [SerializeField] private float shakeFrequency  = 25f;
    [SerializeField] private float shakeAmplitude  = 0.08f;
    [SerializeField] private float shakeRampUpSpeed = 2f;

    [Header("Fall FOV")]
    [SerializeField] private float normalFOV    = 60f;
    [SerializeField] private float maxFallFOV   = 80f;
    [SerializeField] private float fovChangeSpeed = 3f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    // ── публичное состояние ───────────────────────────────────
    public bool IsGroundedAfterFall { get; private set; }

    // ── приватное ─────────────────────────────────────────────
    private CharacterController cc;
    private FallDamageSystem    fallDamageSystem;

    private Vector3 velocity;
    private float   fallTime;
    private float   fallStartY;

    private Vector3 cameraOriginalLocalPos;
    private bool    isCameraShaking;
    private float   currentShakeIntensity;

    private void Awake()
    {
        cc               = GetComponent<CharacterController>();
        fallDamageSystem = GetComponent<FallDamageSystem>();
    }

    /// <summary>Вызывается когда начинается падение.</summary>
    public void BeginFall(float startY)
    {
        fallStartY            = startY;
        fallTime              = 0f;
        velocity              = Vector3.zero;
        IsGroundedAfterFall   = false;
    }

    /// <summary>Тик физики падения. Вызывать пока в состоянии Falling.</summary>
    public void Tick(float gravity, bool isGrounded)
    {
        UpdateFOV(true);
        fallTime += Time.deltaTime;
        ApplyCameraShake(fallTime);

        velocity.y += gravity * Time.deltaTime;
        velocity.x  = 0f;
        velocity.z  = 0f;

        cc.Move(velocity * Time.deltaTime);

        if (isGrounded)
        {
            StopCameraShake();
            fallDamageSystem?.ProcessClimbingLanding(fallStartY, transform.position.y);
            IsGroundedAfterFall = true;
        }
    }

    /// <summary>Сбрасывает FOV к нормальному значению.</summary>
    public void UpdateFOVNormal() => UpdateFOV(false);

    public void StopEffects()
    {
        StopCameraShake();
        UpdateFOV(false);
    }

    // ── FOV ───────────────────────────────────────────────────
    private void UpdateFOV(bool falling)
    {
        if (playerCamera == null) return;
        float target = falling ? maxFallFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, Time.deltaTime * fovChangeSpeed);
    }

    // ── Camera Shake ─────────────────────────────────────────
    private void ApplyCameraShake(float timeInFall)
    {
        if (playerCamera == null) return;

        if (!isCameraShaking)
        {
            cameraOriginalLocalPos = playerCamera.transform.localPosition;
            isCameraShaking        = true;
        }

        currentShakeIntensity = Mathf.Min(
            timeInFall * shakeRampUpSpeed * shakeAmplitude,
            shakeAmplitude
        );

        float sx = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f * currentShakeIntensity;
        float sy = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f * currentShakeIntensity;

        playerCamera.transform.localPosition = cameraOriginalLocalPos + new Vector3(sx, sy, 0f);
    }

    private void StopCameraShake()
    {
        if (playerCamera == null) return;
        isCameraShaking        = false;
        currentShakeIntensity  = 0f;
        playerCamera.transform.localPosition = cameraOriginalLocalPos;
    }
}