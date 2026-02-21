using UnityEngine;
using TMPro;
using System.Collections;

public class DialogueManager : MonoBehaviour
{
    private void Start()
    {
        Invoke(nameof(AutoTestDialogue), 2f);
    }

    private void AutoTestDialogue()
    {
        Debug.Log("🔥 АВТОТЕСТ ДИАЛОГА!");
        DialogueAsset testAsset = Resources.Load<DialogueAsset>("cliff_intro");
        if (testAsset != null)
        {
            Debug.Log($"✅ НАШЁЛ ДИАЛОГ: {testAsset.name}");
            StartDialogue(testAsset);
        }
    }

    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject dialoguePanel;
    public TMP_Text currentText;
    // speakerText больше НЕ нужен!

    [Header("Настройки")]
    [Range(0.01f, 0.1f)]
    public float textSpeed = 0.026f;  // БЫСТРО! 0.02 = 50 символов/сек

    private DialogueAsset _currentDialogue;
    private int _currentLineIndex = 0;
    private bool _isTyping = false;
    private Coroutine _typingCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // ЛЮБАЯ КЛАВИША = следующая реплика
        if (_isTyping && Input.anyKeyDown)
        {
            SkipTyping();
        }
    }

    public void StartDialogue(DialogueAsset dialogue)
    {
        if (dialogue == null || dialogue.lines == null || dialogue.lines.Length == 0)
        {
            Debug.LogWarning("Пустой диалог!");
            return;
        }

        _currentDialogue = dialogue;
        _currentLineIndex = 0;
        dialoguePanel.SetActive(true);
        ShowNextLine();
    }

    public void StopDialogue()
    {
        dialoguePanel.SetActive(false);
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }
        _currentDialogue = null;
        _currentLineIndex = 0;
        _isTyping = false;
    }

    public void ShowNextLine()
    {
        // 🛑 ОСТАНАВЛИВАЕМ ЛЮБУЮ СТАРУЮ КОРУТИНУ
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            _isTyping = false;
        }

        if (_currentDialogue == null || _currentLineIndex >= _currentDialogue.lines.Length)
        {
            StopDialogue();
            return;
        }

        DialogueLine line = _currentDialogue.lines[_currentLineIndex];
        _typingCoroutine = StartCoroutine(TypeText(line));
        _currentLineIndex++;
    }

    private IEnumerator TypeText(DialogueLine line)
    {
        _isTyping = true;
        
        currentText.text = "";
        //currentText.color = line.speakerId.Contains("bad") ? Color.red : Color.cyan;

        string fullText = line.text;
        
        // 🎯 ФИКСИРОВАННАЯ СКОРОСТЬ: N символов за кадр
        for (int i = 0; i < fullText.Length; i++)
        {
            currentText.text = fullText.Substring(0, i + 1);
            
            // Ждём фиксированное время независимо от FPS
            float timer = 0;
            while (timer < textSpeed)
            {
                timer += Time.unscaledDeltaTime;  // НЕ зависит от timescale!
                yield return null;  // 1 кадр
            }
        }

        _isTyping = false;
        _typingCoroutine = null;
    }


    public void SkipTyping()
    {
        if (_isTyping && _typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            _isTyping = false;
            ShowNextLine();
        }
    }
}
