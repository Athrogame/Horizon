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
    // Emotion / GIF entries
    // -------------------------------------------------------------------------
    [System.Serializable]
    public class GifEmotionEntry
    {
        [Tooltip("Drag a .gif here directly — no bake step needed in Play Mode (Editor).")]
        public Object gifSource;

        [Tooltip("If true, the GIF loops while the speaker is visible.")]
        public bool loop = true;
    }

    [Header("Emotions")]
    [Tooltip("Drag GIF files here. SpeakerBox decodes them at runtime in the Editor.")]
    public List<GifEmotionEntry> emotionGifEntries = new List<GifEmotionEntry>();

    [Tooltip("Legacy fallback. Used only when Emotion Gif Entries is empty. Supports pre-baked GifEmotionAnimation assets.")]
    public List<GifEmotionAnimation> emotionAnimations = new List<GifEmotionAnimation>();

    // -------------------------------------------------------------------------
    // Normal (unfocused) state
    // -------------------------------------------------------------------------
    [Header("Normal State")]
    public bool useNormalForcedRect = true;
    public Vector2 normalPos = new Vector2(0, 325);
    public Vector2 normalSize = new Vector2(350, 350);
    public float showDuration = 0.35f;
    public float hideDuration = 0.25f;

    [Tooltip("How far below the screen the speaker starts/ends its slide animation.")]
    public float hiddenOffset = 800f;

    // -------------------------------------------------------------------------
    // Focus state
    // -------------------------------------------------------------------------
    [Header("Focus State")]
    public bool useFocusForcedRect = true;
    public Vector2 focusPos = new Vector2(0, 500);
    public Vector2 focusSize = new Vector2(600, 600);

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
    // GIF playback tuning
    // -------------------------------------------------------------------------
    [Header("GIF Playback")]
    [Tooltip("1 = normal speed. 2 = twice as fast. 0.5 = half speed.")]
    public float gifPlaybackSpeed = 1f;

    [Tooltip("Minimum seconds per frame. Prevents decoding too fast on very short-delay GIFs.")]
    public float gifPlaybackFpsCap = 0.9f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private bool isVisible;
    private bool isFocused;

    private Coroutine moveCoroutine;
    private Coroutine bgFadeCoroutine;
    private Coroutine emotionAnimationCoroutine;

    // Saved when entering focus without useNormalForcedRect, so we can restore on exit.
    private Vector2 cachedNormalPos;
    private Vector2 cachedNormalSize;

    private Color bgOriginalColor;
    private object currentEmotionKey;
    private Image portraitImageComponent;

    // -------------------------------------------------------------------------
    // GIF runtime data
    // -------------------------------------------------------------------------
    private class RuntimeGifData
    {
        public List<Sprite> frames;
        public List<float> delays;
        public bool loop;
    }

    private readonly Dictionary<GifEmotionAnimation, RuntimeGifData> runtimeGifCache
        = new Dictionary<GifEmotionAnimation, RuntimeGifData>();

    private readonly Dictionary<Object, RuntimeGifData> runtimeGifSourceCache
        = new Dictionary<Object, RuntimeGifData>();

    private const int GifHeaderBytes = 6;
    private const int RuntimeGifMaxFrames = 120;

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

        if (useNormalForcedRect && speakerRect != null)
            SetSpeakerRect(normalPos, normalSize);

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

    // Slides the speaker in from below. If startFocused, goes straight to focus size.
    public void ShowNormal()
    {
        if (speakerRect == null) return;

        gameObject.SetActive(true);
        isVisible = true;
        isFocused = false;

        // Apply emotion immediately so the correct portrait shows on the first frame.
        SetEmotionForLine(0);

        if (startFocused)
        {
            Vector2 hiddenPos = focusPos + new Vector2(0f, -hiddenOffset);
            StartMove(hiddenPos, focusPos, focusSize, showDuration);
            isFocused = true;
            FadeBackgroundIn();
        }
        else
        {
            Vector2 targetPos = useNormalForcedRect ? normalPos : speakerRect.anchoredPosition;
            Vector2 size      = useNormalForcedRect ? normalSize : speakerRect.sizeDelta;
            Vector2 hiddenPos = targetPos + new Vector2(0f, -hiddenOffset);
            StartMove(hiddenPos, targetPos, size, showDuration);
        }
    }

    // Slides the speaker out below and deactivates it.
    public void Hide()
    {
        if (!isVisible || speakerRect == null) return;

        StopEmotionAnimation();
        isVisible = false;
        isFocused = false;

        Vector2 startPos = speakerRect.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(0f, -hiddenOffset);
        StartMove(startPos, endPos, speakerRect.sizeDelta, hideDuration, () => gameObject.SetActive(false));

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
        if (!useFocusForcedRect || speakerRect == null) return;

        isFocused = true;
        cachedNormalPos  = speakerRect.anchoredPosition;
        cachedNormalSize = speakerRect.sizeDelta;

        StartMove(cachedNormalPos, focusPos, focusSize, showDuration);
        FadeBackgroundIn();
    }

    public void ExitFocus()
    {
        if (speakerRect == null) return;

        isFocused = false;

        Vector2 fromPos = speakerRect.anchoredPosition;
        Vector2 toPos   = useNormalForcedRect ? normalPos  : cachedNormalPos;
        Vector2 toSize  = useNormalForcedRect ? normalSize : cachedNormalSize;

        StartMove(fromPos, toPos, toSize, showDuration);
        FadeBackgroundOut();
    }

    // -------------------------------------------------------------------------
    // Emotion / GIF selection
    // -------------------------------------------------------------------------

    // Sets the portrait GIF for the given dialogue line index.
    public void SetEmotionForLine(int index)
    {
        if (portraitObject == null) return;

        // Preferred path: raw GIF entries dragged directly into the inspector.
        if (emotionGifEntries != null && emotionGifEntries.Count > 0)
        {
            if (index < 0 || index >= emotionGifEntries.Count) return;

            var entry = emotionGifEntries[index];
            if (entry == null || entry.gifSource == null) { ClearPortraitAndStopAnimation(); return; }

            var runtime = GetRuntimeGifData(entry);
            if (runtime == null || runtime.frames.Count == 0) { ClearPortraitAndStopAnimation(); return; }

            StopEmotionAnimation();
            currentEmotionKey = entry.gifSource;
            emotionAnimationCoroutine = StartCoroutine(PlayEmotionFrames(runtime, currentEmotionKey));
            return;
        }

        // Legacy fallback: GifEmotionAnimation assets.
        if (emotionAnimations == null || emotionAnimations.Count == 0) return;
        if (index < 0 || index >= emotionAnimations.Count) return;

        var anim = emotionAnimations[index];
        if (anim == null || anim.gifSource == null) { ClearPortraitAndStopAnimation(); return; }

        var runtimeAnim = GetRuntimeGifData(anim);
        if (runtimeAnim == null || runtimeAnim.frames.Count == 0) { ClearPortraitAndStopAnimation(); return; }

        StopEmotionAnimation();
        currentEmotionKey = anim;
        emotionAnimationCoroutine = StartCoroutine(PlayEmotionFrames(runtimeAnim, currentEmotionKey));
    }

    // -------------------------------------------------------------------------
    // GIF decode and cache
    // -------------------------------------------------------------------------

    private RuntimeGifData GetRuntimeGifData(GifEmotionAnimation anim)
    {
        if (anim == null) return null;
        if (runtimeGifCache.TryGetValue(anim, out var cached)) return cached;

        // Use pre-baked frames if available.
        if (anim.IsReady)
        {
            var data = new RuntimeGifData { frames = anim.frames, delays = anim.frameDelays, loop = anim.loop };
            runtimeGifCache[anim] = data;
            return data;
        }

        if (!TryReadGifBytesFromAsset(anim.gifSource, out byte[] bytes)) return null;

        var runtimeData = DecodeGifBytes(bytes, anim.name, anim.loop);
        if (runtimeData == null) return null;

        runtimeGifCache[anim] = runtimeData;
        return runtimeData;
    }

    private RuntimeGifData GetRuntimeGifData(GifEmotionEntry entry)
    {
        if (entry == null || entry.gifSource == null) return null;

        if (runtimeGifSourceCache.TryGetValue(entry.gifSource, out var cached))
        {
            cached.loop = entry.loop; // Loop flag can differ per entry even for the same GIF.
            return cached;
        }

        if (!TryReadGifBytesFromAsset(entry.gifSource, out byte[] bytes)) return null;

        var runtimeData = DecodeGifBytes(bytes, entry.gifSource.name, entry.loop);
        if (runtimeData == null) return null;

        runtimeGifSourceCache[entry.gifSource] = runtimeData;
        return runtimeData;
    }

    private bool TryReadGifBytesFromAsset(Object sourceAsset, out byte[] bytes)
    {
        bytes = null;
#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(sourceAsset);
        if (string.IsNullOrEmpty(path)) return false;

        bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length < GifHeaderBytes) return false;

        string sig = Encoding.ASCII.GetString(bytes, 0, GifHeaderBytes);
        if (sig != "GIF87a" && sig != "GIF89a")
        {
            Debug.LogError($"SpeakerBox: '{sourceAsset.name}' is not a valid GIF (signature: '{sig}', path: '{path}').");
            return false;
        }
        return true;
