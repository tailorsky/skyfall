using UnityEngine;

/// <summary>
/// Маркер для поверхностей, за которые можно зацепиться.
/// Также управляет точками захвата и бонусами.
/// </summary>
public class RockSurface : MonoBehaviour
{
    [Header("Surface Type")]
    [SerializeField] private SurfaceType surfaceType = SurfaceType.Normal;
    [SerializeField] private float gripDifficulty = 1f; // 1 = нормально, >1 = сложнее

    [Header("Special Zones")]
    [SerializeField] private bool isRestZone = false;     // Восстанавливает стамину быстрее
    [SerializeField] private bool isDangerZone = false;   // Тратит стамину быстрее
    [SerializeField] private float staminaModifier = 1.5f;

    public enum SurfaceType
    {
        Normal,     // Обычная скала
        Smooth,     // Гладкая - труднее держаться
        Rough,      // Грубая - легче держаться
        Wet,        // Мокрая - очень скользко
        Crack,      // Трещина - отличная точка захвата
        Ledge       // Уступ - самая надёжная поверхность
    }

    public SurfaceType Surface => surfaceType;
    public bool IsRestZone => isRestZone;
    public bool IsDangerZone => isDangerZone;
    public float GripDifficulty => gripDifficulty;
    public float StaminaModifier => staminaModifier;

    // Для визуализации
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.3f);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}