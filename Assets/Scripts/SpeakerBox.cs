using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpeakerBox : MonoBehaviour, ISpeakerBox
{
    // -------------------------------------------------------------------------
    // Inspector References
    // -------------------------------------------------------------------------
    [Header("References")]
    [Tooltip("The RectTransform of the speaker box. Auto-fills from this GameObject if left empty.")]
    public RectTransform speakerRect;

    [Tooltip("The child GameObject that holds the portrait Image. Auto-wires to the first non-background Image child if left empty.")]
    public GameObject portraitObject;

    [Tooltip("Relative uniform scale of the portrait. Use this to shrink the image slightly (e.g., 0.92) to fit nicely within custom parent borders in both normal and focus modes.")]
    [Range(0.5f, 1f)]
    public float portraitScale = 0.92f;

    // -------------------------------------------------------------------------
    // Emotion / image entries
    // -------------------------------------------------------------------------
    [System.Serializable]
    public class EmotionEntry
    {
        [Tooltip("Drag a Sprite here for this emotion.")]
        public Sprite sprite;
    }

    [Header("Emotions")]
    [Tooltip("Drag Sprites here. Index matches the dialogue line index.")]
    public List<EmotionEntry> emotionEntries = new List<EmotionEntry>();

    // -------------------------------------------------------------------------
    // Slide animation
    // -------------------------------------------------------------------------
    [Header("Animation")]
    [Tooltip("Duration of the slide-in animation.")]
    public float showDuration = 0.35f;
    [Tooltip("Duration of the slide-out animation.")]
    public float hideDuration = 0.25f;
    [Tooltip("How far below the screen the speaker starts/ends its slide animation.")]
    public float hiddenOffset = 800f;

    // -------------------------------------------------------------------------
    // Focus state
    // -------------------------------------------------------------------------
    [Header("Focus State")]
    [Tooltip("Uniform scale applied to the speaker box when focused. 1 = no change.")]
    public float focusScale = 1.5f;
    [Tooltip("Optional position offset applied on top of the resting position when focused.")]
    public Vector2 focusPosOffset = Vector2.zero;

    [Tooltip("If true, the speaker enters focus mode automatically when dialogue opens.")]
    public bool startFocused = false;

    // -------------------------------------------------------------------------
    // Dark background (shown during focus)
    // -------------------------------------------------------------------------
    [Header("Focus Background")]
    public Image focusBackground;
    public float backgroundTargetAlpha = 0.6f;
    public float backgroundFadeDuration = 0.2f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private bool isVisible;
    private bool isFocused;

    private Coroutine moveCoroutine;
    private Coroutine scaleCoroutine;
    private Coroutine bgFadeCoroutine;

    // Captured at Awake — the resting anchored position set in the layout.
    private Vector2 originalPos;
    private Vector3 originalLocalScale;

    private Color bgOriginalColor;
    private Image portraitImageComponent;

    // -------------------------------------------------------------------------
    // Awake — wiring and initial state
    // -------------------------------------------------------------------------
    private void Awake()
    {
        if (speakerRect == null)
            speakerRect = GetComponent<RectTransform>();

        // Auto-wire portraitObject to the first direct Image child that isn't the focus background.
        if (portraitObject == null && speakerRect != null)
        {
            foreach (Transform child in speakerRect)
            {
                var img = child.GetComponent<Image>();
                if (img != null && (focusBackground == null || img != focusBackground))
                {
                    portraitObject = child.gameObject;
                    break;
                }
            }
        }

        if (portraitObject != null)
            portraitImageComponent = portraitObject.GetComponent<Image>();

        // Make the portrait stretch to fill speakerRect so it scales with it automatically.
        SetPortraitStretch();

        // Capture the resting position and scale from the layout — same pattern as DialogueBox.
        if (speakerRect != null)
        {
            originalPos = speakerRect.anchoredPosition;
            // Park it off-screen; ShowNormal will slide it back up.
            speakerRect.anchoredPosition = originalPos + new Vector2(0f, -hiddenOffset);
        }
        originalLocalScale = transform.localScale;

        // Start the focus background hidden.
        if (focusBackground != null)
        {
            bgOriginalColor = focusBackground.color;
            var c = bgOriginalColor;
            c.a = 0f;
            focusBackground.color = c;
            focusBackground.gameObject.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    // Sets the portrait to perfectly match the speakerRect, then scales it from the center.
    private void SetPortraitStretch()
    {
        if (portraitObject == null) return;
        var rt = portraitObject.GetComponent<RectTransform>();
        if (rt == null) return;

        // First match the speaker box exactly without any pixel offsets
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Then proportionally shrink from both sides via the middle
        rt.localScale = new Vector3(portraitScale, portraitScale, 1f);
    }

    // -------------------------------------------------------------------------
    // Show / Hide — called by DialogueBox
    // -------------------------------------------------------------------------

    // Slides the speaker in from below to its baked resting position.
    public void ShowNormal()
    {
        if (speakerRect == null) return;

        gameObject.SetActive(true);
        isVisible = true;
        isFocused = false;

        // Apply emotion immediately so the correct portrait shows on the first frame.
        SetEmotionForLine(0);

        Vector2 hiddenPos = originalPos + new Vector2(0f, -hiddenOffset);
        speakerRect.anchoredPosition = hiddenPos;
        transform.localScale = originalLocalScale;

        if (startFocused)
        {
            Vector2 focusedPos = originalPos + focusPosOffset;
            StartMove(hiddenPos, focusedPos, showDuration);
            AnimateScale(originalLocalScale, originalLocalScale * focusScale, showDuration);
            isFocused = true;
            FadeBackgroundIn();
        }
        else
        {
            StartMove(hiddenPos, originalPos, showDuration);
        }
    }

    // Slides the speaker out below and deactivates it.
    public void Hide()
    {
        if (!isVisible || speakerRect == null) return;

        isVisible = false;
        isFocused = false;

        Vector2 startPos = speakerRect.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(0f, -hiddenOffset);
        StartMove(startPos, endPos, hideDuration, () =>
        {
            transform.localScale = originalLocalScale;
            gameObject.SetActive(false);
        });
        AnimateScale(transform.localScale, originalLocalScale, hideDuration);

        FadeBackgroundOut();
    }

    // -------------------------------------------------------------------------
    // Focus mode
    // -------------------------------------------------------------------------

    public void ToggleFocus()
    {
        if (!isVisible) return;
        if (isFocused) ExitFocus();
        else EnterFocus();
    }

    public void EnterFocus()
    {
        if (speakerRect == null) return;

        isFocused = true;
        Vector2 focusedPos = originalPos + focusPosOffset;
        StartMove(speakerRect.anchoredPosition, focusedPos, showDuration);
        AnimateScale(transform.localScale, originalLocalScale * focusScale, showDuration);
        FadeBackgroundIn();
    }

    public void ExitFocus()
    {
        if (speakerRect == null) return;

        isFocused = false;
        StartMove(speakerRect.anchoredPosition, originalPos, showDuration);
        AnimateScale(transform.localScale, originalLocalScale, showDuration);
        FadeBackgroundOut();
    }

    // -------------------------------------------------------------------------
    // Emotion / image selection
    // -------------------------------------------------------------------------

    // Sets the portrait sprite for the given dialogue line index.
    public void SetEmotionForLine(int index)
    {
        if (portraitObject == null) return;

        if (portraitImageComponent == null)
            portraitImageComponent = portraitObject.GetComponent<Image>();

        if (portraitImageComponent == null) return;

        if (emotionEntries == null || emotionEntries.Count == 0 || index < 0 || index >= emotionEntries.Count)
        {
            portraitImageComponent.sprite  = null;
            portraitImageComponent.enabled = false;
            return;
        }

        var entry = emotionEntries[index];
        if (entry == null || entry.sprite == null)
        {
            portraitImageComponent.sprite  = null;
            portraitImageComponent.enabled = false;
            return;
        }

        portraitImageComponent.sprite  = entry.sprite;
        portraitImageComponent.enabled = true;
    }

    // -------------------------------------------------------------------------
    // Movement helpers
    // -------------------------------------------------------------------------

    private void StartMove(Vector2 from, Vector2 to, float duration, System.Action onComplete = null)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(SlideSpeaker(from, to, duration, onComplete));
    }

    // Slides speakerRect from one position to another with an ease-out cubic curve.
    private IEnumerator SlideSpeaker(Vector2 from, Vector2 to, float duration, System.Action onComplete = null)
    {
        duration = Mathf.Max(0.01f, duration);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
            if (speakerRect != null)
                speakerRect.anchoredPosition = Vector2.Lerp(from, to, lerp);
            yield return null;
        }

        if (speakerRect != null)
            speakerRect.anchoredPosition = to;

        onComplete?.Invoke();
    }

    private void AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleSpeaker(from, to, duration));
    }

    // Smoothly scales the speaker's localScale with the same ease-out cubic.
    private IEnumerator ScaleSpeaker(Vector3 from, Vector3 to, float duration)
    {
        duration = Mathf.Max(0.01f, duration);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
            transform.localScale = Vector3.Lerp(from, to, lerp);
            yield return null;
        }

        transform.localScale = to;
    }

    // -------------------------------------------------------------------------
    // Focus background fade helpers
    // -------------------------------------------------------------------------

    private void FadeBackgroundIn()
    {
        if (focusBackground == null) return;
        focusBackground.gameObject.SetActive(true);
        if (bgFadeCoroutine != null) StopCoroutine(bgFadeCoroutine);
        bgFadeCoroutine = StartCoroutine(FadeBackground(0f, backgroundTargetAlpha, false));
    }

    private void FadeBackgroundOut()
    {
        if (focusBackground == null) return;
        if (bgFadeCoroutine != null) StopCoroutine(bgFadeCoroutine);
        bgFadeCoroutine = StartCoroutine(FadeBackground(focusBackground.color.a, 0f, true));
    }

    private IEnumerator FadeBackground(float from, float to, bool disableOnComplete)
    {
        float duration = Mathf.Max(0.01f, backgroundFadeDuration);
        float t = 0f;
        Color c = bgOriginalColor;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            focusBackground.color = c;
            yield return null;
        }

        c.a = to;
        focusBackground.color = c;

        if (disableOnComplete && Mathf.Approximately(to, 0f))
            focusBackground.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // This ensures that when you adjust the slider in the Inspector, the image scales in real-time.
        if (portraitObject != null)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || portraitObject == null) return;
                SetPortraitStretch();
            };
        }
    }
#endif
}
