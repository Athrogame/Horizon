using UnityEditor;
using UnityEngine;

// Runs automatically whenever Unity reloads scripts (via [InitializeOnLoad]).
// Sets Player Settings to match OMORI's launch behaviour:
//   - 640x480 default window size
//   - Windowed on launch (no fullscreen on startup)
//   - No free-drag window resizing (OMORI only allows x1/x2/x3/x4 via menu/keybind)
//   - Mac: Retina disabled to prevent the OS applying a second fractional DPI scale
//     on top of our integer scale, which would produce blurry pixels.
//
// These are editor-time settings only — they have no runtime overhead.
[InitializeOnLoad]
public static class ResolutionPlayerSettings
{
    static ResolutionPlayerSettings()
    {
        // Only apply if the values differ to avoid dirtying the project on every recompile.
        bool dirty = false;

        if (PlayerSettings.defaultScreenWidth != 640)  { PlayerSettings.defaultScreenWidth  = 640;                    dirty = true; }
        if (PlayerSettings.defaultScreenHeight != 480) { PlayerSettings.defaultScreenHeight = 480;                    dirty = true; }
        if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)   { PlayerSettings.fullScreenMode = FullScreenMode.Windowed; dirty = true; }
        if (PlayerSettings.runInBackground != false)   { PlayerSettings.runInBackground     = false;                  dirty = true; }
        if (PlayerSettings.resizableWindow  != false)  { PlayerSettings.resizableWindow     = false;                  dirty = true; }

#if UNITY_STANDALONE_OSX
        // macRetinaSupport is Mac-only. Disabling it prevents the OS from applying
        // an additional fractional DPI multiplier on top of our integer scale.
        if (PlayerSettings.macRetinaSupport != false)  { PlayerSettings.macRetinaSupport    = false;                  dirty = true; }
#endif

        if (dirty)
            Debug.Log("[Resolution] PlayerSettings updated to match OMORI defaults.");
    }
}
