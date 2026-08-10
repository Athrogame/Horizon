using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a fixed number of coloured segments in a row as a single mesh — a lightweight stand-in
/// for a UI Image + Horizontal Layout Group when all you need is a percentage bar.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class SegmentedBar : Graphic
{
    [Tooltip("How many segments the bar is divided into.")]
    public int segmentCount = 20;
    [Tooltip("Gap between segments, in UI units.")]
    public float segmentSpacing = 2f;
    [Tooltip("Colour of a filled segment.")]
    public Color filledColor = Color.white;
    [Tooltip("Colour of an empty segment.")]
    public Color emptyColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private int filledSegments;

    /// <summary>Sets how many of the bar's segments are filled, from a value out of max.</summary>
    public void SetValue(int value, int max)
    {
        int filled = max > 0 ? Mathf.RoundToInt(Mathf.Clamp01(value / (float)max) * segmentCount) : 0;
        filled = Mathf.Clamp(filled, 0, segmentCount);
        if (filled == filledSegments) return;
        filledSegments = filled;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (segmentCount <= 0) return;

        Rect r = GetPixelAdjustedRect();
        float totalSpacing = segmentSpacing * (segmentCount - 1);
        float segmentWidth = (r.width - totalSpacing) / segmentCount;
        if (segmentWidth <= 0f) return;

        for (int i = 0; i < segmentCount; i++)
        {
            float x = r.xMin + i * (segmentWidth + segmentSpacing);
            AddQuad(vh, x, r.yMin, x + segmentWidth, r.yMax, i < filledSegments ? filledColor : emptyColor);
        }
    }

    private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(xMin, yMin), color, Vector2.zero);
        vh.AddVert(new Vector3(xMin, yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(xMax, yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(xMax, yMin), color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SetVerticesDirty();
    }
#endif
}
