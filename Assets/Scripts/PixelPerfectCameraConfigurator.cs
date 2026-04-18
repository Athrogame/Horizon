using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// Configures the PixelPerfectCamera and Camera on this GameObject at startup.
// Attach this to the same GameObject as the main Camera in every scene.
[RequireComponent(typeof(Camera))]
public class PixelPerfectCameraConfigurator : MonoBehaviour
{
    [Tooltip("Pixels per unit used by all sprites. Must match the sprite import setting.")]
    public float pixelsPerUnit = 32;

    private void Awake()
    {
        ConfigurePixelPerfectCamera();
        ConfigureCamera();
        CheckCinemachineExtensions();
    }

    private void ConfigurePixelPerfectCamera()
    {
        var ppc = GetComponent<PixelPerfectCamera>() ?? gameObject.AddComponent<PixelPerfectCamera>();
        ppc.refResolutionX = 640;
        ppc.refResolutionY = 480;
        ppc.assetsPPU      = 32;
        ppc.gridSnapping   = PixelPerfectCamera.GridSnapping.PixelSnapping;
        ppc.cropFrame      = PixelPerfectCamera.CropFrame.Windowbox;
    }

    // Pure black background so letterbox bars are invisible.
    private void ConfigureCamera()
    {
        var cam = GetComponent<Camera>();
        cam.backgroundColor = Color.black;
        cam.clearFlags      = CameraClearFlags.SolidColor;
    }

    // Warns if any Cinemachine camera is missing the CinemachinePixelPerfect extension — add it manually in the Inspector.
    private void CheckCinemachineExtensions()
    {
        foreach (var vcam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (vcam.GetComponent<CinemachinePixelPerfect>() == null)
                Debug.LogWarning($"[PixelPerfect] '{vcam.name}' is missing the CinemachinePixelPerfect extension. Add it in the Inspector.");
        }
    }

    // Snaps the camera position to the pixel grid every frame to prevent sub-pixel jitter.
    private void LateUpdate()
    {
        float snap = 1f / pixelsPerUnit;
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x / snap) * snap;
        pos.y = Mathf.Round(pos.y / snap) * snap;
        transform.position = pos;
    }
}
