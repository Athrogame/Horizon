using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
// Do not `using MG.GIF` — it conflicts with UnityEngine.UI.Image.

public class SpeakerBox : MonoBehaviour, ISpeakerBox
{
    // ---------------------------------------------------------------------
    // Scene/UI references
    // ---------------------------------------------------------------------
    [Header("Required References")]
    public RectTransform speakerRect;
    // Drag the child GameObject that holds the portrait Image here.
    // The script will read its Image component to swap emotion sprites.
    public GameObject portraitObject;
    // Optional: if set, this RectTransform will be kept in sync
    // with the speakerRect so the "box" and "image" always share
    // the same rect (position + size) at runtime.
    public RectTransform speakerImageRect;

    [System.Serializable]
    public class GifEmotionEntry
    {
        [Tooltip("Drag a .gif here. No GifEmotionAnimation asset or Bake step required in Play Mode (Editor).")]
        public Object gifSource;

        [Tooltip("If true, the GIF loops while the speaker is visible.")]
        public bool loop = true;
    }

    [Header("Emotions (drag GIFs directly)")]
    [Tooltip("Use this list to drag GIF files straight into the inspector. SpeakerBox will decode them at runtime (Play Mode in Editor).")]
    public List<GifEmotionEntry> emotionGifEntries = new List<GifEmotionEntry>();

    [Header("Emotions (legacy: GifEmotionAnimation assets)")]
    [Tooltip("Optional. If emotionGifEntries is empty, SpeakerBox will fall back to this list (supports baked frames).")]
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
    private object currentEmotionKey;
    private Image portraitImageComponent;

    [Header("GIF Playback")]
    [Tooltip("1 = normal speed. 2 = twice as fast. 0.5 = half speed.")]
    public float gifPlaybackSpeed = 1f;

    [Tooltip("Playback fps cap (used to avoid decoding too fast).")]
    public float gifPlaybackFpsCap = 0.9f;

    private class RuntimeGifData
    {
        // Decoded frames and matching delays in seconds.
        public List<Sprite> frames;
        public List<float> delays;
        public bool loop;
    }

    private readonly Dictionary<GifEmotionAnimation, RuntimeGifData> runtimeGifCache = new Dictionary<GifEmotionAnimation, RuntimeGifData>();
    private readonly Dictionary<Object, RuntimeGifData> runtimeGifSourceCache = new Dictionary<Object, RuntimeGifData>();
    private const int GifHeaderBytes = 6;
    private const int RuntimeGifMaxFrames = 120;

    // ---------------------------------------------------------------------
    // Lifecycle and visibility
    // ---------------------------------------------------------------------
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

    // Called by DialogueBox when dialogue opens.
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

    // Called by DialogueBox when dialogue closes.
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

    // ---------------------------------------------------------------------
    // Focus mode
    // ---------------------------------------------------------------------
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

    // ---------------------------------------------------------------------
    // Emotion / GIF selection
    // ---------------------------------------------------------------------
    public void SetEmotionForLine(int index)
    {
        if (portraitObject == null)
            return;

        // Preferred: raw GIF entries (no GifEmotionAnimation asset needed).
        if (emotionGifEntries != null && emotionGifEntries.Count > 0)
        {
            if (index < 0 || index >= emotionGifEntries.Count)
                return;

            var entry = emotionGifEntries[index];
            if (entry == null || entry.gifSource == null)
            {
                ClearPortraitAndStopAnimation();
                return;
            }

            var runtime = GetRuntimeGifData(entry);
            if (runtime == null || runtime.frames == null || runtime.frames.Count == 0)
            {
                ClearPortraitAndStopAnimation();
                return;
            }

            StopEmotionAnimation();
            currentEmotionKey = entry.gifSource;
            emotionAnimationCoroutine = StartCoroutine(PlayEmotionFrames(runtime, currentEmotionKey));
            return;
        }

        // Legacy fallback: GifEmotionAnimation assets (baked frames supported).
        if (emotionAnimations == null || emotionAnimations.Count == 0)
            return;

        if (index < 0 || index >= emotionAnimations.Count)
            return;

        var anim = emotionAnimations[index];
        if (anim == null || anim.gifSource == null)
        {
            ClearPortraitAndStopAnimation();
            return;
        }

        var runtimeAnim = GetRuntimeGifData(anim);
        if (runtimeAnim == null || runtimeAnim.frames == null || runtimeAnim.frames.Count == 0)
        {
            ClearPortraitAndStopAnimation();
            return;
        }

        StopEmotionAnimation();
        currentEmotionKey = anim;
        emotionAnimationCoroutine = StartCoroutine(PlayEmotionFrames(runtimeAnim, currentEmotionKey));
    }

