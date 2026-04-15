using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Singleton MonoBehaviour that replicates OMORI's exact resolution and scaling behaviour.
//
// OMORI runs on RPG Maker MV / NW.js which enforces:
//   - A fixed 640x480 internal canvas
//   - Strict integer scale multipliers only (x1, x2, x3, x4) — never fractional
//   - Windowed: window resized to exactly 640N x 480N
//   - Fullscreen: largest N that fits using LOGICAL (DPI-adjusted) pixels, letterboxed with pure black
//
// This object survives scene loads via DontDestroyOnLoad.
// Duplicate instances (from scenes loaded additively) are destroyed automatically.
public class ResolutionManager : MonoBehaviour
{
    public static ResolutionManager Instance { get; private set; }

    [Header("Config")]
    [Tooltip("ScriptableObject containing base resolution and scale settings. Assign in Inspector.")]
    public ResolutionConfig config;

    [Tooltip("Log scale change events to the console.")]
    public bool logScalingEvents = true;

    // The integer multiplier currently applied in windowed mode (stored so it survives a
    // temporary switch to fullscreen and is restored when the player exits fullscreen).
    private int currentWindowScale = 1;

    // Whether the game is currently in fullscreen (borderless window) mode.
    private bool isFullscreen = false;

    // All active cameras in the current scene. Refreshed every scene load so we never
    // hold stale references across loads. DO NOT cache across scene boundaries.
    private Camera[] managedCameras = new Camera[0];

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Enforce singleton — if another instance already exists, destroy this duplicate.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Try to load config from Resources if it wasn't assigned in the Inspector.
        if (config == null)
            config = Resources.Load<ResolutionConfig>("ResolutionConfig");

        if (config == null)
            Debug.LogError("[Resolution] No ResolutionConfig found! Assign it in the Inspector or place it in Assets/Resources/ResolutionConfig.asset.");

        // Refresh the camera list every time a new scene finishes loading.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Apply the default windowed scale defined in config.
        // Fullscreen is NEVER applied on launch — only via keybind or settings menu.
        int launchScale = config != null ? config.defaultWindowScale : 1;
        ApplyWindowedScale(launchScale);
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid a dangling delegate if this object is ever destroyed.
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        // F11 / F4 toggle fullscreen on Windows, matching OMORI's keybinds.
        // Option+Return (Alt+Return) is the standard Mac fullscreen shortcut.
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        bool altReturn = altHeld && Input.GetKeyDown(KeyCode.Return);

        if (Input.GetKeyDown(KeyCode.F11) || Input.GetKeyDown(KeyCode.F4) || altReturn)
            ToggleFullscreen();

