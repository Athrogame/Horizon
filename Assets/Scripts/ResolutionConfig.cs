using UnityEngine;

// ScriptableObject that holds all resolution/scaling constants.
// Create one via Assets > Create > Settings > Resolution Config,
// then assign it to the ResolutionManager in each scene.
[CreateAssetMenu(fileName = "ResolutionConfig", menuName = "Settings/Resolution Config")]
public class ResolutionConfig : ScriptableObject
{
    [Tooltip("Pixels per unit used by all sprites in the project.")]
    public int pixelsPerUnit = 32;

    [Tooltip("Internal render width in pixels. OMORI uses 640.")]
    public int baseWidth = 640;

    [Tooltip("Internal render height in pixels. OMORI uses 480.")]
    public int baseHeight = 480;

    [Tooltip("Integer scale multiplier applied to the window on launch. 1 = 640x480. Matches OMORI's default small window.")]
    public int defaultWindowScale = 1;

    [Tooltip("If true the game starts fullscreen. Should stay false — fullscreen is only entered via keybind or settings menu, matching OMORI.")]
    public bool startFullscreen = false;
}
