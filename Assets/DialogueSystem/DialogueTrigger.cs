using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Диалог для этого уступа")]
    public DialogueAsset dialogueAsset;

    [Header("Можно триггерить повторно?")]
    public bool oneShot = true;

    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Триггер сработал! Объект: {other.name}, тег: {other.tag}");
        
        if (!other.CompareTag("Player")) return;
        if (oneShot && _triggered) return;
        if (dialogueAsset == null) return;

        _triggered = true;
        DialogueManager.Instance.StartDialogue(dialogueAsset, OnDialogueFinished);
    }

    private void OnDialogueFinished()
    {
        // Здесь можно повесить логику: заспавнить следующий уступ,
        // разблокировать хват и т.д.
        Debug.Log($"Диалог на {gameObject.name} завершён");
    }

    
}