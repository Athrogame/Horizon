using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds black bars on top of the game using a UI Canvas,
/// so you get a fixed aspect window (like 4:3) with bars over the environment.
/// Attach this to a Canvas (Screen Space - Overlay or Screen Space - Camera)
/// and assign bar RectTransforms.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class LetterboxBars : MonoBehaviour
{
    [Header("Target Aspect")]
    [Tooltip("Desired aspect ratio of the visible area (e.g. 4:3).")]
    public Vector2 targetAspect = new Vector2(4f, 3f);

    [Tooltip("Multiplier for bar thickness. 1 = exact math, >1 = thicker bars, <1 = thinner.")]
    [Range(0.5f, 1.5f)]
    public float barThicknessMultiplier = 1f;

    [Tooltip("Small overscan so bars extend slightly past the screen edges to hide 1px gaps.")]
    [Range(0f, 0.1f)]
    public float edgeOverscan = 0.01f;

    [Header("Bar RectTransforms")]
    [Tooltip("Left bar (optional, used for pillarbox).")]
    public RectTransform leftBar;

    [Tooltip("Right bar (optional, used for pillarbox).")]
    public RectTransform rightBar;

    [Tooltip("Top bar (optional, used for letterbox).")]
    public RectTransform topBar;

    [Tooltip("Bottom bar (optional, used for letterbox).")]
    public RectTransform bottomBar;

    [Header("Bar Appearance")]
    [Tooltip("Color to apply to bar Images (if present).")]
    public Color barColor = Color.black;

    private void OnEnable()
    {
        ApplyBars();
    }

    private void Update()
    {
        // Recalculate when the game window size changes
        ApplyBars();
    }

    private void ApplyBars()
    {
        if (targetAspect.y <= 0f) return;

        float target = targetAspect.x / targetAspect.y;
        if (Screen.height == 0) return;

        float window = (float)Screen.width / Screen.height;

        // Wider than target -> vertical side bars (pillarbox)
        if (window > target)
        {
            float visibleWidth = target / window;          // fraction of screen width used by game
            float barFraction = (1f - visibleWidth) * 0.5f; // fraction used by each side bar

            // Allow designer to nudge bar size a bit
            barFraction *= barThicknessMultiplier;
            barFraction = Mathf.Clamp01(barFraction);

            // Left bar
            if (leftBar != null)
            {
                // Extend slightly past screen edges and overlap the play area a bit
                leftBar.anchorMin = new Vector2(-edgeOverscan, -edgeOverscan);
                leftBar.anchorMax = new Vector2(barFraction + edgeOverscan, 1f + edgeOverscan);
                leftBar.offsetMin = Vector2.zero;
                leftBar.offsetMax = Vector2.zero;
                leftBar.gameObject.SetActive(true);
                ApplyColor(leftBar);
            }

            // Right bar
            if (rightBar != null)
            {
                rightBar.anchorMin = new Vector2(1f - barFraction - edgeOverscan, -edgeOverscan);
                rightBar.anchorMax = new Vector2(1f + edgeOverscan, 1f + edgeOverscan);
                rightBar.offsetMin = Vector2.zero;
                rightBar.offsetMax = Vector2.zero;
                rightBar.gameObject.SetActive(true);
                ApplyColor(rightBar);
            }

            // Hide top/bottom bars if they exist
            if (topBar != null) topBar.gameObject.SetActive(false);
            if (bottomBar != null) bottomBar.gameObject.SetActive(false);
        }
        // Taller than target -> horizontal bars (letterbox)
        else if (window < target)
        {
            float visibleHeight = window / target;          // fraction of screen height used by game
            float barFraction = (1f - visibleHeight) * 0.5f; // fraction used by each bar

            barFraction *= barThicknessMultiplier;
            barFraction = Mathf.Clamp01(barFraction);

            // Top bar
            if (topBar != null)
            {
                topBar.anchorMin = new Vector2(-edgeOverscan, 1f - barFraction);
                topBar.anchorMax = new Vector2(1f + edgeOverscan, 1f);
                topBar.offsetMin = Vector2.zero;
                topBar.offsetMax = Vector2.zero;
                topBar.gameObject.SetActive(true);
                ApplyColor(topBar);
            }

            // Bottom bar
            if (bottomBar != null)
            {
                bottomBar.anchorMin = new Vector2(-edgeOverscan, 0f);
                bottomBar.anchorMax = new Vector2(1f + edgeOverscan, barFraction);
                bottomBar.offsetMin = Vector2.zero;
                bottomBar.offsetMax = Vector2.zero;
                bottomBar.gameObject.SetActive(true);
                ApplyColor(bottomBar);
            }

            // Hide left/right bars if they exist
            if (leftBar != null) leftBar.gameObject.SetActive(false);
            if (rightBar != null) rightBar.gameObject.SetActive(false);
        }
        else
        {
            // Exactly target aspect: hide all bars
            if (leftBar != null) leftBar.gameObject.SetActive(false);
            if (rightBar != null) rightBar.gameObject.SetActive(false);
            if (topBar != null) topBar.gameObject.SetActive(false);
            if (bottomBar != null) bottomBar.gameObject.SetActive(false);
        }
    }

    private void ApplyColor(RectTransform rect)
    {
        var img = rect.GetComponent<Image>();
        if (img != null)
        {
            img.color = barColor;
        }
    }
}

