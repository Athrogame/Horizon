using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Plays a sequenced list of CutsceneActions top-to-bottom.
// Can be triggered on Start, on Enable, or by calling StartCutscene() from another script or UnityEvent.
public class CutsceneManager : MonoBehaviour
{
    [Tooltip("Plays automatically when the scene loads.")]
    public bool playOnStart = false;

    [Tooltip("Plays automatically whenever this GameObject is activated.")]
    public bool playOnEnable = false;

    [Tooltip("This cutscene will never play more than once, even across scene reloads.")]
    public bool playOnlyOnce = false;

    [Tooltip("Unique save key for the 'Play Only Once' flag. Must be unique across all scenes (e.g. 'Forest_IntroCutscene').")]
    public string cutsceneID = "";

    [Tooltip("The list of actions that make up this cutscene. Executed top-to-bottom.")]
    public List<CutsceneAction> cutsceneActions = new List<CutsceneAction>();

    private Coroutine activeRoutine;

    // Prefixed so old un-prefixed PlayerPrefs values left over from prior dev builds
    // are orphaned and the release build starts from a clean slate. Dev/editor uses
    // a separate prefix so a dev playthrough can never write to the release namespace.
    private string Key =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        "dev_cutscene_" + cutsceneID;
#else
        "cutscene_" + cutsceneID;
#endif

    private void Start()
    {
        Debug.Log($"[Cutscene/{cutsceneID}] Start fired. playOnStart={playOnStart} playOnEnable={playOnEnable} active={gameObject.activeInHierarchy} enabled={enabled}");
        // Guard against double-fire if both flags are set.
        if (playOnStart && !playOnEnable)
            StartCoroutine(DelayedStart());
    }

    private void OnEnable()
    {
        Debug.Log($"[Cutscene/{cutsceneID}] OnEnable fired. playOnEnable={playOnEnable}");
        if (playOnEnable)
            StartCoroutine(DelayedStart());
    }

    // Wait until PlayerController.I exists before starting. On first scene load in
    // standalone builds, the player's Awake can fire after this script's Start, so
    // a single-frame delay isn't enough — the cutscene would silently no-op the
    // movement lock / idle calls. We cap the wait so a missing player can't hang us.
    private IEnumerator DelayedStart()
    {
        Debug.Log($"[Cutscene/{cutsceneID}] DelayedStart waiting for PlayerController.I...");
        const int maxFrames = 60;
        int waited = 0;
        for (int i = 0; i < maxFrames; i++)
        {
            if (PlayerController.I != null) break;
            waited++;
            yield return null;
        }
        Debug.Log($"[Cutscene/{cutsceneID}] DelayedStart done waiting. framesWaited={waited} playerFound={(PlayerController.I != null)}");
        // One extra frame so everything else's Start has fired too.
        yield return null;
        StartCutscene();
    }

    // Flag key stored in the save file when this cutscene has played (per-save, survives loads).
    private string CutsceneFlag => "cutscene_" + cutsceneID;

    // True if this play-only-once cutscene has already been seen in the current playthrough.
    // Prefers the save system (per-save, survives loading an old save); falls back to PlayerPrefs
    // only when no SaveManager exists.
    private bool HasPlayedCutscene()
    {
        if (string.IsNullOrEmpty(cutsceneID)) return false;
        if (SaveManager.I != null) return SaveManager.I.HasFlag(CutsceneFlag);
        return PlayerPrefs.GetInt(Key, 0) == 1;
    }

