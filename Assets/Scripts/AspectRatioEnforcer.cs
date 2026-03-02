using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectRatioEnforcer : MonoBehaviour
{
    [Header("Target Aspect Ratio")]
    [Tooltip("Desired aspect ratio (X:Y). 4:3 for Omori-style view.")]
    public Vector2 targetAspect = new Vector2(4f, 3f);

    private Camera cam;
    private float lastScreenWidth;
    private float lastScreenHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyAspect();
    }

    private void Update()
    {
        // Only recalc when resolution changes
        if (Mathf.Abs(Screen.width - lastScreenWidth) > 0.1f ||
            Mathf.Abs(Screen.height - lastScreenHeight) > 0.1f)
        {
            ApplyAspect();
        }
    }

    private void ApplyAspect()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (cam == null) return;
        if (targetAspect.y <= 0f) return;

        float target = targetAspect.x / targetAspect.y;
        float window = (float)Screen.width / Screen.height;

        // Reset to full screen by default
        Rect rect = new Rect(0f, 0f, 1f, 1f);

        if (Mathf.Approximately(window, target))
        {
            cam.rect = rect;
            return;
        }

        if (window > target)
        {
            // Window is wider than target -> pillarbox (vertical bars on sides)
            float scale = target / window;
            float xBorder = (1f - scale) * 0.5f;
            rect = new Rect(xBorder, 0f, scale, 1f);
        }
        else
        {
            // Window is taller than target -> letterbox (horizontal bars top/bottom)
            float scale = window / target;
            float yBorder = (1f - scale) * 0.5f;
            rect = new Rect(0f, yBorder, 1f, scale);
        }

        cam.rect = rect;
    }
}

