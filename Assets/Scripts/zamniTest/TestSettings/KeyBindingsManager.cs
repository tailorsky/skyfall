using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

/// <summary>
/// Система переназначения клавиш
/// </summary>
public class KeyBindingsManager : MonoBehaviour
{
    // Определение всех действий
    [Serializable]
    public class KeyBinding
    {
        public string actionName;           // Название для отображения
        public string actionId;             // Уникальный ID
        public Key primaryKey;              // Основная клавиша
        public Key secondaryKey;            // Альтернативная клавиша (опционально)
        public MouseButton mouseButton;     // Кнопка мыши (если есть)

        public KeyBinding(string name, string id, Key primary, Key secondary = Key.None, MouseButton mouse = MouseButton.None)
        {
            actionName = name;
            actionId = id;
            primaryKey = primary;
            secondaryKey = secondary;
            mouseButton = mouse;
        }
    }

    public enum MouseButton
    {
        None,
        Left,
        Right,
        Middle
    }

    [Header("Default Bindings")]
    [SerializeField] private List<KeyBinding> bindings = new List<KeyBinding>();

    // Текущие бинды (загруженные)
    private Dictionary<string, KeyBinding> currentBindings = new Dictionary<string, KeyBinding>();

    // События
    public event Action<string, Key> OnKeyRebound;
    public event Action OnBindingsLoaded;

    // Состояние ребинда
    private bool isRebinding = false;
    private string rebindingActionId = null;
    private bool rebindingPrimary = true;
    private Action<Key> onRebindComplete;

    // ─────────────────────────────────────────
    //  ИНИЦИАЛИЗАЦИЯ
    // ─────────────────────────────────────────

    private void Awake()
    {
        InitializeDefaultBindings();
    }

    private void InitializeDefaultBindings()
    {
        // Очищаем и добавляем дефолтные бинды
        bindings.Clear();

        // Движение
        bindings.Add(new KeyBinding("Вперёд", "move_forward", Key.W, Key.UpArrow));
        bindings.Add(new KeyBinding("Назад", "move_back", Key.S, Key.DownArrow));
        bindings.Add(new KeyBinding("Влево", "move_left", Key.A, Key.LeftArrow));
        bindings.Add(new KeyBinding("Вправо", "move_right", Key.D, Key.RightArrow));

        // Действия
        bindings.Add(new KeyBinding("Прыжок", "jump", Key.Space));
        bindings.Add(new KeyBinding("Левая рука", "grip_left", Key.Q, Key.None, MouseButton.Left));
        bindings.Add(new KeyBinding("Правая рука", "grip_right", Key.E, Key.None, MouseButton.Right));
        bindings.Add(new KeyBinding("Перелезть", "mantle", Key.Space, Key.W));

        // Система
        bindings.Add(new KeyBinding("Пауза", "pause", Key.Escape));
        bindings.Add(new KeyBinding("Быстрое сохранение", "quicksave", Key.F5));
        bindings.Add(new KeyBinding("Быстрая загрузка", "quickload", Key.F9));

        // Копируем в словарь
        foreach (var binding in bindings)
        {
            currentBindings[binding.actionId] = new KeyBinding(
                binding.actionName,
                binding.actionId,
                binding.primaryKey,
                binding.secondaryKey,
                binding.mouseButton
            );
        }
    }

    // ─────────────────────────────────────────
    //  ПРОВЕРКА НАЖАТИЯ
    // ─────────────────────────────────────────

    public bool IsActionPressed(string actionId)
    {
        if (!currentBindings.TryGetValue(actionId, out KeyBinding binding))
            return false;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null) return false;

        // Проверяем клавиши
        if (binding.primaryKey != Key.None && keyboard[binding.primaryKey].isPressed)
            return true;

        if (binding.secondaryKey != Key.None && keyboard[binding.secondaryKey].isPressed)
            return true;

        // Проверяем мышь
        if (mouse != null && binding.mouseButton != MouseButton.None)
        {
            switch (binding.mouseButton)
            {
                case MouseButton.Left: return mouse.leftButton.isPressed;
                case MouseButton.Right: return mouse.rightButton.isPressed;
                case MouseButton.Middle: return mouse.middleButton.isPressed;
            }
        }

