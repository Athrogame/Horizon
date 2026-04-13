using UnityEngine;
using System.Collections.Generic;
public enum TweenCurve
{
    EaseInOut,
    EaseIn,
    EaseOut
}

public enum CutsceneActionType
{
    Wait,
    MoveToTransform,
    ShowDialogue,
    SetAnimationTrigger,
    SetAnimationBool,
    PlayAnimationState,
    TeleportObject,
    SetActive,
    CameraShake,
    ChangeCameraTarget
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
    [Tooltip("If true, allows applying a mathematical curve to the movement instead of linear")]
    public bool useTweening = false;
    [Tooltip("Which type of ease to apply to the motion")]
    public TweenCurve tweenCurve = TweenCurve.EaseInOut;

    [Header("Dialogue Settings")]
    [Tooltip("Leave empty to automatically find your main DialogueBox")]
    public DialogueBox dialogueBox;
    public List<DialogueLine> dialogueLines;

    [Header("Animation Settings")]
    [Tooltip("The character's Animator component")]
    public Animator targetAnimator;
    [Tooltip("The exact string name of the Trigger inside the animator (e.g. 'Jump')")]
    public string animationTriggerName;
    [Tooltip("The exact string name of the Bool inside the animator")]
    public string animationBoolName;
    [Tooltip("The value to set the boolean to")]
    public bool animationBoolValue = true;
    [Tooltip("Drag the Animation Clip here to play it directly (Note: Its state in the Animator must have the same name)")]
    public AnimationClip animationClip;

    [Header("Set Active Settings")]
    [Tooltip("The object to enable or disable")]
    public GameObject targetGameObject;
    [Tooltip("Check to activate the object, uncheck to deactivate it")]
    public bool setActiveState = true;

    [Header("Camera Shake Settings")]
    [Tooltip("The Cinemachine Virtual Camera that has a CinemachineBasicMultiChannelPerlin noise component on it")]
    public GameObject shakeVirtualCamera;
    [Tooltip("How long the camera should shake (in seconds)")]
    public float shakeDuration = 0.5f;
    [Tooltip("How violent the shake is — this sets the noise AmplitudeGain temporarily")]
    public float shakeMagnitude = 0.3f;

    [Header("Cinemachine Settings")]
    [Tooltip("The Cinemachine Virtual Camera (drag from Hierarchy)")]
    public GameObject virtualCamera;
    [Tooltip("The object you want the camera to focus on and follow instead")]
    public Transform cameraFollowTarget;
}
