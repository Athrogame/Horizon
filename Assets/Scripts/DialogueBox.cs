using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class DialogueBox : MonoBehaviour
{
    [Header("Text Components")]
    [Tooltip("The UI Text component that displays the dialogue. If null, will try to find one in children.")]
    public Text dialogueText;
    
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

        // Store the visible position (current position)
        visiblePosition = rectTransform.anchoredPosition;
        
        // Calculate hidden position (below screen)
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);

        // Auto-find dialogue text if not assigned
        if (dialogueText == null)
        {
            dialogueText = GetComponentInChildren<Text>();
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
        
        // Start hidden, then slide up
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = hiddenPosition;
        }
        
        gameObject.SetActive(true);
        
        // Slide up animation
        if (slideAnimationCoroutine != null)
        {
            StopCoroutine(slideAnimationCoroutine);
        }
        slideAnimationCoroutine = StartCoroutine(SlideUp());
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
}
