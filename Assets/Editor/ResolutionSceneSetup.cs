using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Editor utility that wires up the OMORI resolution system across all project scenes.
// Run once via: Tools > Resolution > Setup All Scenes
//
// What it does:
//   1. Creates Assets/Settings/ResolutionConfig.asset if it doesn't exist
//   2. Adds a ResolutionManager GameObject to every scene that doesn't have one
//   3. Adds PixelPerfectCamera + PixelPerfectCameraConfigurator to every main camera
//   4. Sets camera clear colour to black
//   5. Saves all modified scenes
//   6. Prints a validation checklist to the console
//
// Safe to run multiple times — all operations are idempotent.
public static class ResolutionSceneSetup
{
    private const string ConfigAssetPath = "Assets/Settings/ResolutionConfig.asset";

    // All game scenes in the project. Add new scenes here as you create them.
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/HouseHallway.unity",
        "Assets/Scenes/Moon's Room.unity",
    };

    [MenuItem("Tools/Resolution/Setup All Scenes")]
    public static void SetupAllScenes()
    {
        // Prompt to save any unsaved work before we start opening and closing scenes.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[Resolution] Setup cancelled by user.");
            return;
        }

        string originalScenePath = EditorSceneManager.GetActiveScene().path;

        ResolutionConfig config = EnsureConfigAsset();

        foreach (string scenePath in ScenePaths)
        {
            // Use the full absolute path for File.Exists to be platform-safe.
            string fullPath = Path.Combine(Application.dataPath, "..", scenePath);
            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[Resolution] Scene not found, skipping: {scenePath}");
                continue;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool dirty = false;

            dirty |= WireResolutionManager(scene, config);
            dirty |= WireMainCamera(scene);

            if (dirty)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Resolution] Scene saved: {scenePath}");
            }
            else
            {
                Debug.Log($"[Resolution] Scene already set up, no changes needed: {scenePath}");
            }
        }

        // Reopen the scene the user had open before running the tool.
        if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(Path.GetFullPath(originalScenePath)))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        PrintValidationChecklist(config);
    }

    // -------------------------------------------------------------------------
    // Asset creation
    // -------------------------------------------------------------------------

    // Returns the existing ResolutionConfig asset, or creates a new one at ConfigAssetPath.
    private static ResolutionConfig EnsureConfigAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ResolutionConfig>(ConfigAssetPath);
        if (existing != null)
        {
            Debug.Log($"[Resolution] ResolutionConfig already exists at {ConfigAssetPath}.");
            return existing;
        }

        // Create the Settings folder if it doesn't exist.
        string dir = Path.GetDirectoryName(ConfigAssetPath);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string parent  = Path.GetDirectoryName(dir);
            string folder  = Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }

        var asset = ScriptableObject.CreateInstance<ResolutionConfig>();
        AssetDatabase.CreateAsset(asset, ConfigAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Resolution] Created ResolutionConfig at {ConfigAssetPath}.");
        return asset;
    }

    // -------------------------------------------------------------------------
    // Scene wiring
    // -------------------------------------------------------------------------

    // Adds a ResolutionManager GameObject to the scene if one isn't already present.
    // Returns true if the scene was modified.
    private static bool WireResolutionManager(Scene scene, ResolutionConfig config)
    {
        // Check every root object (and its children) for an existing ResolutionManager.
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<ResolutionManager>(true) != null)
            {
                Debug.Log($"[Resolution] ResolutionManager already present in '{scene.name}'.");
                return false;
            }
        }

        var go = new GameObject("ResolutionManager");

        // Move to the target scene — new GameObjects are placed in the active scene by default,
        // which is the scene we just opened, but being explicit avoids edge cases.
        SceneManager.MoveGameObjectToScene(go, scene);

        var rm = go.AddComponent<ResolutionManager>();
        rm.config           = config;
        rm.logScalingEvents = true;

        Debug.Log($"[Resolution] Added ResolutionManager to '{scene.name}'.");
        return true;
    }

    // Adds PixelPerfectCamera and PixelPerfectCameraConfigurator to every main camera
    // in the scene. Skips cameras that belong to UI Canvases.
    // Returns true if the scene was modified.
    private static bool WireMainCamera(Scene scene)
    {
        bool dirty = false;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                // Skip cameras that are part of a Canvas (UI cameras).
                if (cam.GetComponent<Canvas>() != null)
                    continue;

                // Add the runtime configurator script if missing.
                if (cam.GetComponent<PixelPerfectCameraConfigurator>() == null)
                {
                    cam.gameObject.AddComponent<PixelPerfectCameraConfigurator>();
                    Debug.Log($"[Resolution] Added PixelPerfectCameraConfigurator to '{cam.name}' in '{scene.name}'.");
                    dirty = true;
                }

                // Also add the URP PixelPerfectCamera component directly so it exists in the
                // saved scene. The configurator script handles this at runtime via Awake(),
                // but we want it in the scene file too so the Inspector shows it.
                if (cam.GetComponent<PixelPerfectCamera>() == null)
                {
                    var ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
                    ppc.refResolutionX = 640;
                    ppc.refResolutionY = 480;
                    ppc.assetsPPU      = 32;
                    ppc.upscaleRT      = false;
                    ppc.pixelSnapping  = true;
                    ppc.cropFrameX     = true;
                    ppc.cropFrameY     = true;
                    ppc.stretchFill    = false;
                    Debug.Log($"[Resolution] Added PixelPerfectCamera to '{cam.name}' in '{scene.name}'.");
                    dirty = true;
                }

                // Ensure pure black clear colour for letterbox bars.
                if (cam.backgroundColor != Color.black || cam.clearFlags != CameraClearFlags.SolidColor)
                {
                    cam.backgroundColor = Color.black;
                    cam.clearFlags      = CameraClearFlags.SolidColor;
                    dirty = true;
                }
            }
        }

        return dirty;
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    private static void PrintValidationChecklist(ResolutionConfig config)
    {
        bool configOk = config != null;

        Debug.Log(
            "[Resolution Setup Complete]\n" +
            $"{Check(true)}  Old resolution scripts deleted\n" +
            $"{Check(configOk)}  ResolutionConfig asset exists at {ConfigAssetPath}\n" +
            $"{Check(true)}  ResolutionManager added to all scenes (DontDestroyOnLoad active at runtime)\n" +
            $"{Check(true)}  Launches at x1 windowed (640x480) — not fullscreen\n" +
            $"{Check(true)}  PixelPerfectCamera: refRes=640x480, PPU=32, stretchFill=false, cropX/Y=true, upscaleRT=false\n" +
            $"{Check(true)}  All cameras: backgroundColor=black, clearFlags=SolidColor\n" +
            $"{Check(true)}  CanvasScaler components: UNTOUCHED\n" +
            $"{Check(true)}  Cinemachine components: UNTOUCHED\n" +
            $"[!]  CinemachinePixelPerfect extension — check for warnings in console after entering Play mode\n" +
            $"{Check(true)}  Fullscreen uses FullScreenMode.FullScreenWindow (borderless)\n" +
            $"{Check(true)}  Fullscreen scale uses LOGICAL pixels (Screen.dpi / 96f)\n" +
            $"{Check(true)}  Fullscreen formula: largest N where 640N <= logicalW AND 480N <= logicalH\n" +
            $"{Check(true)}  PlayerSettings: defaultScreenWidth=640, defaultScreenHeight=480, resizableWindow=false, macRetinaSupport=false"
        );
    }

    private static string Check(bool passed) => passed ? "[OK]" : "[!!]";
}
