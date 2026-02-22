using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI элемент дл€ одного бинда клавиши
/// </summary>
public class KeyBindingUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI actionNameText;
    [SerializeField] private Button primaryKeyButton;
    [SerializeField] private Button secondaryKeyButton;
    [SerializeField] private TextMeshProUGUI primaryKeyText;
    [SerializeField] private TextMeshProUGUI secondaryKeyText;

    private KeyBindingsManager.KeyBinding binding;
    private SettingsUI settingsUI;

    public void Setup(KeyBindingsManager.KeyBinding binding, SettingsUI ui)
    {
        this.binding = binding;
        this.settingsUI = ui;

        if (actionNameText != null)
            actionNameText.text = binding.actionName;

        primaryKeyButton?.onClick.AddListener(() =>
        {
            settingsUI.StartRebind(binding.actionId, primaryKeyButton);
        });

        secondaryKeyButton?.onClick.AddListener(() =>
        {
            // ћожно добавить ребинд вторичной клавиши
        });

        Refresh();
    }

    public void Refresh()
    {
        if (binding == null) return;

        var keys = SettingsManager.Instance?.Keys;
        if (keys == null) return;

        if (primaryKeyText != null)
            primaryKeyText.text = keys.GetKeyDisplayName(binding.actionId, true);

        if (secondaryKeyText != null)
            secondaryKeyText.text = keys.GetKeyDisplayName(binding.actionId, false);
    }
}