#else
        Debug.LogError("SpeakerBox: Runtime GIF decode only works in the Editor. Bake GIFs for builds.");
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
                if (tex == null) break;

                tex.filterMode = FilterMode.Point;
                tex.wrapMode   = TextureWrapMode.Clamp;
                tex.name       = $"{namingPrefix}_rt_frame_{frameIndex:000}";

                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = $"{namingPrefix}_rt_sprite_{frameIndex:000}";

                frames.Add(sprite);
                delays.Add(Mathf.Max(0f, img.Delay / 1000f));

                frameIndex++;
                img = decoder.NextImage();
            }
        }

        if (frames.Count == 0) return null;

        return new RuntimeGifData { frames = frames, delays = delays, loop = loop };
    }

    // -------------------------------------------------------------------------
    // GIF frame playback coroutine
    // -------------------------------------------------------------------------

    private IEnumerator PlayEmotionFrames(RuntimeGifData runtime, object key)
    {
        if (runtime == null || runtime.frames == null || runtime.frames.Count == 0) yield break;

        if (portraitImageComponent == null && portraitObject != null)
            portraitImageComponent = portraitObject.GetComponent<Image>();

        if (portraitImageComponent == null) yield break;

        int frameCount = runtime.frames.Count;

        do
        {
            for (int i = 0; i < frameCount; i++)
            {
                if (!isVisible || currentEmotionKey == null || currentEmotionKey != key)
                    yield break;

                var frameSprite = runtime.frames[i];
                portraitImageComponent.sprite  = frameSprite;
                portraitImageComponent.enabled = frameSprite != null;

                float baseDelay = (runtime.delays != null && i < runtime.delays.Count) ? runtime.delays[i] : 0f;
                float speed     = Mathf.Max(0.0001f, gifPlaybackSpeed);
                float minDelay  = 1f / (Mathf.Max(0.0001f, gifPlaybackFpsCap) * speed);
                float delay     = Mathf.Max(minDelay, baseDelay / speed);

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
            portraitImageComponent.sprite  = null;
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

    // -------------------------------------------------------------------------
    // Movement helpers
    // -------------------------------------------------------------------------

    private void StartMove(Vector2 from, Vector2 to, Vector2 size, float duration, System.Action onComplete = null)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(SlideSpeaker(from, to, size, duration, onComplete));
    }

    // Slides speakerRect from one position to another with an ease-out curve.
    // Sets the target size immediately so the portrait (stretch child) resizes from the start.
    private IEnumerator SlideSpeaker(Vector2 from, Vector2 to, Vector2 size, float duration, System.Action onComplete = null)
    {
        duration = Mathf.Max(0.01f, duration);

        if ((useNormalForcedRect || useFocusForcedRect) && speakerRect != null)
            speakerRect.sizeDelta = size;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = 1f - Mathf.Pow(1f - (t / duration), 3f); // ease-out cubic
            if (speakerRect != null)
                speakerRect.anchoredPosition = Vector2.Lerp(from, to, lerp);
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

    private void SetSpeakerRect(Vector2 pos, Vector2 size)
    {
        if (speakerRect == null) return;
        speakerRect.anchoredPosition = pos;
        speakerRect.sizeDelta = size;
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
