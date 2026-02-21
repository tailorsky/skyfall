using UnityEngine;

public class TestDialogue : MonoBehaviour
{
    public DialogueAsset testDialogue;

    private void Update()
    {
        // Было: Input.GetKeyDown(KeyCode.T)
        if (Input.anyKeyDown)  // ← НОВЫЙ КОД: любая клавиша
        {
            DialogueManager.Instance.StartDialogue(testDialogue);
        }
    }
}
