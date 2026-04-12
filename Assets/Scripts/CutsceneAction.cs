using UnityEngine;
using System.Collections.Generic;

public enum CutsceneActionType
{
    Wait,
    MoveToTransform,
    ShowDialogue,
    SetAnimationTrigger,
    TeleportObject
}

[System.Serializable]
public class CutsceneAction
{
    [Tooltip("Select what type of action this is")]
    public CutsceneActionType actionType;

    [Tooltip("If checked, pauses the cutscene until this action finishes. If unchecked, the next block triggers instantly so they happen simultaneously!")]
    public bool waitToFinish = true;

    [Header("Wait Settings")]
    [Tooltip("How many seconds to pause the sequence before moving to the next action")]
    public float waitDuration = 1f;

    [Header("Move Settings")]
    [Tooltip("The object you want to move (e.g. Player, NPC)")]
    public Transform targetToMove;
    [Tooltip("The destination waypoint it should walk to")]
    public Transform destinationNode;
    [Tooltip("Speed to move from A to B")]
    public float moveSpeed = 5f;

    [Header("Dialogue Settings")]
    [Tooltip("Leave empty to automatically find your main DialogueBox")]
    public DialogueBox dialogueBox;
    public List<DialogueLine> dialogueLines;

    [Header("Animation Settings")]
    [Tooltip("The character's Animator component")]
    public Animator targetAnimator;
    [Tooltip("The exact string name of the Trigger inside the animator (e.g. 'Jump')")]
    public string animationTriggerName;
}