    // ---------------------------------------------------------------------
    // GIF decode and cache
    // ---------------------------------------------------------------------
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
        if (!TryReadGifBytesFromAsset(anim.gifSource, out byte[] bytes))
            return null;

        var runtimeData = DecodeGifBytes(bytes, anim.name, anim.loop);
        if (runtimeData == null)
            return null;

        runtimeGifCache[anim] = runtimeData;
        return runtimeData;
    }

    private RuntimeGifData GetRuntimeGifData(GifEmotionEntry entry)
    {
        if (entry == null || entry.gifSource == null)
            return null;

        if (runtimeGifSourceCache.TryGetValue(entry.gifSource, out var cached))
        {
            // Frames are the same; loop behavior can differ per entry.
            cached.loop = entry.loop;
            return cached;
        }

        // Runtime decode fallback (no manual baking).
        // This uses AssetDatabase to read the GIF bytes in the editor.
        // In builds, extend this to load from StreamingAssets if you need it.
        if (!TryReadGifBytesFromAsset(entry.gifSource, out byte[] bytes))
            return null;

        var runtimeData = DecodeGifBytes(bytes, entry.gifSource.name, entry.loop);
        if (runtimeData == null)
            return null;

        runtimeGifSourceCache[entry.gifSource] = runtimeData;
        return runtimeData;
    }

    private bool TryReadGifBytesFromAsset(Object sourceAsset, out byte[] bytes)
    {
        bytes = null;

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(sourceAsset);
        if (string.IsNullOrEmpty(path))
            return false;

        bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length < GifHeaderBytes)
            return false;

        // Sanity check: make sure these are real GIF bytes.
        string sig = Encoding.ASCII.GetString(bytes, 0, GifHeaderBytes);
        if (sig != "GIF87a" && sig != "GIF89a")
        {
            Debug.LogError(
                $"SpeakerBox: The asset bytes for '{sourceAsset.name}' are not a GIF. " +
                $"AssetPath='{path}', Signature='{sig}'."
            );
            return false;
        }

        return true;
#else
        Debug.LogError("SpeakerBox: Runtime GIF decode is only implemented in the editor. Bake GIFs or extend runtime byte loading.");
        return false;
#endif
    }

    private RuntimeGifData DecodeGifBytes(byte[] bytes, string namingPrefix, bool loop)
    {
        var frames = new List<Sprite>();
        var delays = new List<float>();

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
                tex.name = $"{namingPrefix}_rt_frame_{frameIndex:000}";

                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                sprite.name = $"{namingPrefix}_rt_sprite_{frameIndex:000}";

                float delaySeconds = Mathf.Max(0f, img.Delay / 1000f);
                frames.Add(sprite);
                delays.Add(delaySeconds);

                frameIndex++;
                img = decoder.NextImage();
            }
        }

        if (frames.Count == 0)
            return null;

        return new RuntimeGifData
        {
            frames = frames,
            delays = delays,
            loop = loop
        };
    }

    // ---------------------------------------------------------------------
    // Runtime GIF playback
    // ---------------------------------------------------------------------
    private IEnumerator PlayEmotionFrames(RuntimeGifData runtime, object key)
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
                if (!isVisible || currentEmotionKey == null || currentEmotionKey != key)
                    yield break;

                var frameSprite = runtime.frames[i];
                portraitImageComponent.sprite = frameSprite;
                portraitImageComponent.enabled = (frameSprite != null);

                float baseDelay = (runtime.delays != null && i >= 0 && i < runtime.delays.Count) ? runtime.delays[i] : 0f;

                float speed = Mathf.Max(0.0001f, gifPlaybackSpeed);
                float scaledDelay = baseDelay / speed;

                // Allow speed to scale the effective fps cap so "speed" really speeds up/slows down.
                float effectiveFpsCap = Mathf.Max(0.0001f, gifPlaybackFpsCap) * speed;
                float minDelay = 1f / effectiveFpsCap;

                float delay = Mathf.Max(minDelay, scaledDelay);
                yield return new WaitForSecondsRealtime(delay);
            }
        }
        while (runtime.loop && isVisible && currentEmotionKey == key);
    }

    private void ClearPortraitAndStopAnimation()
    {
        StopEmotionAnimation();
        if (portraitImageComponent != null)
        {
            portraitImageComponent.sprite = null;
            portraitImageComponent.enabled = false;
        }
    }

    private void StopEmotionAnimation()
    {
        if (emotionAnimationCoroutine != null)
        {
            StopCoroutine(emotionAnimationCoroutine);
            emotionAnimationCoroutine = null;
        }

        currentEmotionKey = null;
    }

    // ---------------------------------------------------------------------
    // Animation helpers (movement + background)
    // ---------------------------------------------------------------------

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