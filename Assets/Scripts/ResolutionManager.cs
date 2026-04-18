using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Manages window resolution and scaling using strict integer multipliers only (x1, x2, x3, x4).
// Fullscreen uses the largest integer scale that fits the display's logical pixel size.
// Survives scene loads via DontDestroyOnLoad. Duplicate instances are destroyed automatically.
public class ResolutionManager : MonoBehaviour
{
    public static ResolutionManager Instance { get; private set; }

    [Header("Config")]
    [Tooltip("ScriptableObject containing base resolution and scale settings. Assign in Inspector.")]
    public ResolutionConfig config;

    // Last integer scale used in windowed mode — restored when exiting fullscreen.
    private int currentWindowScale = 1;
    private bool isFullscreen = false;

    // Refreshed on every scene load so we never hold stale camera references.
    private Camera[] managedCameras = new Camera[0];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (config == null)
            config = Resources.Load<ResolutionConfig>("ResolutionConfig");

        if (config == null)
            Debug.LogError("[Resolution] No ResolutionConfig found! Assign it in the Inspector or place it in Assets/Resources/ResolutionConfig.asset.");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        int launchScale = config != null ? config.defaultWindowScale : 1;
        ApplyWindowedScale(launchScale);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        // F11 / F4 toggle fullscreen. Alt+Return is the standard Mac shortcut.
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (Input.GetKeyDown(KeyCode.F11) || Input.GetKeyDown(KeyCode.F4) || (altHeld && Input.GetKeyDown(KeyCode.Return)))
            ToggleFullscreen();

        // Number keys 1–4 set the windowed scale multiplier directly.
        for (int i = 1; i <= 4; i++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i)))
                SetWindowScale(i);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshCameraCache();
    }

    // Rebuilds the camera list — called after every scene load and resolution change.
    public void RefreshCameraCache()
    {
        managedCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    // Returns the largest integer N where: baseWidth*N <= logicalW AND baseHeight*N <= logicalH.
    // Uses logical (DPI-adjusted) pixels so the scale matches correctly on high-DPI displays.
    private int CalculateFullscreenIntegerScale()
    {
        int physicalW = Screen.currentResolution.width;
        int physicalH = Screen.currentResolution.height;

        // 96 DPI = 100% OS scaling. Clamp to 1 — some platforms return 0 for Screen.dpi.
        float dpiScale = Mathf.Max(Screen.dpi / 96f, 1f);

        int logicalW = Mathf.RoundToInt(physicalW / dpiScale);
        int logicalH = Mathf.RoundToInt(physicalH / dpiScale);

        int scale = 1;
        while ((config.baseWidth  * (scale + 1)) <= logicalW &&
               (config.baseHeight * (scale + 1)) <= logicalH)
            scale++;

        return scale;
    }

    // Resizes the window to exactly baseWidth*scale x baseHeight*scale. Scale is clamped to [1, 4].
    public void ApplyWindowedScale(int scale)
    {
        if (config == null) { Debug.LogError("[Resolution] Cannot apply scale — config is missing."); return; }

        scale = Mathf.Clamp(scale, 1, 4);
        currentWindowScale = scale;
        isFullscreen = false;

        Screen.SetResolution(config.baseWidth * scale, config.baseHeight * scale, FullScreenMode.Windowed);
        StartCoroutine(ResetViewportsNextFrame());
    }

    // Enters borderless fullscreen and applies the largest fitting integer-scale viewport.
    public void ApplyFullscreen()
    {
        if (config == null) { Debug.LogError("[Resolution] Cannot apply fullscreen — config is missing."); return; }

        isFullscreen = true;
        Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        StartCoroutine(ApplyFullscreenViewportNextFrame());
    }

    // Toggles between fullscreen and windowed, restoring the last used scale.
    public void ToggleFullscreen()
    {
        if (isFullscreen) ApplyWindowedScale(currentWindowScale);
        else              ApplyFullscreen();
    }

    // Sets the windowed scale. If currently fullscreen, stores the value for when windowed mode is restored.
    public void SetWindowScale(int scale)
    {
        currentWindowScale = Mathf.Clamp(scale, 1, 4);
        if (!isFullscreen)
            ApplyWindowedScale(currentWindowScale);
    }

    // Wait one frame for SetResolution to settle, then reset all camera viewports to full (no letterbox needed in windowed mode).
    private IEnumerator ResetViewportsNextFrame()
    {
        yield return null;
        RefreshCameraCache();
        foreach (Camera cam in managedCameras)
            cam.rect = new Rect(0f, 0f, 1f, 1f);
    }

    // Wait two frames for SetResolution to settle (extra frame for Mac reliability), then apply centred letterbox viewport.
    private IEnumerator ApplyFullscreenViewportNextFrame()
    {
        yield return null;
        yield return null;

        int scale   = CalculateFullscreenIntegerScale();
        int gameW   = config.baseWidth  * scale;
        int gameH   = config.baseHeight * scale;
        int screenW = Screen.width;
        int screenH = Screen.height;

        float vpW = (float)gameW / screenW;
        float vpH = (float)gameH / screenH;
        Rect viewport = new Rect((1f - vpW) / 2f, (1f - vpH) / 2f, vpW, vpH);

        RefreshCameraCache();
        foreach (Camera cam in managedCameras)
            cam.rect = viewport;
    }
}