    // Begins the cutscene. Call this from a trigger collider, UnityEvent, or another script.
    public void StartCutscene()
    {
        Debug.Log($"[Cutscene/{cutsceneID}] StartCutscene called. playOnlyOnce={playOnlyOnce} played={HasPlayedCutscene()}");

        if (playOnlyOnce)
        {
            if (string.IsNullOrEmpty(cutsceneID))
            {
                Debug.LogWarning($"[CutsceneManager] '{gameObject.name}' has Play Only Once enabled but no Cutscene ID set — it will always replay.");
            }
            else if (HasPlayedCutscene())
            {
                Debug.Log($"[Cutscene/{cutsceneID}] SKIPPING — already played. Re-applying SetActive state.");
                ApplyPersistentState();
                return; // Already played — skip.
            }
        }

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        Debug.Log($"[Cutscene/{cutsceneID}] Starting PlayCutsceneRoutine. actionCount={cutsceneActions?.Count ?? 0}");
        activeRoutine = StartCoroutine(PlayCutsceneRoutine());
    }

    // Clears the Play Only Once save flag so this cutscene can play again.
    public void ResetCutscene()
    {
        if (!string.IsNullOrEmpty(cutsceneID))
        {
            if (SaveManager.I != null) SaveManager.I.ClearFlag(CutsceneFlag);
            PlayerPrefs.DeleteKey(Key);
        }
    }

    // Re-applies the world-state actions from this cutscene without playing it.
    // Called when a Play-Only-Once cutscene is being skipped on re-entry, so anything
    // it had toggled on/off (or teleported) stays that way after a scene reload.
    private void ApplyPersistentState()
    {
        foreach (CutsceneAction action in cutsceneActions)
        {
            switch (action.actionType)
            {
                case CutsceneActionType.SetActive:
                    if (action.targetGameObject != null)
                        action.targetGameObject.SetActive(action.setActiveState);
                    break;

                case CutsceneActionType.TeleportObject:
                    if (action.targetToMove != null && action.destinationNode != null)
                    {
                        Vector3 dest = new Vector3(action.destinationNode.position.x, action.destinationNode.position.y, action.targetToMove.position.z);
                        Rigidbody2D rb = action.targetToMove.GetComponent<Rigidbody2D>();
                        if (rb != null)
                        {
                            rb.position = dest;
                            rb.linearVelocity = Vector2.zero;
                        }
                        action.targetToMove.position = dest;
                    }
                    break;

                case CutsceneActionType.MoveToTransform:
                    // The cutscene's net effect on position is that the target ends at the destination.
                    if (action.targetToMove != null && action.destinationNode != null)
                    {
                        Vector3 dest = new Vector3(action.destinationNode.position.x, action.destinationNode.position.y, action.targetToMove.position.z);
                        Rigidbody2D rb = action.targetToMove.GetComponent<Rigidbody2D>();
                        if (rb != null)
                        {
                            rb.position = dest;
                            rb.linearVelocity = Vector2.zero;
                        }
                        action.targetToMove.position = dest;
                    }
                    break;
            }
        }
    }

