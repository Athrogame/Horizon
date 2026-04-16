using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;using UnityEngine.InputSystem;

[System.Serializable]
public class DialogueLine
{
    [TextArea(3, 10)]
    public string text;
    [Tooltip("If checked, this text will append to the end of the previous text box instead of clearing it.")]
    public bool appendToPrevious = false;
    [Tooltip("Delay in seconds from the time this line starts before the player is allowed to skip typing or advance.")]
    public float delayBeforeSkipAllowed = 0f;
    [Tooltip("Font size for this specific line. Default is 57.")]
    public float fontSize = 57f;
    [Tooltip("Custom typing speed for this line. Set to 0 to use the global textSpeed.")]
    public float customSpeed = 0f;
    [Tooltip("If checked, characters string will bounce up and down!")]
    public bool emphasizeLine = false;
    [Tooltip("Event triggered exactly when this line finishes and the player advances to the next one.")]
    public UnityEngine.Events.UnityEvent onLineFinished;

    public DialogueLine() {}
    public DialogueLine(string text)
    {
        this.text = text;
        this.appendToPrevious = false;
        this.delayBeforeSkipAllowed = 0f;
        this.fontSize = 57f;
        this.customSpeed = 0f;
        this.emphasizeLine = false;
        this.onLineFinished = new UnityEngine.Events.UnityEvent();
    }
}

public class DialogueBox : MonoBehaviour
{
    [Header("Text Components")]
    public TextMeshProUGUI dialogueText;
    public GameObject continueIndicator;

    [Header("Settings")]
    public float textSpeed = 30f;
    public float fastForwardMultiplier = 4f;
    public InputActionReference advanceAction;
    public float indicatorDelay = 0.5f;
    [Tooltip("If true, automatically runs inspector dialogue lines when GameObject is turned on.")]
    public bool playOnEnable = true;
    [Tooltip("If true, forces player to idle animation during dialogue (Uncheck for timelines!).")]
    public bool forcePlayerIdle = false;

    [Header("Audio (Optional)")]
    public AudioSource typingSound;
    public int typingSoundInterval = 3;

    [Header("Events")]
    [Tooltip("Fired when the entire dialogue box finishes all lines and closes.")]
    public UnityEngine.Events.UnityEvent onDialogueClosed;

    [Header("Panel Animation")]
    public float slideUpDuration = 0.5f;
    public float slideDownDuration = 0.5f;
    public float closeDelay = 0.15f;
    public float hiddenOffset = 5000f;

    [Header("Dialogue Lines")]
    public List<DialogueLine> dialogueLines = new List<DialogueLine>();

    [Header("Speaker Controller (new script)")]
    [Tooltip("Drag the GameObject that has SpeakerBox on it here.")]
    public MonoBehaviour speakerBoxController; // SpeakerBox component (runtime-called)

    private List<DialogueLine> dialogueQueue = new List<DialogueLine>();
    private int currentDialogueIndex = 0;
    private bool isDisplayingText = false;
    private bool isFastForwarding = false;
    private bool canAdvance = false;
    private float skipAllowedTime = 0f;
    private string baseTextBeforeAppending = "";
    private Coroutine typewriterCoroutine;
    private Coroutine slideAnimationCoroutine;
    private InputAction advanceInput;

    // Guard so multiple ShowDialogue calls in one cutscene don't stack extra locks.
    // DialogueBox should only ever hold one movement lock at a time.
    private bool _hasMovementLock = false;

    // Panel layout
    private RectTransform rectTransform;
    private Vector2 visiblePosition;
    private Vector2 hiddenPosition;
    private bool isAnimating = false;

    private void Awake()
    {
        if (hiddenOffset < 5000f) hiddenOffset = 5000f;

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("DialogueBox: Requires RectTransform component.");
            return;
        }

