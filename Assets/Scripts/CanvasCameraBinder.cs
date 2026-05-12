using UnityEngine;

// Attach to a Screen Space - Camera Canvas. Keeps worldCamera bound to the active
// MainCamera so the reference doesn't end up "Missing (Camera)" after a scene reload —
// when that happens the canvas falls back to Overlay-style rendering at the wrong scale.
[RequireComponent(typeof(Canvas))]
public class CanvasCameraBinder : MonoBehaviour
{
    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        BindCamera();
    }

    private void OnEnable()
    {
        BindCamera();
    }

    private void LateUpdate()
    {
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera) return;
        if (canvas.worldCamera == null)
            BindCamera();
    }

    private void BindCamera()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas.renderMode != RenderMode.ScreenSpaceCamera) return;

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cam.CompareTag("MainCamera"))
            {
                canvas.worldCamera = cam;
                return;
            }
        }
    }
}
