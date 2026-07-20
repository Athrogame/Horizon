using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// An in-game save point. When the player is inside its trigger and facing it, pressing Interact
/// opens the <see cref="SaveSlotSelectMenu"/> to choose which slot to save into.
///
/// Modeled on InteractTrigger: same trigger + facing check + Input System hookup.
/// </summary>
public class SavePoint : MonoBehaviour
{
    [Header("Save menu")]
    [Tooltip("Optional. Leave empty to use the persistent SaveSlotSelectMenu automatically. " +
             "Only assign this to override with a specific menu in this scene.")]
    public SaveSlotSelectMenu saveMenu;

    // Use the explicitly-assigned menu, or fall back to the persistent one.
    private SaveSlotSelectMenu Menu => saveMenu != null ? saveMenu : SaveSlotSelectMenu.I;

    [Header("Input")]
    [Tooltip("Assign Player/Interact from InputSystem_Actions.")]
    public InputActionReference interactAction;

    [Header("Debug")]
    [Tooltip("Log to the Console why an interact did or didn't open the menu. Turn on only when troubleshooting.")]
    public bool debugLogs = false;

    private bool playerInside = false;

    private void OnEnable()
    {
        if (interactAction == null)
            Debug.LogWarning($"SavePoint '{name}': Interact Action is NOT assigned — interacts will never fire.");
        else
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
        {
            playerInside = true;
            if (debugLogs) Debug.Log($"SavePoint '{name}': player ENTERED trigger.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (debugLogs) Debug.Log($"SavePoint '{name}': interact pressed.");

        if (!playerInside)
        {
            if (debugLogs) Debug.Log($"SavePoint '{name}': player NOT inside the trigger (check collider Is Trigger + player tag).");
            return;
        }
        SaveSlotSelectMenu menu = Menu;
        if (menu == null)
        {
            if (debugLogs) Debug.Log($"SavePoint '{name}': no save menu found (SaveSlotSelectMenu.I is null — is the menu in a loaded scene?).");
            return;
        }
        if (menu.IsOpen) return;
        if (DialogueBox.AnyActive)
        {
            if (debugLogs) Debug.Log($"SavePoint '{name}': a dialogue is active, ignoring.");
            return;
        }
        if (!IsPlayerFacingMe())
        {
            if (debugLogs) Debug.Log($"SavePoint '{name}': player is NOT facing the save point — turn to face it and try again.");
            return;
        }

        if (debugLogs) Debug.Log($"SavePoint '{name}': opening save menu.");
        menu.Open();
    }

    // Same cardinal facing check as InteractTrigger.
    private bool IsPlayerFacingMe()
    {
        if (PlayerController.I == null) return false;

        Vector2 dir = (Vector2)(transform.position - PlayerController.I.transform.position);
        Vector2 facing = PlayerController.I.FacingDirection;

        Vector2 required;
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            required = new Vector2(Mathf.Sign(dir.x), 0f);
        else
            required = new Vector2(0f, Mathf.Sign(dir.y));

        return Mathf.RoundToInt(facing.x) == Mathf.RoundToInt(required.x) &&
               Mathf.RoundToInt(facing.y) == Mathf.RoundToInt(required.y);
    }
}
