using UnityEngine;
using UnityEngine.InputSystem;

public class InteractTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [Tooltip("DialogueBox to play. If Use Repeat Dialogue is off, this plays every time. If on, this plays the first time only.")]
    public DialogueBox dialogueBox;

    [Tooltip("If checked, a different DialogueBox plays on all interactions after the first.")]
    public bool useRepeatDialogue = false;

    [Tooltip("DialogueBox to play on the second and subsequent interactions. Only used when Use Repeat Dialogue is checked.")]
    public DialogueBox repeatDialogueBox;

    [Header("Input")]
    [Tooltip("Assign Player/Interact from InputSystem_Actions.")]
    public InputActionReference interactAction;

    private bool playerInside = false;
    private bool hasInteractedOnce = false;

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.started += OnInteract;
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.started -= OnInteract;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!playerInside) return;
        if (!IsPlayerFacingMe()) return;

        DialogueBox target = (useRepeatDialogue && hasInteractedOnce) ? repeatDialogueBox : dialogueBox;
        if (target == null || target.IsDialogueActive()) return;

        hasInteractedOnce = true;
        target.StartDialogue();
    }

    private bool IsPlayerFacingMe()
    {
        if (PlayerController.I == null) return false;

        Vector2 dir = (Vector2)(transform.position - PlayerController.I.transform.position);
        Vector2 facing = PlayerController.I.FacingDirection;

        // Determine which cardinal direction the object is in relative to the player
        Vector2 required;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            required = new Vector2(Mathf.Sign(dir.x), 0f);
        else
            required = new Vector2(0f, Mathf.Sign(dir.y));

        return Mathf.RoundToInt(facing.x) == Mathf.RoundToInt(required.x) &&
               Mathf.RoundToInt(facing.y) == Mathf.RoundToInt(required.y);
    }
}
