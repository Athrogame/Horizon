using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CutsceneManager : MonoBehaviour
{
    [Tooltip("If checked, plays automatically when the game starts/object loads")]
    public bool playOnStart = false;

    [Tooltip("If checked, plays automatically when the GameObject is turned from Inactive to Active")]
    public bool playOnEnable = false;

    [Tooltip("If checked, this cutscene will never play more than once — even if you leave and re-enter the room")]
    public bool playOnlyOnce = false;

    [Tooltip("A unique ID used to remember if this cutscene has already played. MUST be unique across all scenes! (e.g. 'Scene1_IntroCutscene')")]
    public string cutsceneID = "";

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
        if (playOnlyOnce)
        {
            if (string.IsNullOrEmpty(cutsceneID))
            {
                Debug.LogWarning($"[CutsceneManager] '{gameObject.name}' has 'Play Only Once' checked but no Cutscene ID set! The cutscene will always replay. Please set a unique Cutscene ID in the Inspector.");
            }
            else if (PlayerPrefs.GetInt(cutsceneID, 0) == 1)
            {
                // Already played before — skip it entirely
                return;
            }
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(PlayCutsceneRoutine());
    }

    /// <summary>
    /// Resets this cutscene so it can play again (clears the PlayerPrefs flag).
    /// </summary>
    public void ResetCutscene()
    {
        if (!string.IsNullOrEmpty(cutsceneID))
            PlayerPrefs.DeleteKey(cutsceneID);
    }

    private IEnumerator PlayCutsceneRoutine()
    {
        // 1. Lock the player's movement while the cutscene has taken control
        if (PlayerController.I != null)
        {
            PlayerController.I.LockMovement();
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
            PlayerController.I.UnlockMovement();
        }

        // 4. If this cutscene should only play once, save that it has been seen
        if (playOnlyOnce && !string.IsNullOrEmpty(cutsceneID))
        {
            PlayerPrefs.SetInt(cutsceneID, 1);
            PlayerPrefs.Save();
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
        Debug.Log($"[CameraShake] Starting shake. Duration: {duration}, Magnitude: {magnitude}, Target Camera: {(virtualCamera != null ? virtualCamera.name : "NULL")}");

        if (virtualCamera == null)
        {
            Debug.LogWarning("[CameraShake] Failed: No Virtual Camera assigned!");
            yield break;
        }

        /* 
        // WORKAROUND: Find and disable ALL Pixel Perfect Camera components (both VCam and Main Camera)
        List<Behaviour> pixelPerfectComponents = new List<Behaviour>();

        Behaviour ppVcam1 = virtualCamera.GetComponent("UnityEngine.U2D.PixelPerfectCamera") as Behaviour;
        if (ppVcam1 != null) pixelPerfectComponents.Add(ppVcam1);
        Behaviour ppVcam2 = virtualCamera.GetComponent("PixelPerfectCamera") as Behaviour;
        if (ppVcam2 != null && !pixelPerfectComponents.Contains(ppVcam2)) pixelPerfectComponents.Add(ppVcam2);

        if (Camera.main != null)
        {
            Behaviour ppMain1 = Camera.main.GetComponent("UnityEngine.U2D.PixelPerfectCamera") as Behaviour;
            if (ppMain1 != null && !pixelPerfectComponents.Contains(ppMain1)) pixelPerfectComponents.Add(ppMain1);
            Behaviour ppMain2 = Camera.main.GetComponent("PixelPerfectCamera") as Behaviour;
            if (ppMain2 != null && !pixelPerfectComponents.Contains(ppMain2)) pixelPerfectComponents.Add(ppMain2);
        }

        List<Behaviour> disabledPPs = new List<Behaviour>();
        foreach (var pp in pixelPerfectComponents)
        {
            if (pp != null && pp.enabled)
            {
                pp.enabled = false;
                disabledPPs.Add(pp);
                Debug.Log($"[CameraShake] WORKAROUND: Temporarily disabled '{pp.GetType().Name}' on '{pp.gameObject.name}'!");
            }
        }
        */

        // Support Cinemachine v2 and v3
        Component noiseComp = virtualCamera.GetComponent("CinemachineBasicMultiChannelPerlin");
        if (noiseComp == null)
            noiseComp = virtualCamera.GetComponent("Unity.Cinemachine.CinemachineBasicMultiChannelPerlin");

        if (noiseComp == null)
        {
            Debug.LogWarning($"[CameraShake] Failed: '{virtualCamera.name}' has no CinemachineBasicMultiChannelPerlin component.");
            yield break;
        }

        Debug.Log($"[CameraShake] Found Noise Component on '{virtualCamera.name}'! Analyzing...");

        var ampProp = noiseComp.GetType().GetProperty("AmplitudeGain");
        var ampField = noiseComp.GetType().GetField("AmplitudeGain");
        var legacyAmpField = noiseComp.GetType().GetField("m_AmplitudeGain");

        var profileField = noiseComp.GetType().GetField("NoiseProfile");
        var legacyProfileField = noiseComp.GetType().GetField("m_NoiseProfile");
        var profileLegacy2 = noiseComp.GetType().GetField("m_Definition"); // older versions
        
        object profile = null;
        if (profileField != null) profile = profileField.GetValue(noiseComp);
        else if (legacyProfileField != null) profile = legacyProfileField.GetValue(noiseComp);
        else if (profileLegacy2 != null) profile = profileLegacy2.GetValue(noiseComp);

        if (profile == null)
        {
            Debug.LogWarning($"[CameraShake] Failed: The noise component on '{virtualCamera.name}' has no Noise Profile! (It must hold something like '6D Shake').");
        }
        else
        {
            Debug.Log($"[CameraShake] Verified valid Noise Profile: {profile.ToString()}");
        }

        if (ampProp == null && ampField == null && legacyAmpField == null)
        {
            Debug.LogWarning("[CameraShake] Failed: Could not find AmplitudeGain on the noise component.");
            yield break;
        }

        var freqProp = noiseComp.GetType().GetProperty("FrequencyGain");
        var freqField = noiseComp.GetType().GetField("FrequencyGain");
        var legacyFreqField = noiseComp.GetType().GetField("m_FrequencyGain");

        // Remember the original amplitude so we can restore it cleanly
        float originalAmplitude = 0f;
        if (ampProp != null) originalAmplitude = (float)ampProp.GetValue(noiseComp);
        else if (ampField != null) originalAmplitude = (float)ampField.GetValue(noiseComp);
        else if (legacyAmpField != null) originalAmplitude = (float)legacyAmpField.GetValue(noiseComp);

        float originalFrequency = 1f;
        if (freqProp != null) originalFrequency = (float)freqProp.GetValue(noiseComp);
        else if (freqField != null) originalFrequency = (float)freqField.GetValue(noiseComp);
        else if (legacyFreqField != null) originalFrequency = (float)legacyFreqField.GetValue(noiseComp);

        Debug.Log($"[CameraShake] Original properties -> Amplitude: {originalAmplitude}, Frequency: {originalFrequency}");

        // Set shake amplitude
        if (ampProp != null) ampProp.SetValue(noiseComp, magnitude);
        else if (ampField != null) ampField.SetValue(noiseComp, magnitude);
        else if (legacyAmpField != null) legacyAmpField.SetValue(noiseComp, magnitude);

        // Guarantee a reasonable frequency
        float runFreq = originalFrequency == 0f ? 1.5f : originalFrequency;
        if (freqProp != null) freqProp.SetValue(noiseComp, runFreq);
        else if (freqField != null) freqField.SetValue(noiseComp, runFreq);
        else if (legacyFreqField != null) legacyFreqField.SetValue(noiseComp, runFreq);

        Debug.Log($"[CameraShake] Set target -> Amplitude: {magnitude}, Frequency: {runFreq}. Waiting {duration}s...");

        yield return new WaitForSeconds(duration);

        Debug.Log("[CameraShake] Duration finished! Reverting to original values.");

        // Restore the original amplitude when done
        if (ampProp != null) ampProp.SetValue(noiseComp, originalAmplitude);
        else if (ampField != null) ampField.SetValue(noiseComp, originalAmplitude);
        else if (legacyAmpField != null) legacyAmpField.SetValue(noiseComp, originalAmplitude);

        if (freqProp != null) freqProp.SetValue(noiseComp, originalFrequency);
        else if (freqField != null) freqField.SetValue(noiseComp, originalFrequency);
        else if (legacyFreqField != null) legacyFreqField.SetValue(noiseComp, originalFrequency);

        /*
        // WORKAROUND: Turn Pixel Perfect scripts back on!
        foreach (var pp in disabledPPs)
        {
            if (pp != null)
            {
                pp.enabled = true;
                Debug.Log($"[CameraShake] WORKAROUND: Shake finished. Re-enabled Pixel Perfect Camera on {pp.gameObject.name}.");
            }
        }
        */
    }
}
