using UnityEngine;
using System;

[System.Serializable]
public class DialogueLine
{
    public string speakerId;
    [TextArea]
    public string text;
}

[CreateAssetMenu(
    fileName = "NewDialogue",
    menuName = "Dialogue/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    [Header("Идентификатор")]
    public string dialogueId;

    [Header("Очередь реплик")]
    public DialogueLine[] lines;
}
