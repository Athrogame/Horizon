using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class DialogueBox : MonoBehaviour
{
    [Header("Text Components")]
    public TextMeshProUGUI dialogueText;
    public GameObject continueIndicator;

    [Header("Settings")]
    public float textSpeed = 30f;
    public InputActionReference advanceAction;
    public float indicatorDelay = 0.5f;

    [Header("Audio (Optional)")]
    public AudioSource typingSound;
    public int typingSoundInterval = 3;

    [Header("Panel Animation")]
    public float slideUpDuration = 0.5f;
    public float slideDownDuration = 0.5f;
    public float closeDelay = 0.15f;
    public float hiddenOffset = 800f;

    [Header("Forced Rect Transform (panel)")]
    public bool useForcedRectTransform = true;
    public float forcedPosX = -3.2884f;
    public float forcedPosY = -443.7852f;
    public float forcedWidth = 1363.656f;
    public float forcedHeight = 285.1071f;

    [Header("Layout (when not using forced rect)")]
    public float visiblePositionYOffset = 100f;
    [Range(0.5f, 1.5f)]
    public float scaleMultiplier = 0.85f;

    [Header("Dialogue Lines")]
    public List<string> dialogueLines = new List<string>();

    [Header("Speaker Controller (new script)")]
    public SpeakerBox speakerBoxController; // assign your SpeakerBox component here

    private List<string> dialogueQueue = new List<string>();
    private int currentDialogueIndex = 0;
    private bool isDisplayingText = false;
    private bool canAdvance = false;
    private Coroutine typewriterCoroutine;
    private Coroutine slideAnimationCoroutine;
    private InputAction advanceInput;

    // Panel layout
    private RectTransform rectTransform;
    private Vector2 visiblePosition;
    private Vector2 hiddenPosition;
    private Vector3 designScale;
    private bool isAnimating = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("DialogueBox: Requires RectTransform component.");
            return;
        }

        visiblePosition = rectTransform.anchoredPosition;
        designScale = rectTransform.localScale;
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);

        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (dialogueText != null)
        {
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.enableWordWrapping = true;
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
    }

    private void OnDisable()
    {
        advanceInput?.Disable();
    }

    private void Update()
    {
        if (canAdvance && advanceInput != null && advanceInput.WasPressedThisFrame())
        {
            AdvanceDialogue();
        }
    }

    // --- Public API --------------------------------------------------------

    public void ShowDialogue(List<string> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            Debug.LogWarning("DialogueBox: No messages provided.");
            return;
        }

        dialogueQueue = new List<string>(messages);
        currentDialogueIndex = 0;

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

        if (slideAnimationCoroutine != null)
            StopCoroutine(slideAnimationCoroutine);
        slideAnimationCoroutine = StartCoroutine(ShowDialogueAndSlideUp());

        // Let speaker controller know dialogue started
        if (speakerBoxController != null)
            speakerBoxController.ShowNormal();
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
        canAdvance = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        // Tell speaker to slide out
        if (speakerBoxController != null)
            speakerBoxController.Hide();

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

    // --- Internals ---------------------------------------------------------

    private IEnumerator ShowDialogueAndSlideUp()
    {
        yield return null;
        if (rectTransform == null) yield break;

        if (useForcedRectTransform)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(forcedWidth, forcedHeight);
            rectTransform.localScale = Vector3.one;
            visiblePosition = new Vector2(forcedPosX, forcedPosY);
            hiddenPosition = new Vector2(forcedPosX, forcedPosY - hiddenOffset);
            rectTransform.anchoredPosition = hiddenPosition;
        }
        else
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            visiblePosition = rectTransform.anchoredPosition;
            visiblePosition.y += visiblePositionYOffset;
            hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);
            rectTransform.anchoredPosition = hiddenPosition;
            rectTransform.localScale = new Vector3(
                designScale.x * scaleMultiplier,
                designScale.y * scaleMultiplier,
                designScale.z
            );
        }

        slideAnimationCoroutine = StartCoroutine(SlideUp());
    }

    private void StartDisplayingText()
    {
        if (currentDialogueIndex >= dialogueQueue.Count)
        {
            CloseDialogue();
            return;
        }

        string currentText = dialogueQueue[currentDialogueIndex];

        if (speakerBoxController != null)
            speakerBoxController.SetEmotionForLine(currentDialogueIndex);

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterEffect(currentText));
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        if (dialogueText == null)
        {
            isDisplayingText = false;
            canAdvance = true;
            yield break;
        }

        isDisplayingText = true;
        canAdvance = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        dialogueText.text = "";
        int charCount = 0;

        foreach (char c in fullText)
        {
            dialogueText.text += c;
            charCount++;

            if (typingSound != null && charCount % (typingSoundInterval + 1) == 0)
                typingSound.Play();

            yield return new WaitForSeconds(1f / textSpeed);
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
                    nextText.text = "Press E to continue";
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
        {
            if (typewriterCoroutine != null)
                StopCoroutine(typewriterCoroutine);

            dialogueText.text = dialogueQueue[currentDialogueIndex];
            isDisplayingText = false;
            canAdvance = true;

            if (continueIndicator != null)
            {
                var nextText = continueIndicator.GetComponent<TextMeshProUGUI>();
                if (nextText != null)
                    nextText.text = "Press E to continue";
                continueIndicator.SetActive(true);
                StartCoroutine(BlinkIndicator());
            }
            return;
        }

        currentDialogueIndex++;
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