        return false;
    }

    public bool IsActionJustPressed(string actionId)
    {
        if (!currentBindings.TryGetValue(actionId, out KeyBinding binding))
            return false;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null) return false;

        // Проверяем клавиши
        if (binding.primaryKey != Key.None && keyboard[binding.primaryKey].wasPressedThisFrame)
            return true;

        if (binding.secondaryKey != Key.None && keyboard[binding.secondaryKey].wasPressedThisFrame)
            return true;

        // Проверяем мышь
        if (mouse != null && binding.mouseButton != MouseButton.None)
        {
            switch (binding.mouseButton)
            {
                case MouseButton.Left: return mouse.leftButton.wasPressedThisFrame;
                case MouseButton.Right: return mouse.rightButton.wasPressedThisFrame;
                case MouseButton.Middle: return mouse.middleButton.wasPressedThisFrame;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────
    //  РЕБИНД
    // ─────────────────────────────────────────

    /// <summary>
    /// Начать процесс переназначения клавиши
    /// </summary>
    public void StartRebinding(string actionId, bool primary = true, Action<Key> onComplete = null)
    {
        if (isRebinding) return;

        isRebinding = true;
        rebindingActionId = actionId;
        rebindingPrimary = primary;
        onRebindComplete = onComplete;

        Debug.Log($"🎮 Ожидание нажатия клавиши для '{actionId}'...");
    }

    /// <summary>
    /// Отменить процесс переназначения
    /// </summary>
    public void CancelRebinding()
    {
        isRebinding = false;
        rebindingActionId = null;
        onRebindComplete = null;
    }

    private void Update()
    {
        if (!isRebinding) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Ищем нажатую клавишу
        foreach (Key key in Enum.GetValues(typeof(Key)))
        {
            if (key == Key.None) continue;

            try
            {
                if (keyboard[key].wasPressedThisFrame)
                {
                    // Escape отменяет ребинд
                    if (key == Key.Escape)
                    {
                        CancelRebinding();
                        return;
                    }

                    CompleteRebinding(key);
                    return;
                }
            }
            catch { /* Некоторые клавиши могут быть недоступны */ }
        }
    }

    private void CompleteRebinding(Key newKey)
    {
        if (!currentBindings.TryGetValue(rebindingActionId, out KeyBinding binding))
        {
            CancelRebinding();
            return;
        }

        // Проверяем на конфликты
        string conflict = CheckForConflict(newKey, rebindingActionId);
        if (!string.IsNullOrEmpty(conflict))
        {
            Debug.LogWarning($"⚠️ Клавиша уже используется для '{conflict}'");
            // Можно либо отменить, либо освободить конфликтующий бинд
            ClearConflictingBind(newKey, rebindingActionId);
        }

        // Назначаем новую клавишу
        if (rebindingPrimary)
            binding.primaryKey = newKey;
        else
            binding.secondaryKey = newKey;

        Debug.Log($"✅ Клавиша '{newKey}' назначена на '{binding.actionName}'");

        OnKeyRebound?.Invoke(rebindingActionId, newKey);
        onRebindComplete?.Invoke(newKey);

        isRebinding = false;
        rebindingActionId = null;
        onRebindComplete = null;
    }

    private string CheckForConflict(Key key, string excludeActionId)
    {
        foreach (var kvp in currentBindings)
        {
            if (kvp.Key == excludeActionId) continue;

            if (kvp.Value.primaryKey == key || kvp.Value.secondaryKey == key)
            {
                return kvp.Value.actionName;
            }
        }
        return null;
    }

    private void ClearConflictingBind(Key key, string excludeActionId)
    {
        foreach (var kvp in currentBindings)
        {
            if (kvp.Key == excludeActionId) continue;

            if (kvp.Value.primaryKey == key)
                kvp.Value.primaryKey = Key.None;

            if (kvp.Value.secondaryKey == key)
                kvp.Value.secondaryKey = Key.None;
        }
    }

    // ─────────────────────────────────────────
    //  ГЕТТЕРЫ
    // ─────────────────────────────────────────

    public KeyBinding GetBinding(string actionId)
    {
        return currentBindings.TryGetValue(actionId, out KeyBinding binding) ? binding : null;
    }

    public List<KeyBinding> GetAllBindings()
    {
        return new List<KeyBinding>(currentBindings.Values);
    }

    public string GetKeyDisplayName(string actionId, bool primary = true)
    {
        var binding = GetBinding(actionId);
        if (binding == null) return "???";

        Key key = primary ? binding.primaryKey : binding.secondaryKey;

        if (key == Key.None)
        {
            // Может есть кнопка мыши
            if (primary && binding.mouseButton != MouseButton.None)
            {
                return binding.mouseButton switch
                {
                    MouseButton.Left => "ЛКМ",
                    MouseButton.Right => "ПКМ",
                    MouseButton.Middle => "СКМ",
                    _ => ""
                };
            }
            return "-";
        }

        return GetKeyName(key);
    }

    private string GetKeyName(Key key)
    {
        // Человекочитаемые названия
        return key switch
        {
            Key.Space => "Пробел",
            Key.LeftShift => "L-Shift",
            Key.RightShift => "R-Shift",
            Key.LeftCtrl => "L-Ctrl",
            Key.RightCtrl => "R-Ctrl",
            Key.LeftAlt => "L-Alt",
            Key.RightAlt => "R-Alt",
            Key.Tab => "Tab",
            Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Backspace => "Backspace",
            Key.UpArrow => "↑",
            Key.DownArrow => "↓",
            Key.LeftArrow => "←",
            Key.RightArrow => "→",
            _ => key.ToString()
        };
    }

    public bool IsRebinding => isRebinding;
    public string RebindingActionId => rebindingActionId;

    // ─────────────────────────────────────────
    //  СОХРАНЕНИЕ / ЗАГРУЗКА
    // ─────────────────────────────────────────

    public void SaveBindings()
    {
        foreach (var kvp in currentBindings)
        {
            PlayerPrefs.SetInt($"Key_{kvp.Key}_Primary", (int)kvp.Value.primaryKey);
            PlayerPrefs.SetInt($"Key_{kvp.Key}_Secondary", (int)kvp.Value.secondaryKey);
            PlayerPrefs.SetInt($"Key_{kvp.Key}_Mouse", (int)kvp.Value.mouseButton);
        }
        PlayerPrefs.Save();
    }

    public void LoadBindings()
    {
        InitializeDefaultBindings();  // Сначала загружаем дефолты

        foreach (var kvp in currentBindings)
        {
            string id = kvp.Key;

            if (PlayerPrefs.HasKey($"Key_{id}_Primary"))
            {
                kvp.Value.primaryKey = (Key)PlayerPrefs.GetInt($"Key_{id}_Primary");
                kvp.Value.secondaryKey = (Key)PlayerPrefs.GetInt($"Key_{id}_Secondary");
                kvp.Value.mouseButton = (MouseButton)PlayerPrefs.GetInt($"Key_{id}_Mouse");
            }
        }

        OnBindingsLoaded?.Invoke();
    }

    public void ResetToDefaults()
    {
        InitializeDefaultBindings();
    }
}