using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// Configures the PixelPerfectCamera on this GameObject to match OMORI's pixel rendering.
// Attach this to the same GameObject as the main Camera in every scene.
//
// Settings applied:
//   refResolution  640x480    — fixed internal canvas size
//   assetsPPU      32         — must match your sprite import PPU
//   upscaleRT      false      — ResolutionManager's viewport handles letterboxing, not the RT
//   pixelSnapping  true       — snaps sprites to pixel grid, prevents sub-pixel wobble
//   cropFrameX/Y   true       — pad with black if aspect doesn't fit exactly
//   stretchFill    false      — NEVER stretch to fill; integer scale only
[RequireComponent(typeof(Camera))]
public class PixelPerfectCameraConfigurator : MonoBehaviour
{
    private void Awake()
    {
        ConfigurePixelPerfectCamera();
        ConfigureCamera();
        CheckCinemachineExtensions();
    }

    // Add (if missing) and configure the URP PixelPerfectCamera component.
    private void ConfigurePixelPerfectCamera()
    {
        var ppc = GetComponent<PixelPerfectCamera>();
        if (ppc == null)
            ppc = gameObject.AddComponent<PixelPerfectCamera>();

        ppc.refResolutionX = 640;
        ppc.refResolutionY = 480;
        ppc.assetsPPU      = 32;
        ppc.upscaleRT      = false;  // viewport-based letterboxing, not RT upscaling
        ppc.pixelSnapping  = true;   // snap world objects to pixel grid
        ppc.cropFrameX     = true;   // black pad on left/right if needed
        ppc.cropFrameY     = true;   // black pad on top/bottom if needed
        ppc.stretchFill    = false;  // absolutely never stretch — most critical setting
    }

    // Set the camera clear colour to pure black so letterbox bars are black,
    // matching OMORI's black border behaviour.
    private void ConfigureCamera()
    {
        var cam = GetComponent<Camera>();
        cam.backgroundColor = Color.black;
        cam.clearFlags      = CameraClearFlags.SolidColor;
    }

    // Warn (but do NOT modify) if any CinemachineCamera in the scene is missing the
    // CinemachinePixelPerfect extension. That extension must be added manually via the Inspector.
    private void CheckCinemachineExtensions()
    {
        var vcams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var vcam in vcams)
        {
            if (vcam.GetComponent<CinemachinePixelPerfect>() == null)
                Debug.LogWarning($"[Resolution] '{vcam.name}' is missing the CinemachinePixelPerfect extension. Add it manually in the Inspector.");
        }
    }
    public float pixelsPerUnit = 32;
    void LateUpdate()
    {
        float unitsPerPixel = 1f / pixelsPerUnit;

        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x / unitsPerPixel) * unitsPerPixel;
        pos.y = Mathf.Round(pos.y / unitsPerPixel) * unitsPerPixel;

        transform.position = pos;
    }
}
