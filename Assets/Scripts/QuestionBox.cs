using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class QuestionBox : MonoBehaviour
{
    [Header("Text")]
    public TextMeshProUGUI optionAText;
    public TextMeshProUGUI optionBText;

    [Header("Arrow Indicator")]
    [Tooltip("The '<-' arrow GameObject. Will be repositioned next to the selected option.")]
    public RectTransform arrowIndicator;

    [Header("Input")]
    [Tooltip("Action used to navigate Up/Down between options (e.g. Player/Move).")]
    public InputActionReference navigateAction;
    [Tooltip("Action used to confirm the selected option (e.g. Player/Interact).")]
    public InputActionReference confirmAction;

    [Header("Slide Animation")]
    public float showDuration = 0.3f;
    public float hideDuration = 0.2f;
    [Tooltip("Distance (pixels) below screen where the panel hides.")]
    public float hiddenOffset = 800f;

    private int selectedIndex = 0;
    private QuestionData currentQuestion;
    private bool isActive = false;
    private bool navHeld = false;

    private RectTransform rectTransform;
    private Vector2 visiblePosition;
    private Vector2 hiddenPosition;
    private Coroutine slideRoutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        visiblePosition = rectTransform.anchoredPosition;
        hiddenPosition = new Vector2(visiblePosition.x, visiblePosition.y - hiddenOffset);
        rectTransform.anchoredPosition = hiddenPosition;
        gameObject.SetActive(false);
    }

    // Called by DialogueBox via reflection.
    public void ShowQuestion(QuestionData question)
    {
        currentQuestion = question;
        selectedIndex = 0;
        isActive = true;

        optionAText.text = question.optionA.label;
        optionBText.text = question.optionB.label;

        UpdateArrow();
        gameObject.SetActive(true);

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideIn());

        SubscribeInput();
    }

    // Polled by DialogueBox via reflection — true while waiting for player input.
    public bool IsActive() => isActive;

    // Called by DialogueBox via reflection when it needs to force-close.
    public void Hide()
    {
        UnsubscribeInput();
        isActive = false;
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideOut());
    }

    private void SubscribeInput()
    {
        if (navigateAction != null)
        {
            navigateAction.action.started += OnNavigate;
            navigateAction.action.canceled += OnNavigateCanceled;
            navigateAction.action.Enable();
        }

        if (confirmAction != null)
        {
            confirmAction.action.started += OnConfirm;
            // Don't Enable — shared asset actions (like Interact) are managed externally.
        }
    }

    private void UnsubscribeInput()
    {
        if (navigateAction != null)
        {
            navigateAction.action.started -= OnNavigate;
            navigateAction.action.canceled -= OnNavigateCanceled;
        }

        if (confirmAction != null)
            confirmAction.action.started -= OnConfirm;
    }

    private void OnNavigate(InputAction.CallbackContext ctx)
    {
        Vector2 dir = ctx.ReadValue<Vector2>();
        // Up arrow → option A (0), Down arrow → option B (1)
        if (dir.y > 0.5f)
            selectedIndex = 0;
        else if (dir.y < -0.5f)
            selectedIndex = 1;

        UpdateArrow();
    }

    private void OnNavigateCanceled(InputAction.CallbackContext ctx)
    {
        navHeld = false;
    }

    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (!isActive) return;

        UnsubscribeInput();
        isActive = false;

        // Fire the chosen option's event
        if (selectedIndex == 0)
            currentQuestion.optionA.onChosen?.Invoke();
        else
            currentQuestion.optionB.onChosen?.Invoke();

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideOut());
    }

    private void UpdateArrow()
    {
        if (arrowIndicator == null) return;

        // Snap the arrow's Y to match the selected option's Y position
        RectTransform targetRect = selectedIndex == 0
            ? optionAText.rectTransform
            : optionBText.rectTransform;

        arrowIndicator.anchoredPosition = new Vector2(
            arrowIndicator.anchoredPosition.x,
            targetRect.anchoredPosition.y
        );
    }

    private IEnumerator SlideIn()
    {
        rectTransform.anchoredPosition = hiddenPosition;
        float elapsed = 0f;
        while (elapsed < showDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - elapsed / showDuration, 3f); // ease-out cubic
            rectTransform.anchoredPosition = Vector2.Lerp(hiddenPosition, visiblePosition, t);
            yield return null;
        }
        rectTransform.anchoredPosition = visiblePosition;
    }

    private IEnumerator SlideOut()
    {
        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = new Vector2(startPos.x, startPos.y - hiddenOffset);
        float elapsed = 0f;
        while (elapsed < hideDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - elapsed / hideDuration, 3f);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
        rectTransform.anchoredPosition = endPos;
        gameObject.SetActive(false);
    }
}
