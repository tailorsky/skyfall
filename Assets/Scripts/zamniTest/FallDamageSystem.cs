using UnityEngine;

/// <summary>
/// Отвечает за отслеживание высоты падения и применение его последствий.
/// Не зависит от ClimbingManager — получает события через публичные методы.
/// </summary>
public class FallDamageSystem : MonoBehaviour
{
    [Header("Fall Thresholds")]
    [SerializeField] private float lethalFallDistance = 2.5f;
    [SerializeField] private float safeFallDistance   = 1.0f;

    [Header("Recovery")]
    [SerializeField] private float stunDuration       = 0.5f;
    [SerializeField] private bool  canMoveWhileStunned = false;

    [Header("Effects")]
    [SerializeField] private AudioClip        landingSoftSound;
    [SerializeField] private AudioClip        landingHardSound;
    [SerializeField] private AudioClip        deathFallSound;
    [SerializeField] private ParticleSystem   landingDustParticles;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // ── Публичное состояние ───────────────────────────────────
    public bool  IsStunned           => isStunned;
    public float CurrentFallDistance => isTracking ? Mathf.Max(0f, highestY - transform.position.y) : 0f;

    // ── События (подписываются GameManager, UI, аниматор и т.д.) ─
    public event System.Action             OnLethalFall;
    public event System.Action             OnHardLanding;
    public event System.Action<float>      OnSoftLanding;   // передаёт дистанцию

    // ── Приватное ─────────────────────────────────────────────
    private AudioSource audioSource;

    private bool  isTracking = false;
    private float highestY   = 0f;

    private bool  isStunned  = false;
    private float stunTimer  = 0f;

    // ── Нормальное падение (прыжок, оступился) ────────────────
    // Внешний код должен сообщать о состоянии земли через Tick()
    private bool wasGrounded = true;

    private void Awake()
    {
        bool needsAudio = landingSoftSound != null || landingHardSound != null || deathFallSound != null;
        if (needsAudio)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
        }
    }

    // ── Вызывается извне (ClimbingManager / PlayerWalking) ────

    /// <summary>
    /// Тик для обычного падения (ходьба/прыжок). Передавать isGrounded каждый кадр.
    /// </summary>
    public void TickWalking(bool isGrounded)
    {
        TickStun();

        if (wasGrounded && !isGrounded)
        {
            BeginTracking(transform.position.y);
        }

        if (!isGrounded && isTracking && transform.position.y > highestY)
        {
            highestY = transform.position.y;
        }

        if (!wasGrounded && isGrounded && isTracking)
        {
            ProcessLanding(highestY, transform.position.y);
        }

        wasGrounded = isGrounded;
    }

    /// <summary>
    /// Вызывается когда игрок отпустил руки при лазании.
    /// </summary>
    public void StartClimbingFall(float startY)
    {
        BeginTracking(startY);
        if (showDebugInfo)
            Debug.Log($"[FallDamage] Падение с лазания, старт Y={startY:F2}");
    }

    /// <summary>
    /// Вызывается когда игрок приземлился после падения с лазания.
    /// </summary>
    public void ProcessClimbingLanding(float startY, float endY)
    {
        if (!isTracking) return;
        ProcessLanding(startY, endY);
    }

    /// <summary>
    /// Можно ли игроку двигаться прямо сейчас.
    /// </summary>
    public bool CanMove() => !isStunned || canMoveWhileStunned;

    /// <summary>
    /// Сброс при рестарте.
    /// </summary>
    public void ResetSystem()
    {
        isTracking = false;
        highestY   = 0f;
        isStunned  = false;
        stunTimer  = 0f;
    }

    // ── Приватные ─────────────────────────────────────────────

    private void BeginTracking(float startY)
    {
        isTracking = true;
        highestY   = startY;
    }

    private void ProcessLanding(float startY, float endY)
    {
        float dist = startY - endY;

        if (showDebugInfo)
            Debug.Log($"[FallDamage] Приземление: {dist:F2}м (с {startY:F2} до {endY:F2})");

        if (dist <= 0f)
        {
            // Поднялись — игнорируем
        }
        else if (dist >= lethalFallDistance)
        {
            HandleLethalFall(dist);
        }
        else if (dist > safeFallDistance)
        {
            HandleHardLanding(dist);
        }
        else
        {
            HandleSoftLanding(dist);
        }

        isTracking = false;
        highestY   = 0f;
    }

    private void HandleLethalFall(float dist)
    {
        Debug.Log($"[FallDamage] 💀 Смертельное падение {dist:F2}м");
        PlaySound(deathFallSound);
        PlayParticles();
        OnLethalFall?.Invoke();
        // GameManager подписывается на OnLethalFall сам — мы о нём не знаем
    }

    private void HandleHardLanding(float dist)
    {
        Debug.Log($"[FallDamage] 😵 Жёсткое приземление {dist:F2}м");
        PlaySound(landingHardSound);
        PlayParticles();

        isStunned = true;
        stunTimer = stunDuration;

        OnHardLanding?.Invoke();
    }

    private void HandleSoftLanding(float dist)
    {
        if (showDebugInfo && dist > 0.1f)
            Debug.Log($"[FallDamage] ✓ Мягкое приземление {dist:F2}м");

        if (dist > 0.3f) PlaySound(landingSoftSound);

        OnSoftLanding?.Invoke(dist);
    }

    private void TickStun()
    {
        if (!isStunned) return;
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            isStunned = false;
            if (showDebugInfo) Debug.Log("[FallDamage] Оглушение прошло");
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    private void PlayParticles()
    {
        if (landingDustParticles != null)
            landingDustParticles.Play();
    }
}