    private IEnumerator PlayCutsceneRoutine()
    {
        if (PlayerController.I != null)
        {
            PlayerController.I.LockMovement();
            PlayerController.I.ForceIdle();
        }

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
                    else Debug.LogWarning("[CutsceneManager] MoveToTransform: missing target or destination.");
                    break;

                case CutsceneActionType.TeleportObject:
                    if (action.targetToMove != null && action.destinationNode != null)
                    {
                        // Preserve Z so the object stays on the correct depth layer.
                        Vector3 dest = new Vector3(action.destinationNode.position.x, action.destinationNode.position.y, action.targetToMove.position.z);

                        // Warp the Rigidbody2D directly so physics doesn't interpolate to the new position.
                        Rigidbody2D rb = action.targetToMove.GetComponent<Rigidbody2D>();
                        if (rb != null)
                        {
                            rb.position = dest;
                            rb.linearVelocity = Vector2.zero;
                        }

                        action.targetToMove.position = dest;
                    }
                    else Debug.LogWarning("[CutsceneManager] TeleportObject: missing target or destination.");
                    break;

                case CutsceneActionType.ShowDialogue:
                    DialogueBox dBox = action.dialogueBox;
                    if (dBox == null)
                        dBox = Object.FindAnyObjectByType<DialogueBox>(FindObjectsInactive.Include);

                    if (dBox != null)
                    {
                        if (!dBox.gameObject.activeSelf)
                            dBox.gameObject.SetActive(true);

                        bool hasCustomLines = action.dialogueLines != null && action.dialogueLines.Count > 0;
                        bool hasBoxLines    = dBox.dialogueLines    != null && dBox.dialogueLines.Count > 0;

                        if (hasCustomLines)
                            dBox.ShowDialogue(action.dialogueLines);
                        else if (hasBoxLines)
                            dBox.StartDialogue();
                        else
                        {
                            Debug.LogWarning("[CutsceneManager] ShowDialogue: no lines found on the action or the DialogueBox.");
                            break;
                        }

                        if (action.waitToFinish)
                            while (dBox.IsDialogueActive())
                                yield return null;
                    }
                    else Debug.LogWarning("[CutsceneManager] ShowDialogue: no DialogueBox found.");
                    break;

                case CutsceneActionType.SetAnimationTrigger:
                    if (action.targetAnimator != null && !string.IsNullOrEmpty(action.animationTriggerName))
                        action.targetAnimator.SetTrigger(action.animationTriggerName);
                    break;

                case CutsceneActionType.SetAnimationBool:
                    if (action.targetAnimator != null && !string.IsNullOrEmpty(action.animationBoolName))
                        action.targetAnimator.SetBool(action.animationBoolName, action.animationBoolValue);
                    break;

                case CutsceneActionType.PlayAnimationState:
                    if (action.targetAnimator != null && action.animationClip != null)
                        action.targetAnimator.Play(action.animationClip.name);
                    break;

                case CutsceneActionType.SetActive:
                    if (action.targetGameObject != null)
                        action.targetGameObject.SetActive(action.setActiveState);
                    else Debug.LogWarning("[CutsceneManager] SetActive: no GameObject assigned.");
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
                        // Support Cinemachine v2 and v3 via reflection.
                        Component vcam = action.virtualCamera.GetComponent("CinemachineCamera")
                                      ?? action.virtualCamera.GetComponent("Unity.Cinemachine.CinemachineCamera")
                                      ?? action.virtualCamera.GetComponent("CinemachineVirtualCamera");

                        if (vcam != null)
                        {
                            // Cinemachine v2: Follow / LookAt properties.
                            vcam.GetType().GetProperty("Follow")?.SetValue(vcam, action.cameraFollowTarget, null);
                            vcam.GetType().GetProperty("LookAt")?.SetValue(vcam, action.cameraFollowTarget, null);

                            // Cinemachine v3: nested Target struct with TrackingTarget / LookAtTarget.
                            var propTarget = vcam.GetType().GetProperty("Target");
                            if (propTarget != null)
                            {
                                object t = propTarget.GetValue(vcam);
                                if (t != null)
                                {
                                    t.GetType().GetField("TrackingTarget")?.SetValue(t, action.cameraFollowTarget);
                                    t.GetType().GetProperty("TrackingTarget")?.SetValue(t, action.cameraFollowTarget, null);
                                    t.GetType().GetField("LookAtTarget")?.SetValue(t, action.cameraFollowTarget);
                                    t.GetType().GetProperty("LookAtTarget")?.SetValue(t, action.cameraFollowTarget, null);
                                    propTarget.SetValue(vcam, t, null);
                                }
                            }
                        }
                        else Debug.LogWarning("[CutsceneManager] ChangeCameraTarget: no Cinemachine camera found on the assigned object.");
                    }
                    break;
            }
        }

        // Force movement fully unlocked at the end regardless of any mid-cutscene lock imbalance.
        if (PlayerController.I != null)
            PlayerController.I.ForceUnlockMovement();

        if (playOnlyOnce && !string.IsNullOrEmpty(cutsceneID))
        {
            // Record in the save system (per-save, survives loads) and PlayerPrefs (legacy fallback).
            if (SaveManager.I != null)
                SaveManager.I.SetFlag(CutsceneFlag);
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator MoveRoutine(Transform target, Transform dest, float speed, bool useTweening, TweenCurve curve)
    {
        Vector3 startPos = target.position;
        // Preserve Z depth so 2D objects don't shift behind the background.
        Vector3 endPos = new Vector3(dest.position.x, dest.position.y, target.position.z);

        if (useTweening)
        {
            float distance = Vector3.Distance(startPos, endPos);
            if (distance <= 0.001f) yield break;

            float duration    = distance / speed;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                t = curve == TweenCurve.EaseInOut ? Mathf.SmoothStep(0f, 1f, t)
                  : curve == TweenCurve.EaseIn    ? t * t
                  : curve == TweenCurve.EaseOut   ? t * (2f - t)
                  : t;

                target.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
        }
        else
        {
            while (Vector3.Distance(target.position, endPos) > 0.05f)
            {
                target.position = Vector3.MoveTowards(target.position, endPos, speed * Time.deltaTime);
                yield return null;
            }
        }

        target.position = endPos;
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude, GameObject virtualCamera)
    {
        if (virtualCamera == null)
        {
            Debug.LogWarning("[CameraShake] No Virtual Camera assigned.");
            yield break;
        }

        // Support Cinemachine v2 and v3 component names.
        Component noise = virtualCamera.GetComponent("CinemachineBasicMultiChannelPerlin")
                       ?? virtualCamera.GetComponent("Unity.Cinemachine.CinemachineBasicMultiChannelPerlin");

        if (noise == null)
        {
            Debug.LogWarning($"[CameraShake] '{virtualCamera.name}' has no CinemachineBasicMultiChannelPerlin component.");
            yield break;
        }

        // Locate AmplitudeGain and FrequencyGain via reflection to support both Cinemachine versions.
        var ampProp        = noise.GetType().GetProperty("AmplitudeGain");
        var ampField       = noise.GetType().GetField("AmplitudeGain");
        var legacyAmpField = noise.GetType().GetField("m_AmplitudeGain");

        var freqProp        = noise.GetType().GetProperty("FrequencyGain");
        var freqField       = noise.GetType().GetField("FrequencyGain");
        var legacyFreqField = noise.GetType().GetField("m_FrequencyGain");

        if (ampProp == null && ampField == null && legacyAmpField == null)
        {
            Debug.LogWarning("[CameraShake] Could not find AmplitudeGain on the noise component.");
            yield break;
        }

        // Read original values so they can be restored after the shake.
        float originalAmp  = ampProp  != null ? (float)ampProp.GetValue(noise)
                           : ampField != null ? (float)ampField.GetValue(noise)
                           : (float)legacyAmpField.GetValue(noise);

        float originalFreq = freqProp  != null ? (float)freqProp.GetValue(noise)
                           : freqField != null ? (float)freqField.GetValue(noise)
                           : legacyFreqField != null ? (float)legacyFreqField.GetValue(noise)
                           : 1f;

        // Apply shake values.
        float runFreq = originalFreq == 0f ? 1.5f : originalFreq;
        SetNoiseValue(noise, ampProp,  ampField,  legacyAmpField,  magnitude);
        SetNoiseValue(noise, freqProp, freqField, legacyFreqField, runFreq);

        yield return new WaitForSeconds(duration);

        // Restore original values.
        SetNoiseValue(noise, ampProp,  ampField,  legacyAmpField,  originalAmp);
        SetNoiseValue(noise, freqProp, freqField, legacyFreqField, originalFreq);
    }

    // Helper to set a float value on a noise component regardless of whether it's a property or field.
    private void SetNoiseValue(Component noise, System.Reflection.PropertyInfo prop, System.Reflection.FieldInfo field, System.Reflection.FieldInfo legacyField, float value)
    {
        if (prop        != null) prop.SetValue(noise, value, null);
        else if (field  != null) field.SetValue(noise, value);
        else legacyField?.SetValue(noise, value);
    }
}
