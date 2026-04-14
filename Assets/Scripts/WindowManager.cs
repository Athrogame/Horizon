using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// RPG Maker MV/MZ-style fullscreen.
///
/// Windowed   : 816x624 window, centered on the monitor.
/// Fullscreen : Borderless fullscreen at the monitor's native resolution.
///              The game is rendered at the largest integer multiple of 816x624
///              that fits on screen (1x, 2x, …), centered, with black bars on
///              all four sides — identical to RPG Maker MV/MZ behavior.
///
///              PixelPerfectCamera is disabled while fullscreen so it cannot
///              override Camera.rect.
///
///              All Screen Space – Overlay canvases are switched to
///              Screen Space – Camera while fullscreen so they stay inside
///              the game viewport and do not bleed into the black bars.
///              They are restored to Overlay when returning to windowed.
///
/// Toggle with F4 or Alt+Enter.
/// Attach to any persistent GameObject.
/// </summary>
public class WindowManager : MonoBehaviour
{
    private const int W = 816;
    private const int H = 624;

    private bool _fullscreen;
    private PixelPerfectCamera _ppc;

#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        System.IntPtr hWnd, System.IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOZORDER   = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static System.IntPtr GetHwnd() =>
        System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
#endif

    // Force windowed at 816x624 before the first frame so there is no
    // fullscreen flash on startup.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceWindowedEarly()
    {
        Screen.SetResolution(W, H, FullScreenMode.Windowed);
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _fullscreen = false;
        CachePixelPerfectCamera();
        // Re-apply canvas mode whenever a new scene is loaded.
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(CenterWindowNextFrame());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // When a scene loads while already in fullscreen, the new scene's canvases
    // will default to Overlay — switch them immediately.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CachePixelPerfectCamera();
        if (_fullscreen)
            StartCoroutine(ApplyIntegerScaleLetterbox());
    }

    // Re-fetch on demand so scene loads don't leave us with a stale reference.
    private void CachePixelPerfectCamera()
    {
        if (Camera.main != null)
            _ppc = Camera.main.GetComponent<PixelPerfectCamera>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool f4       = kb.f4Key.wasPressedThisFrame;
        bool altEnter = kb.enterKey.wasPressedThisFrame &&
                        (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

        if (f4 || altEnter) Toggle();
    }

    private void Toggle()
    {
        if (_fullscreen) GoWindowed();
        else             GoFullscreen();
    }

    private void GoWindowed()
    {
        _fullscreen = false;

        // Restore camera before switching resolution.
        CachePixelPerfectCamera();
        if (_ppc != null) _ppc.enabled = true;
        if (Camera.main != null) Camera.main.rect = new Rect(0f, 0f, 1f, 1f);

        // Switch all canvases back to Screen Space – Overlay.
        SetCanvasMode(overlay: true);

        Screen.SetResolution(W, H, FullScreenMode.Windowed);
        StartCoroutine(CenterWindowNextFrame());
    }

    private void GoFullscreen()
    {
        _fullscreen = true;

        // Disable PixelPerfectCamera so it cannot override Camera.rect.
        CachePixelPerfectCamera();
        if (_ppc != null) _ppc.enabled = false;

        // Switch to the monitor's actual resolution (borderless fullscreen).
        Resolution native = Screen.currentResolution;
        Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);

        StartCoroutine(ApplyIntegerScaleLetterbox());
    }

    // Calculates the largest integer scale where 816x624 fits on screen,
    // centers the camera viewport, and switches canvases to Screen Space – Camera
    // so they are confined to the game area.
    private IEnumerator ApplyIntegerScaleLetterbox()
    {
        yield return null;
        yield return null; // wait for SetResolution to settle

        float screenW = Screen.width;
        float screenH = Screen.height;

        // Integer scale: 1x, 2x, 3x … — whichever is the biggest that fits.
        int scale = Mathf.Max(1, Mathf.Min(
            Mathf.FloorToInt(screenW / W),
            Mathf.FloorToInt(screenH / H)
        ));

        float renderW = W * scale;
        float renderH = H * scale;

        // Camera.rect uses 0–1 normalized coordinates.
        float x = (screenW - renderW) / 2f / screenW;
        float y = (screenH - renderH) / 2f / screenH;

        var viewportRect = new Rect(x, y, renderW / screenW, renderH / screenH);

        if (Camera.main != null)
            Camera.main.rect = viewportRect;

        // Switch canvases to Screen Space – Camera so they respect the viewport.
        SetCanvasMode(overlay: false);
    }

    // Switches every Canvas in the scene between Screen Space – Overlay and
    // Screen Space – Camera. In Camera mode the canvas is bound to Camera.main's
    // viewport rect, so it stays inside the game area and out of the black bars.
    private void SetCanvasMode(bool overlay)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            // Only touch root canvases — child canvases inherit from their parent.
            if (canvas.transform.parent != null) continue;

            if (overlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
            }
        }
    }

    // Center the windowed window on the primary monitor (Windows builds only).
    private IEnumerator CenterWindowNextFrame()
    {
        yield return null;
        yield return null;  // two frames for SetResolution + OS border to settle

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        int sw   = Screen.currentResolution.width;
        int sh   = Screen.currentResolution.height;
        var hwnd = GetHwnd();
        if (hwnd != System.IntPtr.Zero)
            SetWindowPos(hwnd, System.IntPtr.Zero,
                (sw - W) / 2, (sh - H) / 2,
                0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
#endif
    }
}
