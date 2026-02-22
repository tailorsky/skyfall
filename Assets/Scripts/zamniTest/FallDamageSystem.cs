using UnityEngine;

/// <summary>
/// Система урона от падения
/// </summary>
public class FallDamageSystem : MonoBehaviour
{
    [Header("Fall Settings")]
    [SerializeField] private float lethalFallDistance = 2.5f;      // Смертельная высота
    [SerializeField] private float safeFallDistance = 1.0f;        // Безопасная высота (без эффектов)

    [Header("Recovery")]
    [SerializeField] private float stunDuration = 0.5f;            // Оглушение при несмертельном падении
    [SerializeField] private bool canMoveWhileStunned = false;

    [Header("Effects")]
    [SerializeField] private AudioClip landingSoftSound;
    [SerializeField] private AudioClip landingHardSound;
    [SerializeField] private AudioClip deathFallSound;
    [SerializeField] private ParticleSystem landingDustParticles;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // Состояние
    private bool isTracking = false;          // Отслеживаем падение
    private float fallStartY = 0f;            // Высота начала падения
    private float highestY = 0f;              // Максимальная высота (для прыжков)
    private bool wasGrounded = true;          // Был ли на земле в прошлом кадре
    private bool isStunned = false;           // Оглушён после падения
    private float stunTimer = 0f;

    // Компоненты
    private ClimbingManager climbingManager;
    private CharacterController characterController;
    private AudioSource audioSource;

    // События
    public System.Action OnLethalFall;        // Смертельное падение
    public System.Action OnHardLanding;       // Жёсткое приземление
    public System.Action OnSoftLanding;       // Мягкое приземление

    public bool IsStunned => isStunned;
    public float CurrentFallDistance => isTracking ? (highestY - transform.position.y) : 0f;

    private void Awake()
    {
        climbingManager = GetComponent<ClimbingManager>();
        characterController = GetComponent<CharacterController>();

        // Создаём AudioSource если нужен звук
        if (landingSoftSound != null || landingHardSound != null || deathFallSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D звук
        }
    }

