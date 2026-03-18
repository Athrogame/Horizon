using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpeakerBox : MonoBehaviour
{
    [Header("Required References")]
    public RectTransform speakerRect;
    // Drag the child GameObject that holds the portrait Image here.
    // The script will read its Image component to swap emotion sprites.
    public GameObject portraitObject;
    // Optional: if set, this RectTransform will be kept in sync
    // with the speakerRect so the \"box\" and \"image\" always share
    // the same rect (position + size) at runtime.
    public RectTransform speakerImageRect;
    public List<Sprite> emotionSprites = new List<Sprite>();

    [Header("Normal (unfocused) state")]
    public bool useNormalForcedRect = true;
    public Vector2 normalPos = new Vector2(0, 325);
    public Vector2 normalSize = new Vector2(350, 350);
    public float showDuration = 0.35f;
    public float hideDuration = 0.25f;
    public float hiddenOffset = 800f;

    [Header("Focus state")]
    public bool useFocusForcedRect = true;
    public Vector2 focusPos = new Vector2(0, 500);
    public Vector2 focusSize = new Vector2(600, 600);
    // If true, the speaker box will enter focus mode automatically when dialogue opens.
    public bool startFocused = false;

    [Header("Dark background")]
    public Image focusBackground;
    public float backgroundTargetAlpha = 0.6f;
    public float backgroundFadeDuration = 0.2f;

    private bool isVisible = false;
    private bool isFocused = false;

    private Coroutine moveCoroutine;
    private Coroutine bgFadeCoroutine;

    private Vector2 cachedNormalPos;
    private Vector2 cachedNormalSize;

    private Color bgOriginalColor;

    private void Awake()
    {
        if (speakerRect == null)
            speakerRect = GetComponent<RectTransform>();

        // Auto-wire the portrait object and its rect if not explicitly assigned.
        if (portraitObject == null && speakerRect != null)
        {
            // Find the first Image on a child of the speaker and use its GameObject.
            var img = speakerRect.GetComponentInChildren<Image>(true);
            if (img != null) portraitObject = img.gameObject;
        }
        if (speakerImageRect == null && portraitObject != null)
        {
            var img = portraitObject.GetComponent<Image>();
            if (img != null) speakerImageRect = img.rectTransform;
        }

        // Make the image rect fill the speaker box so X/Y both follow reliably.
        // We assume speakerImageRect is a child of speakerRect; anchors 0..1 and
        // zero offsets make it match the parent rect exactly.
        if (speakerRect != null && speakerImageRect != null && speakerImageRect.transform.IsChildOf(speakerRect.transform))
        {
            speakerImageRect.anchorMin = Vector2.zero;
            speakerImageRect.anchorMax = Vector2.one;
            speakerImageRect.pivot = new Vector2(0.5f, 0.5f);
            speakerImageRect.anchoredPosition = Vector2.zero;
            speakerImageRect.sizeDelta = Vector2.zero;
            speakerImageRect.localScale = Vector3.one;
        }

        if (useNormalForcedRect && speakerRect != null)
        {
            ApplyRectToSpeakerAndImage(normalPos, normalSize);
        }

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

    // Called by DialogueBox when dialogue opens
    public void ShowNormal()
    {
        if (speakerRect == null)
            return;

        gameObject.SetActive(true);
        isVisible = true;
        isFocused = false;

        // Apply the first emotion sprite immediately so the correct image is
        // visible from the very first frame of the slide-in animation.
        SetEmotionForLine(0);

        if (startFocused)
        {
            // Slide in off-screen, then snap straight to focused proportions
            Vector2 hiddenPos = focusPos + new Vector2(0f, -hiddenOffset);
            if (moveCoroutine != null)
                StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(SlideSpeaker(hiddenPos, focusPos, focusSize, showDuration));
            isFocused = true;

            if (focusBackground != null)
            {
                focusBackground.gameObject.SetActive(true);
                if (bgFadeCoroutine != null)
                    StopCoroutine(bgFadeCoroutine);
                bgFadeCoroutine = StartCoroutine(FadeBackground(0f, backgroundTargetAlpha, false));
            }
        }
        else
        {
            // Start off-screen below
            Vector2 targetPos = useNormalForcedRect ? normalPos : speakerRect.anchoredPosition;
            Vector2 hiddenPos = targetPos + new Vector2(0f, -hiddenOffset);
            Vector2 size = useNormalForcedRect ? normalSize : speakerRect.sizeDelta;

            if (moveCoroutine != null)
                StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(SlideSpeaker(hiddenPos, targetPos, size, showDuration));
        }
    }

    // Called by DialogueBox when dialogue closes
    public void Hide()
    {
        if (!isVisible || speakerRect == null)
            return;

        isVisible = false;
        isFocused = false;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        Vector2 startPos = speakerRect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, -hiddenOffset);

        moveCoroutine = StartCoroutine(SlideSpeaker(startPos, endPos, speakerRect.sizeDelta, hideDuration, () =>
        {
            gameObject.SetActive(false);
        }));

        // Ensure background is removed if it was focused
        if (focusBackground != null)
        {
            if (bgFadeCoroutine != null)
                StopCoroutine(bgFadeCoroutine);
            bgFadeCoroutine = StartCoroutine(FadeBackground(focusBackground.color.a, 0f, true));
        }
    }

    public void ToggleFocus()
    {
        if (!isVisible)
            return;

        if (isFocused)
            ExitFocus();
        else
            EnterFocus();
    }

    public void EnterFocus()
    {
        if (!useFocusForcedRect || speakerRect == null)
            return;

        isFocused = true;

        // Cache current rect as \"normal\" to restore later
        cachedNormalPos = speakerRect.anchoredPosition;
        cachedNormalSize = speakerRect.sizeDelta;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(SlideSpeaker(cachedNormalPos, focusPos, focusSize, showDuration));

        if (focusBackground != null)
        {
            focusBackground.gameObject.SetActive(true);
            if (bgFadeCoroutine != null)
                StopCoroutine(bgFadeCoroutine);
            bgFadeCoroutine = StartCoroutine(FadeBackground(0f, backgroundTargetAlpha, false));
        }
    }

    public void ExitFocus()
    {
        if (speakerRect == null)
            return;

        isFocused = false;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        Vector2 fromPos = speakerRect.anchoredPosition;
        Vector2 fromSize = speakerRect.sizeDelta;
        Vector2 toPos = useNormalForcedRect ? normalPos : cachedNormalPos;
        Vector2 toSize = useNormalForcedRect ? normalSize : cachedNormalSize;

        moveCoroutine = StartCoroutine(SlideSpeaker(fromPos, toPos, toSize, showDuration));

        if (focusBackground != null)
        {
            if (bgFadeCoroutine != null)
                StopCoroutine(bgFadeCoroutine);
            bgFadeCoroutine = StartCoroutine(FadeBackground(focusBackground.color.a, 0f, true));
        }
    }

    public void SetEmotionForLine(int index)
    {
        if (emotionSprites == null || emotionSprites.Count == 0)
            return;

        // Get the Image from the dragged-in portrait GameObject.
        Image targetImage = portraitObject != null ? portraitObject.GetComponent<Image>() : null;

        if (targetImage == null)
            return;

        if (index >= 0 && index < emotionSprites.Count)
        {
            var s = emotionSprites[index];
            targetImage.sprite = s;
            targetImage.enabled = (s != null);
        }
    }

    // --- helpers -----------------------------------------------------------

    private IEnumerator SlideSpeaker(Vector2 from, Vector2 to, Vector2 size, float duration, System.Action onComplete = null)
    {
        float t = 0f;
        duration = Mathf.Max(0.01f, duration);

        if (useNormalForcedRect || useFocusForcedRect)
        {
            // Only the speakerRect needs an explicit size; the image fills it via anchors.
            if (speakerRect != null)
                speakerRect.sizeDelta = size;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = 1f - Mathf.Pow(1f - (t / duration), 3f); // ease-out
            Vector2 currentPos = Vector2.Lerp(from, to, lerp);
            if (speakerRect != null)
                speakerRect.anchoredPosition = currentPos;
            yield return null;
        }

        if (speakerRect != null)
        {
            speakerRect.anchoredPosition = to;
            if (useNormalForcedRect || useFocusForcedRect)
                speakerRect.sizeDelta = size;
        }

        onComplete?.Invoke();
    }

    // Keep speaker box on the forced rect; the image fills it via anchors.
    private void ApplyRectToSpeakerAndImage(Vector2 pos, Vector2 size)
    {
        if (speakerRect != null)
        {
            speakerRect.anchoredPosition = pos;
            speakerRect.sizeDelta = size;
        }
    }

    private IEnumerator FadeBackground(float from, float to, bool disableOnComplete)
    {
        float t = 0f;
        float duration = Mathf.Max(0.01f, backgroundFadeDuration);

        Color c = bgOriginalColor;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            c.a = Mathf.Lerp(from, to, lerp);
            focusBackground.color = c;
            yield return null;
        }

        c.a = to;
        focusBackground.color = c;

        if (disableOnComplete && Mathf.Approximately(to, 0f))
            focusBackground.gameObject.SetActive(false);
    }
}