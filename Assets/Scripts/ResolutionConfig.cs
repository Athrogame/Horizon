using UnityEngine;

// ScriptableObject holding all resolution and scale constants for the game.
// Create one via: Assets > Create > Settings > Resolution Config
// Then assign it to the ResolutionManager in each scene.
[CreateAssetMenu(fileName = "ResolutionConfig", menuName = "Settings/Resolution Config")]
public class ResolutionConfig : ScriptableObject
{
    [Tooltip("Pixels per unit used by all sprites in the project.")]
    public int pixelsPerUnit = 32;

    [Tooltip("Internal render width in pixels.")]
    public int baseWidth = 640;

    [Tooltip("Internal render height in pixels.")]
    public int baseHeight = 480;

    [Tooltip("Integer scale multiplier applied to the window on launch. 1 = native resolution.")]
    public int defaultWindowScale = 1;

    [Tooltip("If true the game starts fullscreen. Recommended to leave false — fullscreen is entered via keybind only.")]
    public bool startFullscreen = false;
}
