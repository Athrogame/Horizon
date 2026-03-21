using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
// Do not `using MG.GIF` — it conflicts with UnityEngine.UI.Image.

public class SpeakerBox : MonoBehaviour, ISpeakerBox
{
    [Header("Required References")]
    public RectTransform speakerRect;
    // Drag the child GameObject that holds the portrait Image here.
    // The script will read its Image component to swap emotion sprites.
    public GameObject portraitObject;
    // Optional: if set, this RectTransform will be kept in sync
    // with the speakerRect so the "box" and "image" always share
    // the same rect (position + size) at runtime.
    public RectTransform speakerImageRect;
    public List<GifEmotionAnimation> emotionAnimations = new List<GifEmotionAnimation>();

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

    // GIF frame playback
    private Coroutine emotionAnimationCoroutine;
    private GifEmotionAnimation currentEmotionAnimation;
    private Image portraitImageComponent;

    private class RuntimeGifData
    {
        public List<Sprite> frames;
        public List<float> delays;
        public bool loop;
    }

    private readonly Dictionary<GifEmotionAnimation, RuntimeGifData> runtimeGifCache = new Dictionary<GifEmotionAnimation, RuntimeGifData>();

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

        if (portraitObject != null)
            portraitImageComponent = portraitObject.GetComponent<Image>();

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

        StopEmotionAnimation();

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

        // Cache current rect as "normal" to restore later
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
        if (portraitObject == null)
            return;

        if (emotionAnimations == null || emotionAnimations.Count == 0)
            return;

        if (index < 0 || index >= emotionAnimations.Count)
            return;

        var anim = emotionAnimations[index];
        if (anim == null || anim.gifSource == null)
        {
            StopEmotionAnimation();
            if (portraitImageComponent != null)
            {
                portraitImageComponent.sprite = null;
                portraitImageComponent.enabled = false;
            }
            return;
        }

        var runtime = GetRuntimeGifData(anim);
        if (runtime == null || runtime.frames == null || runtime.frames.Count == 0)
        {
            StopEmotionAnimation();
            if (portraitImageComponent != null)
            {
                portraitImageComponent.sprite = null;
                portraitImageComponent.enabled = false;
            }
            return;
        }

        StopEmotionAnimation();
        currentEmotionAnimation = anim;
        emotionAnimationCoroutine = StartCoroutine(PlayEmotionFrames(runtime));
    }

    private const float RuntimeGifPlaybackFpsCap = 0.9f;
    private const int RuntimeGifMaxFrames = 120;

    private RuntimeGifData GetRuntimeGifData(GifEmotionAnimation anim)
    {
        if (anim == null)
            return null;

        if (runtimeGifCache.TryGetValue(anim, out var cached))
            return cached;

        // If it's already baked, use it directly.
        if (anim.IsReady)
        {
            var data = new RuntimeGifData
            {
                frames = anim.frames,
                delays = anim.frameDelays,
                loop = anim.loop
            };
            runtimeGifCache[anim] = data;
            return data;
        }

        // Runtime decode fallback (no manual baking).
        // In builds this requires a different byte-source; for now we support editor playback.
#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(anim.gifSource);
        if (string.IsNullOrEmpty(path))
            return null;

        byte[] bytes = File.ReadAllBytes(path);
#else
        Debug.LogError("SpeakerBox: Runtime GIF decode is only implemented in the editor. Bake GIFs or extend runtime byte loading.");
        return null;
#endif

        var frames = new List<Sprite>();
        var delays = new List<float>();

        float minDelaySeconds = 1f / Mathf.Max(0.0001f, RuntimeGifPlaybackFpsCap);

        using (var decoder = new MG.GIF.Decoder(bytes))
        {
            var img = decoder.NextImage();
            int frameIndex = 0;

            while (img != null && frameIndex < RuntimeGifMaxFrames)
            {
                Texture2D tex = img.CreateTexture();
                if (tex == null)
                    break;

                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                tex.name = $"{anim.name}_rt_frame_{frameIndex:000}";

                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                sprite.name = $"{anim.name}_rt_sprite_{frameIndex:000}";

                float delaySeconds = Mathf.Max(minDelaySeconds, img.Delay / 1000f);

                frames.Add(sprite);
                delays.Add(delaySeconds);

                frameIndex++;
                img = decoder.NextImage();
            }
        }

        if (frames.Count == 0)
            return null;

        var runtimeData = new RuntimeGifData
        {
            frames = frames,
            delays = delays,
            loop = anim.loop
        };

        runtimeGifCache[anim] = runtimeData;
        return runtimeData;
    }

    private IEnumerator PlayEmotionFrames(RuntimeGifData runtime)
    {
        if (runtime == null || runtime.frames == null || runtime.frames.Count == 0)
            yield break;

        if (portraitImageComponent == null && portraitObject != null)
            portraitImageComponent = portraitObject.GetComponent<Image>();

        if (portraitImageComponent == null)
            yield break;

        int frameCount = runtime.frames.Count;

        do
        {
            for (int i = 0; i < frameCount; i++)
            {
                if (!isVisible || currentEmotionAnimation == null)
                    yield break;

                var frameSprite = runtime.frames[i];
                portraitImageComponent.sprite = frameSprite;
                portraitImageComponent.enabled = (frameSprite != null);

                float delay = (runtime.delays != null && i >= 0 && i < runtime.delays.Count) ? runtime.delays[i] : (1f / Mathf.Max(0.0001f, RuntimeGifPlaybackFpsCap));
                yield return new WaitForSecondsRealtime(delay);
            }
        }
        while (runtime.loop && isVisible && currentEmotionAnimation != null);
    }

    private void StopEmotionAnimation()
    {
        if (emotionAnimationCoroutine != null)
        {
            StopCoroutine(emotionAnimationCoroutine);
            emotionAnimationCoroutine = null;
        }

        currentEmotionAnimation = null;
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