using UnityEngine;
using UnityEngine.InputSystem;

// Handles input and state for question prompts.
// Visuals (option labels, arrow) are driven entirely through the assigned SpeakerBox.
public class QuestionBox : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Action used to navigate Up/Down between options (e.g. Player/Move).")]
    public InputActionReference navigateAction;
    [Tooltip("Action used to confirm the selected option (e.g. Player/Interact).")]
    public InputActionReference confirmAction;

    [Header("Speaker Box")]
    [Tooltip("The SpeakerBox that visually renders the options. Auto-wired by DialogueBox at runtime if left empty.")]
    public SpeakerBox speakerBox;

    private int selectedIndex = 0;
    private int optionCount = 0;
    private QuestionData currentQuestion;
    private bool isActive = false;

    // Called by DialogueBox via reflection.
    public void ShowQuestion(QuestionData question)
    {
        currentQuestion = question;
        selectedIndex = 0;
        optionCount = (question?.options != null) ? question.options.Count : 0;
        isActive = true;

        if (speakerBox != null && question?.options != null && question.options.Count > 0)
        {
            speakerBox.EnterQuestionMode(question.options);
            speakerBox.SelectOption(0);
        }

        SubscribeInput();
    }

    // Called by DialogueBox to wire the speaker at runtime (avoids needing manual Inspector assignment).
    public void SetSpeakerBox(SpeakerBox sb)
    {
        speakerBox = sb;
    }

    // Polled by DialogueBox via reflection — true while waiting for player input.
    public bool IsActive() => isActive;

    // Called by DialogueBox via reflection when it needs to force-close.
    public void Hide()
    {
        UnsubscribeInput();
        CloseQuestion();
    }

    private void CloseQuestion()
    {
        isActive = false;
        speakerBox?.ExitQuestionMode();
    }

    private void SubscribeInput()
    {
        if (navigateAction != null)
        {
            navigateAction.action.started += OnNavigate;
            navigateAction.action.Enable();
        }

        if (confirmAction != null)
            confirmAction.action.started += OnConfirm;
    }

    private void UnsubscribeInput()
    {
        if (navigateAction != null)
            navigateAction.action.started -= OnNavigate;

        if (confirmAction != null)
            confirmAction.action.started -= OnConfirm;
    }

    private void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (!isActive || optionCount == 0) return;

        Vector2 dir = ctx.ReadValue<Vector2>();
        if (dir.y > 0.5f)
            selectedIndex = (selectedIndex - 1 + optionCount) % optionCount;
        else if (dir.y < -0.5f)
            selectedIndex = (selectedIndex + 1) % optionCount;

        speakerBox?.SelectOption(selectedIndex);
    }

    private void OnConfirm(InputAction.CallbackContext ctx)
    {
        if (!isActive) return;
        if (currentQuestion?.options == null || selectedIndex >= currentQuestion.options.Count) return;

        UnsubscribeInput();
        currentQuestion.options[selectedIndex].onChosen?.Invoke();
        CloseQuestion();
    }
}
