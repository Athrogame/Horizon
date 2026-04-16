using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Simple helper: press a key to show example dialogue using DialogueBox.
/// Attach to any GameObject and optionally assign a DialogueBox in the inspector.
/// </summary>
public class ShowDialogueOnKey : MonoBehaviour
{
    [Tooltip("DialogueBox component to control. If left empty the script will try to FindObjectOfType at Start.")]
    public DialogueBox dialogueBox;

    [Tooltip("Key that triggers the dialogue (default = P). Used only if Interact action is not assigned.")]
    public KeyCode triggerKey = KeyCode.P;

    [Tooltip("(New Input System) Optional Input Action Reference for the Interact action. If set, this will be used instead of KeyCode.")]
    public InputActionReference interactAction;

    [Tooltip("(New Input System) Fallback key when Interact action is not assigned. E = Interact.")]
    public UnityEngine.InputSystem.Key triggerKeyInput = UnityEngine.InputSystem.Key.E;

    void Start()
    {
        if (dialogueBox == null)
        {
            // Include inactive so we find the DialogueBox even when it starts hidden
            dialogueBox = FindObjectOfType<DialogueBox>(true);
            if (dialogueBox == null)
            {
                Debug.LogWarning("ShowDialogueOnKey: No DialogueBox found in scene. Assign one in the inspector.");
            }
        }
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            // Only "started" so one press = one show (avoids double trigger with Hold/performed)
            interactAction.action.started += OnInteract;
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
        {
            interactAction.action.started -= OnInteract;
            interactAction.action.Disable();
        }
    }

    void Update()
    {
        // Keyboard fallback removed — dialogue is only triggered via an assigned
        // InputActionReference (interactAction) or an explicit script call.
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (dialogueBox == null)
            dialogueBox = FindObjectOfType<DialogueBox>(true);
        if (dialogueBox == null)
        {
            Debug.LogWarning("ShowDialogueOnKey: No DialogueBox in scene. Add a UI with DialogueBox component.");
            return;
        }
        if (dialogueBox.IsDialogueActive())
            return;
        dialogueBox.StartDialogue();
    }
}