        visiblePosition = rectTransform.anchoredPosition;
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);

        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (dialogueText != null)
        {
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (advanceAction != null)
        {
            advanceInput = advanceAction.action;
        }
        else
        {
            advanceInput = new InputAction(type: InputActionType.Button);
            advanceInput.AddBinding("<Keyboard>/space");
            advanceInput.AddBinding("<Keyboard>/z");
            advanceInput.AddBinding("<Keyboard>/enter");
        }

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        rectTransform.anchoredPosition = hiddenPosition;
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        advanceInput?.Enable();

        // If something manually turns this Game Object on without passing lines, we auto-play the inspector lines.
        if (playOnEnable && dialogueQueue != null && dialogueQueue.Count == 0)
        {
            StartDialogue();
        }
    }

    private void OnDisable()
    {
        advanceInput?.Disable();
    }

    private void Update()
    {
        if (advanceInput != null && advanceInput.WasPressedThisFrame())
        {
            if (Time.time < skipAllowedTime) return; // Prevent skipping/advancing during the unskippable delay

            if (isDisplayingText)
            {
                isFastForwarding = true;
            }
            else if (canAdvance)
            {
                AdvanceDialogue();
            }
        }
        
        UpdateVertexAnimation();
    }

    // --- Public API --------------------------------------------------------

    public void ShowDialogue(List<DialogueLine> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning("DialogueBox: No messages provided.");
            return;
        }

        dialogueQueue = new List<DialogueLine>(lines);
        currentDialogueIndex = 0;
        baseTextBeforeAppending = "";

        // Ensure this object and parents are active
        Transform t = transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }

        if (dialogueText != null)
            dialogueText.text = "";

        if (PlayerController.I != null && !_hasMovementLock)
        {
            PlayerController.I.LockMovement();
            _hasMovementLock = true;
            if (forcePlayerIdle) PlayerController.I.ForceIdle();
        }

        if (slideAnimationCoroutine != null)
            StopCoroutine(slideAnimationCoroutine);
        slideAnimationCoroutine = StartCoroutine(ShowDialogueAndSlideUp());

        // Let speaker controller know dialogue started
        SpeakerShowNormal();
    }

    public void ShowDialogue(List<string> messages)
    {
        if (messages == null || messages.Count == 0) return;
        List<DialogueLine> lines = new List<DialogueLine>();
        foreach (var m in messages) lines.Add(new DialogueLine(m));
        ShowDialogue(lines);
    }

    public void ShowDialogue(string message)
    {
        ShowDialogue(new List<string> { message });
    }

    public void ShowDialogue(string[] messages)
    {
        ShowDialogue(new List<string>(messages));
    }

    public void StartDialogue()
    {
        ShowDialogue(dialogueLines);
    }

    public void CloseDialogue()
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        dialogueQueue.Clear();
        currentDialogueIndex = 0;
        isDisplayingText = false;
        isFastForwarding = false;
        canAdvance = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        if (PlayerController.I != null && _hasMovementLock)
        {
            PlayerController.I.UnlockMovement();
            _hasMovementLock = false;
        }

        // Tell speaker to slide out
        SpeakerHide();

        onDialogueClosed?.Invoke();

        if (rectTransform != null && gameObject.activeSelf)
        {
            if (slideAnimationCoroutine != null)
                StopCoroutine(slideAnimationCoroutine);
            slideAnimationCoroutine = StartCoroutine(SlideDown());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public bool IsDialogueActive()
    {
        return gameObject.activeSelf && dialogueQueue.Count > 0;
    }

    // SpeakerBox is driven via reflection to avoid compile-time coupling.
    private void SpeakerShowNormal()
    {
        if (speakerBoxController == null)
            return;
        var m = speakerBoxController.GetType().GetMethod("ShowNormal");
        m?.Invoke(speakerBoxController, null);
    }

    private void SpeakerHide()
    {
        if (speakerBoxController == null)
            return;
        var m = speakerBoxController.GetType().GetMethod("Hide");
        m?.Invoke(speakerBoxController, null);
    }

    private void SpeakerSetEmotionForLine(int index)
    {
        if (speakerBoxController == null)
            return;
        var m = speakerBoxController.GetType().GetMethod("SetEmotionForLine");
        if (m != null)
            m.Invoke(speakerBoxController, new object[] { index });
    }

    // --- Internals ---------------------------------------------------------

    private IEnumerator ShowDialogueAndSlideUp()
    {
        yield return null;
        if (rectTransform == null) yield break;

        // Give the UI a frame to update its layout sizes from any new text
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        // Snap to hidden position and slide up to its true anchored position
        rectTransform.anchoredPosition = hiddenPosition;

        slideAnimationCoroutine = StartCoroutine(SlideUp());
    }

    private void StartDisplayingText()
    {
        if (currentDialogueIndex >= dialogueQueue.Count)
        {
            CloseDialogue();
            return;
        }

        DialogueLine line = dialogueQueue[currentDialogueIndex];
        skipAllowedTime = Time.time + line.delayBeforeSkipAllowed;

        SpeakerSetEmotionForLine(currentDialogueIndex);

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterEffect(line));
    }

    private IEnumerator TypewriterEffect(DialogueLine line)
    {
        if (dialogueText == null)
        {
            isDisplayingText = false;
            canAdvance = true;
            yield break;
        }

        isDisplayingText = true;
        isFastForwarding = false;
        canAdvance = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        if (line.appendToPrevious)
        {
            baseTextBeforeAppending = dialogueText.text;
        }
        else
        {
            baseTextBeforeAppending = "";
            dialogueText.text = "";
        }

        int charCount = 0;
        string currentTextToType = line.text ?? "";
        string newlyTyped = "";
        string sizePrefix = $"<size={line.fontSize}>";
        string sizeSuffix = "</size>";
        
        if (line.emphasizeLine)
        {
            sizePrefix += "<link=\"bounce\">";
            sizeSuffix = "</link>" + sizeSuffix;
        }

        foreach (char c in currentTextToType)
        {
            newlyTyped += c;
            dialogueText.text = baseTextBeforeAppending + sizePrefix + newlyTyped + sizeSuffix;
            charCount++;

            if (typingSound != null && charCount % (typingSoundInterval + 1) == 0)
                typingSound.Play();

            float baseSpeed = line.customSpeed > 0f ? line.customSpeed : textSpeed;
            float currentSpeed = isFastForwarding ? (baseSpeed * fastForwardMultiplier) : baseSpeed;
            yield return new WaitForSeconds(1f / currentSpeed);
        }

        isDisplayingText = false;
        canAdvance = true;

        if (continueIndicator != null)
        {
            yield return new WaitForSeconds(indicatorDelay);
            if (canAdvance)
            {
                var nextText = continueIndicator.GetComponent<TextMeshProUGUI>();
                if (nextText != null)
                    nextText.text = "->>";
                continueIndicator.SetActive(true);
                StartCoroutine(BlinkIndicator());
            }
        }
    }

    private IEnumerator BlinkIndicator()
    {
        while (continueIndicator != null && continueIndicator.activeSelf)
        {
            continueIndicator.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            continueIndicator.SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void AdvanceDialogue()
    {
        if (isDisplayingText)
            return;

        int finishedIndex = currentDialogueIndex;
        currentDialogueIndex++;

        // Trigger the event for the line we just finished passing
        if (finishedIndex < dialogueQueue.Count)
        {
            dialogueQueue[finishedIndex].onLineFinished?.Invoke();
        }

        StartDisplayingText();
    }

    private IEnumerator SlideUp()
    {
        if (rectTransform == null) yield break;

        isAnimating = true;
        float elapsed = 0f;
        Vector2 startPos = hiddenPosition;
        Vector2 endPos = visiblePosition;

        while (elapsed < slideUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideUpDuration;
            t = 1f - Mathf.Pow(1f - t, 3f); // ease-out

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        isAnimating = false;

        StartDisplayingText();
    }

    private void UpdateVertexAnimation()
    {
        if (dialogueText == null || !gameObject.activeInHierarchy || dialogueText.textInfo == null) return;

        // Ensure mesh is generated
        dialogueText.ForceMeshUpdate(false, false);
        var textInfo = dialogueText.textInfo;

        if (textInfo.characterCount == 0 || textInfo.linkCount == 0) return;

        bool hasChanges = false;

        for (int i = 0; i < textInfo.linkCount; i++)
        {
            var linkInfo = textInfo.linkInfo[i];
            if (linkInfo.GetLinkID() == "bounce")
            {
                hasChanges = true;
                int firstChar = linkInfo.linkTextfirstCharacterIndex;
                int lastChar = firstChar + linkInfo.linkTextLength;

                for (int j = firstChar; j < lastChar; j++)
                {
                    if (j >= textInfo.characterCount) break;

                    var charInfo = textInfo.characterInfo[j];
                    if (!charInfo.isVisible) continue;

                    int materialIndex = charInfo.materialReferenceIndex;
                    int vertexIndex = charInfo.vertexIndex;

                    Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                    // Omori bounce offset
                    float offset = Mathf.Sin(Time.time * 20f + j * 0.8f) * 6f;

                    vertices[vertexIndex + 0].y += offset;
                    vertices[vertexIndex + 1].y += offset;
                    vertices[vertexIndex + 2].y += offset;
                    vertices[vertexIndex + 3].y += offset;
                }
            }
        }

        if (hasChanges)
        {
            for (int i = 0; i < textInfo.materialCount; i++)
            {
                if (textInfo.meshInfo[i].mesh == null || textInfo.meshInfo[i].vertices == null) continue;
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                dialogueText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }
    }

    private IEnumerator SlideDown()
    {
        if (rectTransform == null)
        {
            gameObject.SetActive(false);
            yield break;
        }

        Vector2 startPos = rectTransform.anchoredPosition;
        hiddenPosition = new Vector2(startPos.x, startPos.y - hiddenOffset);
        Vector2 endPos = hiddenPosition;

        float duration = Mathf.Max(0.01f, slideDownDuration);
        isAnimating = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        isAnimating = false;
        yield return new WaitForSeconds(closeDelay);

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        gameObject.SetActive(false);
    }
}