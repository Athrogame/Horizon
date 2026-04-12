using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CutsceneManager : MonoBehaviour
{
    [Tooltip("If checked, plays automatically when the game starts/object loads")]
    public bool playOnStart = false;

    [Tooltip("If checked, plays automatically when the GameObject is turned from Inactive to Active")]
    public bool playOnEnable = false;

    [Tooltip("Add actions to this list to build your cutscene top-to-bottom!")]
    public List<CutsceneAction> cutsceneActions = new List<CutsceneAction>();

    private Coroutine activeRoutine;

    private void Start()
    {
        if (playOnStart && !playOnEnable) // Prevent double-fire if both are checked
        {
            StartCoroutine(DelayedStart());
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            StartCoroutine(DelayedStart());
        }
    }

    private IEnumerator DelayedStart()
    {
        // One frame grace period to let standard Awake/Start logic fully boot up in Unity (e.g. PlayerController)
        yield return new WaitForEndOfFrame();
        StartCutscene();
    }

    /// <summary>
    /// Call this from a Trigger Collider or another script to begin the cutscene sequence!
    /// </summary>
    public void StartCutscene()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(PlayCutsceneRoutine());
    }

    private IEnumerator PlayCutsceneRoutine()
    {
        // 1. Lock the player's movement while the cutscene has taken control
        if (PlayerController.I != null)
        {
            PlayerController.I.movementLocked = true;
            PlayerController.I.ForceIdle();
        }

        // 2. Play exactly through the sequenced actions
        foreach (CutsceneAction action in cutsceneActions)
        {
            switch (action.actionType)
            {
                case CutsceneActionType.Wait:
                    if (action.waitToFinish)
                        yield return new WaitForSeconds(action.waitDuration);
                    break;

                case CutsceneActionType.MoveToTransform:
                    if (action.targetToMove != null && action.destinationNode != null)
                    {
                        if (action.waitToFinish)
                            yield return StartCoroutine(MoveRoutine(action.targetToMove, action.destinationNode, action.moveSpeed));
                        else
                            StartCoroutine(MoveRoutine(action.targetToMove, action.destinationNode, action.moveSpeed));
                    }
                    else
                    {
                        Debug.LogWarning("Cutscene Action Failed: Missing Move Targets!");
                    }
                    break;

                case CutsceneActionType.TeleportObject:
                    if (action.targetToMove != null && action.destinationNode != null)
                    {
                        // 1. We MUST preserve their current Z-depth so they don't vanish behind the 2D background tilemap!
                        Vector3 dest = new Vector3(action.destinationNode.position.x, action.destinationNode.position.y, action.targetToMove.position.z);
                        
                        // 2. If it's a physics object (like the Player), we MUST tell the rigid body to warp and kill its velocity!
                        Rigidbody2D rb = action.targetToMove.GetComponent<Rigidbody2D>();
                        if (rb != null)
                        {
                            rb.position = dest;
                            rb.linearVelocity = Vector2.zero;
                        }

                        action.targetToMove.position = dest;
                    }
                    else
                    {
                        Debug.LogWarning("Cutscene Action Failed: Missing Teleport Targets!");
                    }
                    break;

                case CutsceneActionType.ShowDialogue:
                    DialogueBox dBox = action.dialogueBox;
                    // Automatically find the DialogueBox if the developer forgot to manually drag it into the inspector slot
                    if (dBox == null)
                    {
                        dBox = FindObjectOfType<DialogueBox>(true);
                    }

                    if (dBox != null)
                    {
                        // Ensure the Dialogue Box is forced active if the developer had it turned off in the hierarchy!
                        if (!dBox.gameObject.activeSelf)
                            dBox.gameObject.SetActive(true);

                        bool hasCustomLines = action.dialogueLines != null && action.dialogueLines.Count > 0;
                        bool hasBoxLines = dBox.dialogueLines != null && dBox.dialogueLines.Count > 0;

                        if (hasCustomLines)
                        {
                            // If the cutscene action has custom lines typed into it, play those!
                            dBox.ShowDialogue(action.dialogueLines);
                        }
                        else if (hasBoxLines)
                        {
                            // If the cutscene action has no lines, but the Dialogue Box ALREADY has lines, play the box's lines!
                            dBox.StartDialogue();
                        }
                        else
                        {
                            Debug.LogWarning("Cutscene Action failed: No dialogue lines were found in the Action OR the Dialogue Box!");
                            break;
                        }
                        
                        if (action.waitToFinish)
                        {
                            // Wait perfectly in limbo until the Dialogue Box has completely finished and vanished from the screen
                            while (dBox.gameObject.activeSelf)
                            {
                                yield return null;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Cutscene Action missing Dialogue Box!");
                    }
                    break;

                case CutsceneActionType.SetAnimationTrigger:
                    if (action.targetAnimator != null && !string.IsNullOrEmpty(action.animationTriggerName))
                    {
                        action.targetAnimator.SetTrigger(action.animationTriggerName);
                    }
                    break;
            }
        }

        // 3. The cutscene is over, yield control back to the player!
        if (PlayerController.I != null)
        {
            PlayerController.I.movementLocked = false;
        }
    }

    private IEnumerator MoveRoutine(Transform target, Transform dest, float speed)
    {
        // Enforce strict 2D movement by never altering the character's original Z depth
        Vector3 endPos = new Vector3(dest.position.x, dest.position.y, target.position.z);

        // Smoothly lerp towards position until extremely close
        while (Vector3.Distance(target.position, endPos) > 0.05f)
        {
            target.position = Vector3.MoveTowards(target.position, endPos, speed * Time.deltaTime);
            yield return null;
        }
        target.position = endPos; // Snap perfectly to end to prevent micro-errors
    }
}
