using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Толкает кнопку влево и меняет цвет текста при наведении.
/// Вешать на каждую кнопку.
/// </summary>
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float offsetX  = -20f;
    [SerializeField] private float duration = 0.15f;

    [Header("Text Color")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = Color.gray;
    [SerializeField] private Color hoverColor  = Color.white;

    private RectTransform rect;
    private Vector2       originalPosition;
    private Coroutine     moveCoroutine;
    private Coroutine     colorCoroutine;

    private void Awake()
    {
        rect             = GetComponent<RectTransform>();
        originalPosition = rect.anchoredPosition;

        // Пробуем найти текст автоматически если не назначен
        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        if (label != null)
            label.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Animate(originalPosition.x + offsetX);
        AnimateColor(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Animate(originalPosition.x);
        AnimateColor(normalColor);
    }

    private void Animate(float targetX)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveToX(targetX));
    }

    private void AnimateColor(Color target)
    {
        if (colorCoroutine != null) StopCoroutine(colorCoroutine);
        colorCoroutine = StartCoroutine(LerpColor(target));
    }

    private IEnumerator MoveToX(float targetX)
    {
        float startX  = rect.anchoredPosition.x;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rect.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, t), rect.anchoredPosition.y);
            yield return null;
        }

        rect.anchoredPosition = new Vector2(targetX, rect.anchoredPosition.y);
    }

    private IEnumerator LerpColor(Color target)
    {
        if (label == null) yield break;

        Color start   = label.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed    += Time.unscaledDeltaTime;
            float t     = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            label.color = Color.Lerp(start, target, t);
            yield return null;
        }

        label.color = target;
    }
}