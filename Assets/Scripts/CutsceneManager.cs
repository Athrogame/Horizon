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
                            yield return StartCoroutine(MoveRoutine(action.targetToMove, action.destinationNode, action.moveSpeed, action.useTweening, action.tweenCurve));
                        else
                            StartCoroutine(MoveRoutine(action.targetToMove, action.destinationNode, action.moveSpeed, action.useTweening, action.tweenCurve));
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
                            // Wait perfectly in limbo until the Dialogue Box has completely finished reading its lines
                            while (dBox.IsDialogueActive())
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
                case CutsceneActionType.SetAnimationBool:
                    if (action.targetAnimator != null && !string.IsNullOrEmpty(action.animationBoolName))
                    {
                        action.targetAnimator.SetBool(action.animationBoolName, action.animationBoolValue);
                    }
                    break;
                case CutsceneActionType.PlayAnimationState:
                    if (action.targetAnimator != null && action.animationClip != null)
                    {
                        action.targetAnimator.Play(action.animationClip.name);
                    }
                    break;

                case CutsceneActionType.SetActive:
                    if (action.targetGameObject != null)
                    {
                        action.targetGameObject.SetActive(action.setActiveState);
                    }
                    else
                    {
                        Debug.LogWarning("Cutscene Action Failed: Missing GameObject to SetActive!");
                    }
                    break;

                case CutsceneActionType.CameraShake:
                    if (action.waitToFinish)
                        yield return StartCoroutine(CameraShakeRoutine(action.shakeDuration, action.shakeMagnitude, action.shakeVirtualCamera));
                    else
                        StartCoroutine(CameraShakeRoutine(action.shakeDuration, action.shakeMagnitude, action.shakeVirtualCamera));
                    break;

                case CutsceneActionType.ChangeCameraTarget:
                    if (action.virtualCamera != null && action.cameraFollowTarget != null)
                    {
                        Component vcam = action.virtualCamera.GetComponent("CinemachineCamera");
                        if (vcam == null) vcam = action.virtualCamera.GetComponent("Unity.Cinemachine.CinemachineCamera");
                        if (vcam == null) vcam = action.virtualCamera.GetComponent("CinemachineVirtualCamera");

                        if (vcam != null)
                        {
                            // Try V2 Properties
                            var propFollow = vcam.GetType().GetProperty("Follow");
                            if (propFollow != null) propFollow.SetValue(vcam, action.cameraFollowTarget, null);
                            
                            var propLookAt = vcam.GetType().GetProperty("LookAt");
                            if (propLookAt != null) propLookAt.SetValue(vcam, action.cameraFollowTarget, null);

                            // Try V3 Struct properties
                            var propTarget = vcam.GetType().GetProperty("Target");
                            if (propTarget != null)
                            {
                                object targetStruct = propTarget.GetValue(vcam); 
                                if (targetStruct != null)
                                {
                                    var fTracking = targetStruct.GetType().GetField("TrackingTarget");
                                    if (fTracking != null) fTracking.SetValue(targetStruct, action.cameraFollowTarget);
                                    
                                    var pTracking = targetStruct.GetType().GetProperty("TrackingTarget");
                                    if (pTracking != null) pTracking.SetValue(targetStruct, action.cameraFollowTarget, null);

                                    var fLookA = targetStruct.GetType().GetField("LookAtTarget");
                                    if (fLookA != null) fLookA.SetValue(targetStruct, action.cameraFollowTarget);
                                    
                                    var pLookA = targetStruct.GetType().GetProperty("LookAtTarget");
                                    if (pLookA != null) pLookA.SetValue(targetStruct, action.cameraFollowTarget, null);

                                    propTarget.SetValue(vcam, targetStruct, null);
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning("ChangeCameraTarget Failed: Could not find CinemachineCamera or CinemachineVirtualCamera component on the provided object.");
                        }
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

    private IEnumerator MoveRoutine(Transform target, Transform dest, float speed, bool useTweening, TweenCurve curve)
    {
        Vector3 startPos = target.position;
        // Enforce strict 2D movement by never altering the character's original Z depth
        Vector3 endPos = new Vector3(dest.position.x, dest.position.y, target.position.z);

        if (useTweening)
        {
            float distance = Vector3.Distance(startPos, endPos);
            if (distance <= 0.001f) yield break;
            
            float duration = distance / speed;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                
                if (curve == TweenCurve.EaseInOut)
                {
                    // Starts slow, speeds up, ends slow
                    t = Mathf.SmoothStep(0f, 1f, t);
                }
                else if (curve == TweenCurve.EaseIn)
                {
                    // Starts slow, ends fast (Quadratic)
                    t = t * t;
                }
                else if (curve == TweenCurve.EaseOut)
                {
                    // Starts fast, ends slow (Quadratic)
                    t = t * (2f - t);
                }
                
                target.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
            target.position = endPos; // Snap perfectly to end to prevent micro-errors
        }
        else
        {
            // Linear movement (no tweening)
            while (Vector3.Distance(target.position, endPos) > 0.05f)
            {
                target.position = Vector3.MoveTowards(target.position, endPos, speed * Time.deltaTime);
                yield return null;
            }
            target.position = endPos; // Snap perfectly to end to prevent micro-errors
        }
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude, GameObject virtualCamera)
    {
        if (virtualCamera == null)
        {
            Debug.LogWarning("CameraShake Failed: No Virtual Camera assigned! Drag your Cinemachine Virtual Camera into the 'Shake Virtual Camera' slot on the CameraShake action.");
            yield break;
        }

        // Support Cinemachine v2 and v3 (Unity package namespace differs)
        Component noiseComp = virtualCamera.GetComponent("CinemachineBasicMultiChannelPerlin");
        if (noiseComp == null)
            noiseComp = virtualCamera.GetComponent("Unity.Cinemachine.CinemachineBasicMultiChannelPerlin");

        if (noiseComp == null)
        {
            Debug.LogWarning($"CameraShake Failed: '{virtualCamera.name}' has no CinemachineBasicMultiChannelPerlin component. " +
                             "Select your Virtual Camera and go Add Component > Cinemachine > Noise > Basic Multi Channel Perlin, then assign a noise profile.");
            yield break;
        }

        // v3 exposes a property called AmplitudeGain; v2 uses the serialized field m_AmplitudeGain
        var ampProp  = noiseComp.GetType().GetProperty("AmplitudeGain");
        var ampField = noiseComp.GetType().GetField("m_AmplitudeGain");

        if (ampProp == null && ampField == null)
        {
            Debug.LogWarning("CameraShake Failed: Could not find AmplitudeGain on the noise component.");
            yield break;
        }

        // Remember the original amplitude so we can restore it cleanly after the shake
        float originalAmplitude = ampProp != null
            ? (float)ampProp.GetValue(noiseComp)
            : (float)ampField.GetValue(noiseComp);

        // Set shake amplitude — Cinemachine reads this every frame, so it works immediately
        if (ampProp  != null) ampProp.SetValue(noiseComp, magnitude);
        else                  ampField.SetValue(noiseComp, magnitude);

        yield return new WaitForSeconds(duration);

        // Restore the original amplitude when done
        if (ampProp  != null) ampProp.SetValue(noiseComp, originalAmplitude);
        else                  ampField.SetValue(noiseComp, originalAmplitude);
    }
}
