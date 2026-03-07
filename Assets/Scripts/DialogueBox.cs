using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class DialogueBox : MonoBehaviour
{
    [Header("Text Components")]
    [Tooltip("The TextMeshProUGUI component that displays the dialogue. If null, will try to find one in children.")]
    public TextMeshProUGUI dialogueText;
    
    [Tooltip("Optional: Image/GameObject that shows when there's more text to read (blinking arrow, etc.)")]
    public GameObject continueIndicator;

    [Header("Settings")]
    [Tooltip("Characters per second for typewriter effect.")]
    public float textSpeed = 30f;
    
    [Tooltip("Input action for advancing dialogue (Z key, Space, etc.). Leave null to use default.")]
    public InputActionReference advanceAction;
    
    [Tooltip("Time in seconds before the continue indicator appears.")]
    public float indicatorDelay = 0.5f;

    [Header("Audio (Optional)")]
    [Tooltip("Audio source for text typing sound effect.")]
    public AudioSource typingSound;
    
    [Tooltip("Play typing sound every N characters (0 = every character, higher = less frequent).")]
    public int typingSoundInterval = 3;

    [Header("Animation")]
    [Tooltip("Duration in seconds for slide-up animation.")]
    public float slideUpDuration = 0.3f;
    
    [Tooltip("Duration in seconds for slide-down animation when closing.")]
    public float slideDownDuration = 0.2f;
    
    [Tooltip("How far below the screen the dialogue box starts (in pixels or units).")]
    public float hiddenOffset = 200f;

    [Header("Forced Rect Transform (applied when dialogue shows)")]
    [Tooltip("When enabled, the dialogue box is forced to these values each time it opens.")]
    public bool useForcedRectTransform = true;
    public float forcedPosX = -3.2884f;
    public float forcedPosY = -443.7852f;
    public float forcedWidth = 1363.656f;
    public float forcedHeight = 285.1071f;

    [Header("Layout (used only when Forced Rect Transform is off)")]
    [Tooltip("Nudge the visible position up (positive) or down (negative).")]
    public float visiblePositionYOffset = 100f;
    
    [Tooltip("Scale of the dialogue box. Lower = smaller.")]
    [Range(0.5f, 1.5f)]
    public float scaleMultiplier = 0.85f;

    [Header("Dialogue Lines")]
    [Tooltip("List of dialogue lines to display. Use StartDialogue() to begin showing them.")]
    public List<string> dialogueLines = new List<string>();

    private List<string> dialogueQueue = new List<string>();
    private int currentDialogueIndex = 0;
    private bool isDisplayingText = false;
    private bool canAdvance = false;
    private Coroutine typewriterCoroutine;
    private Coroutine slideAnimationCoroutine;
    private InputAction advanceInput;
    
    private RectTransform rectTransform;
    private Vector2 visiblePosition;
    private Vector2 hiddenPosition;
    private Vector3 designScale;
    private bool isAnimating = false;

    private void Awake()
    {
        // Get RectTransform for UI positioning
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("DialogueBox: Requires RectTransform component for UI animation.");
            return;
        }

        // Store the visible position and scale from the editor layout
        visiblePosition = rectTransform.anchoredPosition;
        designScale = rectTransform.localScale;
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);

        // Auto-find dialogue text if not assigned
        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (dialogueText != null)
        {
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.enableWordWrapping = true;
        }

        // Set up input
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

        // Hide continue indicator initially
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        // Start hidden (below screen)
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

    /// <summary>
    /// Start displaying a list of dialogue messages.
    /// </summary>
    public void ShowDialogue(List<string> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            Debug.LogWarning("DialogueBox: No messages provided.");
            return;
        }

        dialogueQueue = new List<string>(messages);
        currentDialogueIndex = 0;

        // Activate this object, all parents, and all children so the whole UI (including text) is visible
        Transform t = transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
        SetActiveRecursive(transform, true);

        // Clear any existing text immediately
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        if (slideAnimationCoroutine != null)
            StopCoroutine(slideAnimationCoroutine);
        slideAnimationCoroutine = StartCoroutine(ShowDialogueAndSlideUp());
    }

    /// <summary>
    /// Start displaying a single dialogue message.
    /// </summary>
    public void ShowDialogue(string message)
    {
        ShowDialogue(new List<string> { message });
    }

    /// <summary>
    /// Start displaying dialogue from an array.
    /// </summary>
    public void ShowDialogue(string[] messages)
    {
        ShowDialogue(new List<string>(messages));
    }

    /// <summary>
    /// Start displaying dialogue from the dialogueLines list.
    /// </summary>
    public void StartDialogue()
    {
        ShowDialogue(dialogueLines);
    }

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
            rectTransform.localScale = new Vector3(designScale.x * scaleMultiplier, designScale.y * scaleMultiplier, designScale.z);
        }

        slideAnimationCoroutine = StartCoroutine(SlideUp());
    }

    private void StartDisplayingText()
    {
        if (currentDialogueIndex >= dialogueQueue.Count)
        {
            // All dialogue finished
            CloseDialogue();
            return;
        }

        string currentText = dialogueQueue[currentDialogueIndex];
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }
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
        {
            continueIndicator.SetActive(false);
        }

        dialogueText.text = "";
        int charCount = 0;

        foreach (char c in fullText)
        {
            dialogueText.text += c;
            charCount++;

            // Play typing sound
            if (typingSound != null && charCount % (typingSoundInterval + 1) == 0)
            {
                typingSound.Play();
            }

            yield return new WaitForSeconds(1f / textSpeed);
        }

        // Text finished displaying
        isDisplayingText = false;
        canAdvance = true;
        
        // Show continue indicator after delay
        if (continueIndicator != null)
        {
            yield return new WaitForSeconds(indicatorDelay);
            if (canAdvance) // Make sure we're still on this dialogue
            {
                // Set continueIndicator text if it has TMP
                TextMeshProUGUI nextText = continueIndicator.GetComponent<TextMeshProUGUI>();
                if (nextText != null)
                {
                    nextText.text = "Press E to continue";
                }
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
            // Skip typewriter effect and show full text immediately
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            dialogueText.text = dialogueQueue[currentDialogueIndex];
            isDisplayingText = false;
            canAdvance = true;
            
            if (continueIndicator != null)
            {
                // Set continueIndicator text if it has TMP
                TextMeshProUGUI nextText = continueIndicator.GetComponent<TextMeshProUGUI>();
                if (nextText != null)
                {
                    nextText.text = "Press E to continue";
                }
                continueIndicator.SetActive(true);
                StartCoroutine(BlinkIndicator());
            }
            return;
        }

        // Move to next dialogue
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
            
            // Ease-out curve for smooth deceleration
            t = 1f - Mathf.Pow(1f - t, 3f);
            
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        isAnimating = false;
        
        // Start displaying text after slide-up completes
        StartDisplayingText();
    }

    private IEnumerator SlideDown()
    {
        if (rectTransform == null)
        {
            gameObject.SetActive(false);
            yield break;
        }

        isAnimating = true;
        float elapsed = 0f;
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = hiddenPosition;

        while (elapsed < slideDownDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDownDuration;
            
            // Ease-in curve for smooth acceleration
            t = t * t;
            
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        isAnimating = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Close the dialogue box and clear all messages.
    /// </summary>
    public void CloseDialogue()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        dialogueQueue.Clear();
        currentDialogueIndex = 0;
        isDisplayingText = false;
        canAdvance = false;
        
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }

        // Slide down animation before hiding
        if (rectTransform != null && gameObject.activeSelf)
        {
            if (slideAnimationCoroutine != null)
            {
                StopCoroutine(slideAnimationCoroutine);
            }
            slideAnimationCoroutine = StartCoroutine(SlideDown());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Check if dialogue is currently being displayed.
    /// </summary>
    public bool IsDialogueActive()
    {
        return gameObject.activeSelf && dialogueQueue.Count > 0;
    }

    private static void SetActiveRecursive(Transform root, bool active)
    {
        root.gameObject.SetActive(active);
        for (int i = 0; i < root.childCount; i++)
            SetActiveRecursive(root.GetChild(i), active);
    }
}
