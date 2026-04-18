using UnityEngine;
using UnityEngine.InputSystem;

// Triggers a DialogueBox when an input action fires.
// Attach to any GameObject. Assign the DialogueBox and the Interact InputActionReference in the Inspector.
public class ShowDialogueOnKey : MonoBehaviour
{
    [Tooltip("DialogueBox to trigger. Auto-found in the scene if left empty.")]
    public DialogueBox dialogueBox;

    [Tooltip("Input action that triggers the dialogue (e.g. Player/Interact).")]
    public InputActionReference interactAction;

    private void Start()
    {
        if (dialogueBox == null)
            dialogueBox = Object.FindAnyObjectByType<DialogueBox>(FindObjectsInactive.Include);

        if (dialogueBox == null)
            Debug.LogWarning("[ShowDialogueOnKey] No DialogueBox found. Assign one in the Inspector.");
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.started += OnInteract;
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.started -= OnInteract;
            // Do NOT call Disable() — this is a shared asset action managed externally.
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (dialogueBox == null)
            dialogueBox = Object.FindAnyObjectByType<DialogueBox>(FindObjectsInactive.Include);

        if (dialogueBox == null || dialogueBox.IsDialogueActive())
            return;

        dialogueBox.StartDialogue();
    }
}
