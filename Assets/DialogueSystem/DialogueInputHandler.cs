using UnityEngine;

public class DialogueInputHandler : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            DialogueManager.Instance?.Advance();
        }
    }
}