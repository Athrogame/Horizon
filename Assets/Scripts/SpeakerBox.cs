using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    // No-speaker / question mode
    // -------------------------------------------------------------------------
    [Header("Question Mode")]
    [Tooltip("If true, no portrait is ever shown — the box is identity-less. Options still appear here during questions.")]
    public bool noSpeaker = false;

    [Tooltip("Container inside the speaker box where question options are generated. Should cover the same area as the portrait. Assign in Inspector.")]
    public RectTransform optionsContainer;

    [Tooltip("Font asset used for dynamically generated option labels. Falls back to TMP default if left empty.")]
    public TMP_FontAsset optionFont;

    [Tooltip("Font size for generated option labels.")]
    public float optionFontSize = 40f;

    [Tooltip("Extra gap in pixels between each option row. Row height is derived from font size automatically.")]
    public float optionRowSpacing = 8f;

    [Tooltip("Vertical padding above and below the entire option list in pixels.")]
    public float optionVerticalPadding = 16f;

    [Tooltip("Left padding in pixels added to each option label to leave room for the arrow.")]
    public float optionLabelLeftPadding = 40f;

    [Tooltip("Color of the generated option labels.")]
    public Color optionTextColor = Color.white;

    [Tooltip("Arrow indicator that points at the currently selected option. Should be a child of optionsContainer.")]
    public RectTransform arrowIndicator;

    [Tooltip("Extra vertical offset for the selection arrow on top of the row's geometric center. Positive raises the arrow; negative lowers it. Adjust until the arrow lines up with the highlighted option's text.")]
    public float arrowYOffset = 0f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private bool isVisible;
    private bool isFocused;

    private Coroutine moveCoroutine;
    private Coroutine scaleCoroutine;
    private Coroutine bgFadeCoroutine;

    private Vector2 originalPos;
    private Vector3 originalLocalScale;
    private Vector2 _originalSizeDelta;

    private Color bgOriginalColor;
    private Image portraitImageComponent;

    private List<TextMeshProUGUI> _optionLabels = new List<TextMeshProUGUI>();
    private float _rowHeight;

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

        if (speakerRect != null)
        {
            originalPos = speakerRect.anchoredPosition;
            _originalSizeDelta = speakerRect.sizeDelta;
            speakerRect.anchoredPosition = originalPos + new Vector2(0f, -hiddenOffset);
        }
        originalLocalScale = transform.localScale;

        if (focusBackground != null)
        {
            bgOriginalColor = focusBackground.color;
            var c = bgOriginalColor;
            c.a = 0f;
            focusBackground.color = c;
            focusBackground.gameObject.SetActive(false);
        }

        if (optionsContainer != null)
            optionsContainer.gameObject.SetActive(false);

        if (arrowIndicator != null)
            arrowIndicator.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Show / Hide — called by DialogueBox
    // -------------------------------------------------------------------------

    public void ShowNormal()
    {
        // noSpeaker boxes are invisible during normal dialogue — only appear during questions
        if (noSpeaker) return;
        if (speakerRect == null) return;

        gameObject.SetActive(true);
        isVisible = true;
        isFocused = false;

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

    public void SetEmotionForLine(int index)
    {
        if (noSpeaker) return;
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
    // Question mode
    // -------------------------------------------------------------------------

    // Called by QuestionBox when a question becomes active.
    public void EnterQuestionMode(List<QuestionOption> options)
    {
        if (portraitObject != null)
            portraitObject.SetActive(false);

        foreach (var lbl in _optionLabels)
            if (lbl != null) Destroy(lbl.gameObject);
        _optionLabels.Clear();

        if (optionsContainer == null || options == null || options.Count == 0) return;

        optionsContainer.gameObject.SetActive(true);

        // Row height derived from font; store so SelectOption can use it
        _rowHeight = optionFontSize * 1.4f;
        float contentHeight = _rowHeight * options.Count
                            + optionRowSpacing * Mathf.Max(0, options.Count - 1)
                            + optionVerticalPadding * 2f;

        // Resize to exactly fit content, keeping the bottom edge fixed.
        // Uses rect.height (actual rendered pixels) so it works for both stretched and fixed rects.
        if (speakerRect != null)
        {
            float currentHeight = speakerRect.rect.height;
            float bottomEdge = originalPos.y - currentHeight * speakerRect.pivot.y;
            speakerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            float newAnchorY = bottomEdge + contentHeight * speakerRect.pivot.y;
            speakerRect.anchoredPosition = new Vector2(originalPos.x, newAnchorY);
        }

        // noSpeaker: box was hidden during dialogue — slide in from below to its resting position
        if (noSpeaker && speakerRect != null)
        {
            gameObject.SetActive(true);
            isVisible = true;
            Vector2 restingPos = speakerRect.anchoredPosition;
            Vector2 hiddenPos = restingPos + new Vector2(0f, -hiddenOffset);
            speakerRect.anchoredPosition = hiddenPos;
            StartMove(hiddenPos, restingPos, showDuration);
        }

        // Labels anchor to the bottom of the container and stack upward
        for (int i = 0; i < options.Count; i++)
        {
            var go = new GameObject($"Option_{i}", typeof(RectTransform));
            go.transform.SetParent(optionsContainer, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = options[i].label;
            tmp.color = optionTextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontSize = optionFontSize;
            if (optionFont != null) tmp.font = optionFont;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            // Build upward: option 0 at top, last option sitting on the bottom padding
            float bottomY = optionVerticalPadding + (options.Count - 1 - i) * (_rowHeight + optionRowSpacing);
            rt.anchoredPosition = new Vector2(optionLabelLeftPadding, bottomY);
            rt.sizeDelta = new Vector2(-optionLabelLeftPadding, _rowHeight);

            _optionLabels.Add(tmp);
        }

        if (arrowIndicator != null)
        {
            // Bottom anchor + center pivot so arrow centers on a row using label.y + rowHeight*0.5
            arrowIndicator.anchorMin = new Vector2(arrowIndicator.anchorMin.x, 0f);
            arrowIndicator.anchorMax = new Vector2(arrowIndicator.anchorMax.x, 0f);
            arrowIndicator.pivot = new Vector2(arrowIndicator.pivot.x, 0.5f);
            arrowIndicator.gameObject.SetActive(true);
            SelectOption(0);
        }
    }

    // Called by QuestionBox when the question is answered or force-closed.
    public void ExitQuestionMode()
    {
        foreach (var lbl in _optionLabels)
            if (lbl != null) Destroy(lbl.gameObject);
        _optionLabels.Clear();

        if (optionsContainer != null)
            optionsContainer.gameObject.SetActive(false);

        if (arrowIndicator != null)
            arrowIndicator.gameObject.SetActive(false);

        if (noSpeaker)
        {
            // Box was only shown for this question — slide it out and deactivate
            isVisible = false;
            if (speakerRect != null)
            {
                Vector2 startPos = speakerRect.anchoredPosition;
                StartMove(startPos, startPos + new Vector2(0f, -hiddenOffset), hideDuration, () =>
                {
                    speakerRect.sizeDelta = _originalSizeDelta;
                    speakerRect.anchoredPosition = originalPos;
                    gameObject.SetActive(false);
                });
            }
            return;
        }

        // Restore box to original size and position, re-show portrait
        if (speakerRect != null)
        {
            speakerRect.sizeDelta = _originalSizeDelta;
            speakerRect.anchoredPosition = originalPos;
        }

        if (portraitObject != null)
            portraitObject.SetActive(true);
    }

    // Moves the arrow to vertically center on the option at the given index.
    public void SelectOption(int index)
    {
        if (arrowIndicator == null || _optionLabels.Count == 0) return;
        if (index < 0 || index >= _optionLabels.Count) return;

        float arrowY = _optionLabels[index].rectTransform.anchoredPosition.y
                     + _rowHeight * 0.5f
                     + arrowYOffset;
        arrowIndicator.anchoredPosition = new Vector2(arrowIndicator.anchoredPosition.x, arrowY);
    }

    public int GetOptionCount() => _optionLabels.Count;

    // -------------------------------------------------------------------------
    // Movement helpers
    // -------------------------------------------------------------------------

    private void StartMove(Vector2 from, Vector2 to, float duration, System.Action onComplete = null)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(SlideSpeaker(from, to, duration, onComplete));
    }

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
}
