using UnityEngine;

/// <summary>
/// Зона победы — ставится как триггер в месте финиша
/// </summary>
[RequireComponent(typeof(Collider))]
public class WinZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showDebugGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.3f);

    [Header("Optional Effects")]
    [SerializeField] private ParticleSystem winParticles;
    [SerializeField] private AudioClip winSound;

    private Collider zoneCollider;
    private bool triggered = false;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        // Проверяем что это игрок
        if (other.CompareTag("Player") || other.GetComponent<ClimbingManager>() != null)
        {
            triggered = true;
            OnPlayerWin(other.gameObject);
        }
    }

    private void OnPlayerWin(GameObject player)
    {
        Debug.Log("🏆 Игрок достиг зоны победы!");

        // Эффекты
        if (winParticles != null)
        {
            winParticles.Play();
        }

        if (winSound != null)
        {
            AudioSource.PlayClipAtPoint(winSound, transform.position);
        }

        // Сообщаем GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerWin();
        }
        else
        {
            Debug.LogWarning("GameManager не найден!");
        }
    }

    // Сброс при рестарте
    public void ResetZone()
    {
        triggered = false;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmo) return;

        Gizmos.color = gizmoColor;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
        }
    }
}