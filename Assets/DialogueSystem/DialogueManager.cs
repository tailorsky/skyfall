using UnityEngine;
using TMPro;
using System.Collections;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;

    [Header("Настройки")]
    [Range(0.01f, 0.1f)]
    public float textSpeed = 0.02f;

    [Header("Звук печати")]
    public AudioSource typingAudio;
    public AudioClip typingSound;

    private bool playSound = false;

    private DialogueAsset _currentDialogue;
    private int _currentLineIndex = 0;
    private bool _isTyping = false;
    private Coroutine _typingCoroutine;

    // Колбэк — вызывается когда диалог полностью завершён
    public System.Action OnDialogueEnd;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartDialogue(DialogueAsset dialogue, System.Action onEnd = null)
    {
        if (dialogue == null || dialogue.lines.Length == 0) return;

        StopAllCoroutines();
        _currentDialogue = dialogue;
        _currentLineIndex = 0;
        OnDialogueEnd = onEnd;

        dialoguePanel.SetActive(true);
        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        if (_currentLineIndex >= _currentDialogue.lines.Length)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = _currentDialogue.lines[_currentLineIndex];

        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeText(line.text));
    }

    private IEnumerator TypeText(string text)
    {
        _isTyping = true;
        dialogueText.text = "";

        foreach (char c in text)
        {
            dialogueText.text += c;
            
            // Играем звук на каждый символ (кроме пробела)
            if (c != '\0' && typingAudio != null && typingSound != null && playSound != true)
            {
                typingAudio.PlayOneShot(typingSound);
                playSound = true;
            }
            
            yield return new WaitForSecondsRealtime(textSpeed);
        }
        
        Invoke(nameof(stopSound), 0.15f);
        _isTyping = false;
        playSound = false;
    }

    private void stopSound()
    {
        typingAudio.Stop();
    }
    public void Advance()
    {
        if (!dialoguePanel.activeSelf) return;
        if (_currentDialogue == null) return;

        if (_isTyping)
        {
            // Пропустить печать — показать весь текст сразу
            StopCoroutine(_typingCoroutine);
            _isTyping = false;
            dialogueText.text = _currentDialogue.lines[_currentLineIndex].text;
        }
        else
        {
            // Следующая реплика
            _currentLineIndex++;
            ShowCurrentLine();
        }
    }

    private void EndDialogue()
    {
        dialoguePanel.SetActive(false);
        _currentDialogue = null;
        OnDialogueEnd?.Invoke();
        OnDialogueEnd = null;
    }

    public bool IsActive() => dialoguePanel.activeSelf;
}