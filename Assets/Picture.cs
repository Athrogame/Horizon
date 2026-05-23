using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Picture : MonoBehaviour
{
    private const float MinimumSlideInDuration = 1f;
    private const float MaxSlideInOffset = 900f;

    [Header("Panel Animation")]
    [Tooltip("Time it takes for the picture to slide up into view.")]
    public float slideInDuration = MinimumSlideInDuration;
    [Tooltip("Time it takes for the picture to slide down out of view.")]
    public float slideOutDuration = 1.15f;
    [Tooltip("Vertical distance below the normal position the picture starts at.")]
    public float hiddenOffset = 2000f;

    [Header("Input (Optional)")]
    [Tooltip("Custom Input Action Reference to close the picture.")]
    public InputActionReference interactAction;

    [Header("Dialogue Integration")]
    [Tooltip("DialogueBox to trigger alongside the picture. If left empty, it will auto-find in the scene.")]
    public DialogueBox dialogueBox;
    [Tooltip("Optional dialogue lines to play when the picture is shown.")]
    [TextArea(2, 5)]
    public List<string> dialogueLines = new List<string>();
    [Tooltip("If true, the picture will automatically slide down and close after the dialogue ends.")]
    public bool autoCloseAfterDialogue = false;

    private RectTransform rectTransform;
    private Vector2 visiblePosition;
    private CanvasGroup parentCanvasGroup;
    private Image parentImage;
    private InputAction fallbackInteractAction;
    private Coroutine slideCoroutine;

    private bool isInitialized = false;
    private bool isAnimating = false;
    private bool isShown = false;
    private bool waitingForDialogue = false;
    private bool hasBeenShown = false;

    private void Start()
    {
        InitIfNeeded();
        
        // Only hide automatically on startup if Show() hasn't been called yet!
        if (!hasBeenShown)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);
            }
            SetParentAlpha(0f);
            
            // Deactivate parent so it does not capture raycasts until Shown
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(false);
            }
        }
    }

    private void InitIfNeeded()
    {
        if (isInitialized) return;

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            visiblePosition = rectTransform.anchoredPosition;
        }

        if (transform.parent != null)
        {
            parentCanvasGroup = transform.parent.GetComponent<CanvasGroup>();
            parentImage = transform.parent.GetComponent<Image>();
        }

        try
        {
            fallbackInteractAction = InputSystem.actions.FindAction("Interact");
        }
        catch (System.Exception)
        {
            // Fallback action not configured in project input actions
        }

        isInitialized = true;
    }

    private void SetParentAlpha(float alpha)
    {
        if (parentCanvasGroup != null)
        {
            parentCanvasGroup.alpha = alpha;
        }
        else if (parentImage != null)
        {
            Color c = parentImage.color;
            c.a = alpha;
            parentImage.color = c;
        }
    }

    private void Update()
    {
        if (isShown && !isAnimating && !waitingForDialogue)
        {
            bool interactPressed = false;

            if (interactAction != null && interactAction.action != null && interactAction.action.WasPressedThisFrame())
            {
                interactPressed = true;
            }
            else if (fallbackInteractAction != null && fallbackInteractAction.WasPressedThisFrame())
            {
                interactPressed = true;
            }

            if (interactPressed)
            {
                Hide();
            }
        }
    }

    /// <summary>
    /// Activates the overlay and slides the picture in smoothly from the bottom.
    /// Can be called directly via Unity Events or triggers.
    /// </summary>
    public void Show()
    {
        hasBeenShown = true;
        InitIfNeeded();

        if (transform.parent != null)
        {
            transform.parent.gameObject.SetActive(true);
        }
        gameObject.SetActive(true);

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
        }

        slideCoroutine = StartCoroutine(SlideInRoutine());

        // Dialogue Integration: Trigger dialogue and lock controls
        if (dialogueBox != null || (dialogueLines != null && dialogueLines.Count > 0))
        {
            if (dialogueBox == null)
            {
                dialogueBox = Object.FindAnyObjectByType<DialogueBox>(FindObjectsInactive.Include);
            }

            if (dialogueBox != null)
            {
                waitingForDialogue = true;
                dialogueBox.onDialogueClosed.AddListener(OnDialogueEnded);

                if (dialogueLines != null && dialogueLines.Count > 0)
                {
                    dialogueBox.ShowDialogue(dialogueLines);
                }
                else
                {
                    // Fallback to pre-configured inspector dialogue lines on the DialogueBox component itself
                    dialogueBox.StartDialogue();
                }
            }
        }
    }

    /// <summary>
    /// Slides the picture out to the bottom and deactivates the overlay.
    /// </summary>
    public void Hide()
    {
        InitIfNeeded();

        // Ensure we clean up listeners if closed manually or via code
        if (dialogueBox != null)
        {
            dialogueBox.onDialogueClosed.RemoveListener(OnDialogueEnded);
        }
        waitingForDialogue = false;

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
        }

        slideCoroutine = StartCoroutine(SlideOutRoutine());
    }

    private void OnDialogueEnded()
    {
        if (dialogueBox != null)
        {
            dialogueBox.onDialogueClosed.RemoveListener(OnDialogueEnded);
        }

        waitingForDialogue = false;

        if (autoCloseAfterDialogue)
        {
            Hide();
        }
    }

    private IEnumerator SlideInRoutine()
    {
        isAnimating = true;
        isShown = false;

        if (rectTransform == null)
        {
            SetParentAlpha(0.4f);
            isAnimating = false;
            isShown = true;
            yield break;
        }

        float slideInOffset = Mathf.Min(hiddenOffset, MaxSlideInOffset);
        Vector2 startPos = new Vector2(visiblePosition.x, visiblePosition.y - slideInOffset);
        Vector2 endPos = visiblePosition;
        rectTransform.anchoredPosition = startPos;
        SetParentAlpha(0f);

        float elapsed = 0f;
        float duration = Mathf.Max(MinimumSlideInDuration, slideInDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = SmoothControlledEase(elapsed / duration);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            SetParentAlpha(Mathf.Lerp(0f, 0.4f, t));
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        SetParentAlpha(0.4f);
        isAnimating = false;
        isShown = true;
    }

    private IEnumerator SlideOutRoutine()
    {
        isAnimating = true;

        if (rectTransform == null)
        {
            SetParentAlpha(0f);
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(false);
            }
            isAnimating = false;
            isShown = false;
            yield break;
        }

        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, slideOutDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = SmoothControlledEase(elapsed / duration);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            SetParentAlpha(Mathf.Lerp(0.4f, 0f, t));
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        SetParentAlpha(0f);
        isAnimating = false;
        isShown = false;

        if (transform.parent != null)
        {
            transform.parent.gameObject.SetActive(false);
        }
    }

    private static float SmoothControlledEase(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