        // Number keys 1–4 set the windowed scale multiplier.
        for (int i = 1; i <= 4; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i)))
                SetWindowScale(i);
    }

    // -------------------------------------------------------------------------
    // Scene loading
    // -------------------------------------------------------------------------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Camera references become stale after a scene load — rebuild the list.
        RefreshCameraCache();
    }

    // Rebuilds the list of all cameras in the currently loaded scenes.
    // Called after every scene load and after every SetResolution call settles.
    public void RefreshCameraCache()
    {
        // FindObjectsByType is the non-deprecated Unity 6 replacement for FindObjectsOfType.
        managedCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
    }

    // -------------------------------------------------------------------------
    // Scale calculation
    // -------------------------------------------------------------------------

    // Replicates OMORI's NW.js / Chromium logical-pixel behaviour for fullscreen scaling.
    //
    // NW.js reads the display size in LOGICAL pixels (i.e. after the OS DPI scale has been
    // applied), not raw physical pixels. We must do the same so the integer scale cap matches
    // OMORI exactly on every monitor + DPI combination.
    //
    // Example: 2560x1440 monitor at 125% DPI (1.25x) → logical = 2048x1152 → max scale = x2
    //          2560x1440 monitor at 100% DPI (1.00x) → logical = 2560x1440 → max scale = x3
    //
    // Returns the largest N such that: 640*N <= logicalW AND 480*N <= logicalH
    private int CalculateFullscreenIntegerScale()
    {
        int physicalW = Screen.currentResolution.width;
        int physicalH = Screen.currentResolution.height;

        // 96 DPI = 100% OS scaling (the CSS / Windows baseline).
        // 120 DPI = 125%, 144 DPI = 150%, etc.
        float dpiScale = Screen.dpi / 96f;

        // Clamp to 1 minimum — some platforms return 0 for Screen.dpi.
        dpiScale = Mathf.Max(dpiScale, 1f);

        int logicalW = Mathf.RoundToInt(physicalW / dpiScale);
        int logicalH = Mathf.RoundToInt(physicalH / dpiScale);

        // Walk up from x1 until the next step would exceed either logical dimension.
        int scale = 1;
        while ((config.baseWidth  * (scale + 1)) <= logicalW &&
               (config.baseHeight * (scale + 1)) <= logicalH)
        {
            scale++;
        }

        if (logScalingEvents)
            Debug.Log($"[Resolution] Physical: {physicalW}x{physicalH} | " +
                      $"DPI: {Screen.dpi} ({dpiScale:F2}x) | " +
                      $"Logical: {logicalW}x{logicalH} | " +
                      $"Best integer fit: x{scale} ({config.baseWidth * scale}x{config.baseHeight * scale})");

        return scale;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    // Resize the window to exactly baseWidth*scale x baseHeight*scale pixels.
    // scale is clamped to the range [1, 4].
    public void ApplyWindowedScale(int scale)
    {
        if (config == null) { Debug.LogError("[Resolution] Cannot apply scale — config is missing."); return; }

        scale = Mathf.Clamp(scale, 1, 4);
        currentWindowScale = scale;
        isFullscreen = false;

        int w = config.baseWidth  * scale;
        int h = config.baseHeight * scale;

        // Screen.SetResolution is async — we wait one frame before resetting viewports
        // to make sure the new dimensions have been applied.
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
        StartCoroutine(ResetViewportsNextFrame());

        if (logScalingEvents)
            Debug.Log($"[Resolution] Windowed x{scale}: {w}x{h}");
    }

    // Enter borderless fullscreen (FullScreenWindow), then calculate and apply
    // the largest integer-scaled viewport that fits inside the display's logical pixels.
    public void ApplyFullscreen()
    {
        if (config == null) { Debug.LogError("[Resolution] Cannot apply fullscreen — config is missing."); return; }

        isFullscreen = true;

        // Use borderless fullscreen, NOT exclusive fullscreen, matching OMORI.
        Screen.SetResolution(
            Screen.currentResolution.width,
            Screen.currentResolution.height,
            FullScreenMode.FullScreenWindow
        );

        // Viewport must be applied after the resolution change settles (async).
        StartCoroutine(ApplyFullscreenViewportNextFrame());

        if (logScalingEvents)
            Debug.Log("[Resolution] Entering fullscreen...");
    }

    // Toggle between fullscreen and windowed. Windowed restores the last used scale.
    public void ToggleFullscreen()
    {
        if (isFullscreen)
            ApplyWindowedScale(currentWindowScale);
        else
            ApplyFullscreen();
    }

    // Set the windowed scale multiplier. If currently fullscreen, only stores the value
    // — it will be applied the next time the player exits fullscreen.
    public void SetWindowScale(int scale)
    {
        currentWindowScale = Mathf.Clamp(scale, 1, 4);
        if (!isFullscreen)
            ApplyWindowedScale(currentWindowScale);
    }

    // -------------------------------------------------------------------------
    // Coroutines
    // -------------------------------------------------------------------------

    // Wait one frame for Screen.SetResolution to settle, then reset all camera viewports
    // to full-screen (0,0,1,1) — correct for windowed mode where no letterboxing is needed.
    private IEnumerator ResetViewportsNextFrame()
    {
        yield return null;
        RefreshCameraCache();
        foreach (Camera cam in managedCameras)
            cam.rect = new Rect(0f, 0f, 1f, 1f);
    }

    // Wait two frames for Screen.SetResolution to settle (one extra for Mac safety),
    // then calculate the integer scale and apply letterbox/pillarbox viewport rects.
    private IEnumerator ApplyFullscreenViewportNextFrame()
    {
        yield return null; // mandatory — SetResolution is async
        yield return null; // second frame for Mac reliability

        int scale   = CalculateFullscreenIntegerScale();
        int gameW   = config.baseWidth  * scale;
        int gameH   = config.baseHeight * scale;
        int screenW = Screen.width;
        int screenH = Screen.height;

        // Normalised viewport rect — centres the game area, leaving pure black bars.
        float vpW = (float)gameW / screenW;
        float vpH = (float)gameH / screenH;
        float vpX = (1f - vpW) / 2f;
        float vpY = (1f - vpH) / 2f;

        Rect viewport = new Rect(vpX, vpY, vpW, vpH);

        RefreshCameraCache();
        foreach (Camera cam in managedCameras)
            cam.rect = viewport;

        if (logScalingEvents)
            Debug.Log($"[Resolution] Fullscreen x{scale}: game area {gameW}x{gameH} | " +
                      $"Black bars L/R={Mathf.RoundToInt((screenW - gameW) / 2f)}px " +
                      $"T/B={Mathf.RoundToInt((screenH - gameH) / 2f)}px");
    }
}