    private void Update()
    {
        if (climbingManager == null) return;

        bool isGrounded = climbingManager.IsGrounded;

        // Обновляем таймер оглушения
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                if (showDebugInfo) Debug.Log("Оглушение прошло");
            }
        }

        // Если лазаем — не трогаем систему падения
        // Она запустится через StartClimbingFall когда отпустим руки
        if (climbingManager.CurrentState == ClimbingManager.PlayerState.Climbing)
        {
            wasGrounded = false;
            return;
        }

        // Если падаем после лазания — ждём приземления
        if (climbingManager.CurrentState == ClimbingManager.PlayerState.Falling)
        {
            if (isGrounded && isTracking)
            {
                // Приземлились после падения с лазания
                ProcessClimbingLanding(highestY, transform.position.y);
            }
            wasGrounded = isGrounded;
            return;
        }

        // Режим ходьбы — отслеживаем обычные падения (прыжки, оступился)
        if (wasGrounded && !isGrounded)
        {
            // Оторвались от земли
            StartTrackingFall();
        }

        if (!isGrounded && isTracking)
        {
            // Обновляем максимальную высоту
            if (transform.position.y > highestY)
                highestY = transform.position.y;
        }

        if (!wasGrounded && isGrounded && isTracking)
        {
            // Приземлились при обычном падении
            ProcessLanding();
        }

        wasGrounded = isGrounded;
    }

    public void StartClimbingFall(float startHeight)
    {
        // startHeight = Y в момент когда отпустили руки
        isTracking = true;
        fallStartY = startHeight;
        highestY = startHeight; // максимальная высота = точка отпускания

        if (showDebugInfo)
            Debug.Log($"Отпустил руки на высоте Y={startHeight:F2}. Ждём приземления...");
    }

    private void ProcessLanding()
    {
        float fallDistance = highestY - transform.position.y;

        if (showDebugInfo)
        {
            Debug.Log($"📍 Приземление! Падение: {fallDistance:F2}м " +
                      $"(старт: {highestY:F2}, конец: {transform.position.y:F2})");
        }

        // ═══════════════════════════════════════════════════
        // Смертельное падение
        // ═══════════════════════════════════════════════════
        if (fallDistance >= lethalFallDistance)
        {
            HandleLethalFall(fallDistance);
        }
        // ═══════════════════════════════════════════════════
        // Жёсткое приземление (между safe и lethal)
        // ═══════════════════════════════════════════════════
        else if (fallDistance > safeFallDistance)
        {
            HandleHardLanding(fallDistance);
        }
        // ═══════════════════════════════════════════════════
        // Мягкое приземление
        // ═══════════════════════════════════════════════════
        else
        {
            HandleSoftLanding(fallDistance);
        }

        ResetTracking();
    }

    private void HandleLethalFall(float distance)
    {
        Debug.Log($"💀 СМЕРТЕЛЬНОЕ ПАДЕНИЕ! Высота: {distance:F2}м");

        // Звук
        PlaySound(deathFallSound);

        // Частицы
        if (landingDustParticles != null)
        {
            landingDustParticles.Play();
        }

        // Событие
        OnLethalFall?.Invoke();

        // Сообщаем GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
    }

    private void HandleHardLanding(float distance)
    {
        Debug.Log($"😵 Жёсткое приземление! Высота: {distance:F2}м");

        // Звук
        PlaySound(landingHardSound);

        // Частицы
        if (landingDustParticles != null)
        {
            landingDustParticles.Play();
        }

        // Оглушение
        isStunned = true;
        stunTimer = stunDuration;

        // Событие
        OnHardLanding?.Invoke();
    }

    private void HandleSoftLanding(float distance)
    {
        if (showDebugInfo && distance > 0.1f)
        {
            Debug.Log($"✓ Мягкое приземление. Высота: {distance:F2}м");
        }

        // Звук только если падали хоть немного
        if (distance > 0.3f)
        {
            PlaySound(landingSoftSound);
        }

        // Событие
        OnSoftLanding?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void ResetTracking()
    {
        isTracking = false;
        fallStartY = 0f;
        highestY = 0f;
    }

    /// <summary>
    /// Вызывается когда отпускаем руки при лазании — начинаем отслеживать падение
    /// </summary>
    
    /// <summary>
    /// Вызывается когда игрок упал с лазания и приземлился
    /// </summary>
    public void ProcessClimbingLanding(float startY, float endY)
    {
        float fallDistance = startY - endY;

        if (showDebugInfo)
            Debug.Log($"Приземление после лазания! " +
                      $"С Y={startY:F2} до Y={endY:F2} = {fallDistance:F2}м");

        // Если как-то поднялись — игнорируем
        if (fallDistance <= 0f)
        {
            ResetTracking();
            return;
        }

        // Обрабатываем урон
        if (fallDistance >= lethalFallDistance)
            HandleLethalFall(fallDistance);
        else if (fallDistance > safeFallDistance)
            HandleHardLanding(fallDistance);
        else
            HandleSoftLanding(fallDistance);

        ResetTracking();
    }

    private void StartTrackingFall()
    {
        isTracking = true;
        fallStartY = transform.position.y;
        highestY = transform.position.y;

        if (showDebugInfo)
            Debug.Log($"Начало падения с Y={fallStartY:F2}");
    }

    /// <summary>
    /// Проверка можно ли двигаться (для интеграции с ClimbingManager)
    /// </summary>
    public bool CanMove()
    {
        if (!isStunned) return true;
        return canMoveWhileStunned;
    }

    // Сброс при рестарте игры
    public void ResetSystem()
    {
        ResetTracking();
        isStunned = false;
        stunTimer = 0f;
    }
}