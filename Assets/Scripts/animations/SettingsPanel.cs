using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One tab's page inside the Settings menu (Video / Audio / Exit).
///
/// Owns the "sheet of paper" show/hide animation (the same slide + settle pop the save-slot menu
/// uses) and the two arrow cursors: a SECTION arrow that points at the current row, and one OPTION
/// arrow per row that points at the chosen value.
///
/// It only moves arrows and reports choices — <see cref="SettingsMenu"/> drives it, and the value
/// of each choice is interpreted by whoever listens to <see cref="onOptionChosen"/>.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    /// <summary>One selectable line on the page, e.g. "Scale:" with its 1x/2x/3x/4x choices.</summary>
    [Serializable]
    public class Row
    {
        [Tooltip("The row's heading text, e.g. 'Scale:' or 'Fullscreen'. The section arrow lines up with this.")]
        public RectTransform label;

        [Tooltip("The arrow that sits beside the currently-chosen value on THIS row.")]
        public RectTransform optionArrow;

        [Tooltip("The choices on this row, left to right, e.g. 1x / 2x / 3x / 4x. Leave empty for a value row.")]
        public List<TextMeshProUGUI> options = new List<TextMeshProUGUI>();

        [Header("Value row (volumes) — instead of a list of choices")]
        [Tooltip("Tick for a row that holds a NUMBER the player nudges left/right (e.g. Master volume) " +
                 "rather than a list of separate choices.")]
        public bool isValueRow;
        [Tooltip("The single text that shows the value, e.g. '▮▮▮▮▮▯▯▯▯▯ 50%'. The option arrow points at it.")]
        public TextMeshProUGUI valueLabel;
        [Tooltip("Highest value this row can reach. The lowest is always 0.")]
        public int maxValue = 10;
    }

    [Header("Rows (top to bottom — the section arrow moves between these)")]
    public List<Row> rows = new List<Row>();

    [Header("Section cursor")]
    [Tooltip("The single arrow that points at whichever ROW is currently focused.")]
    public RectTransform sectionArrow;
    [Tooltip("Horizontal gap between an arrow's right edge and the thing it points at, in UI units.")]
    public float arrowGap = 8f;
    [Tooltip("Fine nudge for the section arrow only (the one beside 'Scale:' / 'Fullscreen'), in UI units — X = right, Y = up.")]
    public Vector2 sectionArrowOffset = new Vector2(6f, 4f);

    [Header("Value rows")]
    [Tooltip("Character drawn for each filled step of a value row's bar.")]
    public string barFilledChar = "#";
    [Tooltip("Character drawn for each empty step of a value row's bar.")]
    public string barEmptyChar = "-";

    [Header("Chosen-value highlight")]
    [Tooltip("Colour of the value currently in effect on each row.")]
    public Color chosenColor = Color.white;
    [Tooltip("Colour of the values NOT in effect.")]
    public Color unchosenColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Paper animation (matches the save-slot menu)")]
    [Tooltip("How long the page takes to slide in from below / back out, in seconds.")]
    public float slideDuration = 0.35f;
    [Tooltip("Scale the page is held at while sliding in, before it pops to full size (1 = no pop).")]
    public float slideStartScale = 0.85f;
    [Tooltip("How long the 'placed down' pop to full size takes after the slide finishes.")]
    public float settleDuration = 0.35f;

    /// <summary>Raised when the player confirms a value: (rowIndex, optionIndex).</summary>
    public event Action<int, int> onOptionChosen;

    // The page's on-screen resting spot / size, captured once before it's ever moved.
    private RectTransform rect;
    private Vector2 restPos;
    private Vector3 baseScale;
    private bool restCaptured;

    private Coroutine paperRoutine;

    // Which option is currently in effect on each row (parallel to rows).
    private readonly List<int> chosenIndices = new List<int>();

    private void Awake()
    {
        CaptureRest();
    }

    private void CaptureRest()
    {
        if (restCaptured) return;
        rect = GetComponent<RectTransform>();
        if (rect == null) return;
        restPos = rect.anchoredPosition;
        baseScale = rect.localScale;
        restCaptured = true;
    }

    /// <summary>Number of selectable rows on this page. 0 means the page is informational only.</summary>
    public int RowCount => rows.Count;

    /// <summary>How many values the given row offers (a value row offers 0..maxValue).</summary>
    public int OptionCount(int row)
    {
        if (!IsValidRow(row)) return 0;
        return rows[row].isValueRow ? rows[row].maxValue + 1 : rows[row].options.Count;
    }

    /// <summary>True if the row is a number the player nudges left/right rather than a list of choices.</summary>
    public bool IsValueRow(int row) => IsValidRow(row) && rows[row].isValueRow;

    /// <summary>The option currently in effect on the given row.</summary>
    public int GetChosen(int row)
    {
        EnsureChosenList();
        return IsValidRow(row) ? chosenIndices[row] : 0;
    }

    /// <summary>
    /// Sets a row's value without raising <see cref="onOptionChosen"/> — used to seed the menu from
    /// saved settings when it opens.
    /// </summary>
    public void SetChosen(int row, int option)
    {
        EnsureChosenList();
        if (!IsValidRow(row)) return;
        chosenIndices[row] = Mathf.Clamp(option, 0, Mathf.Max(0, OptionCount(row) - 1));
        RefreshRowColors(row);
        PointOptionArrow(row, chosenIndices[row]);
    }

    /// <summary>Confirms the given option on the given row: stores it, recolours, and notifies listeners.</summary>
    public void ChooseOption(int row, int option)
    {
        SetChosen(row, option);
        onOptionChosen?.Invoke(row, GetChosen(row));
    }

    // ---------------------------------------------------------------- cursors

    /// <summary>Moves the section arrow beside the given row's label. Pass -1 to hide it.</summary>
    public void PointSectionArrow(int row)
    {
        if (sectionArrow == null) return;

        if (!IsValidRow(row) || rows[row].label == null)
        {
            sectionArrow.gameObject.SetActive(false);
            return;
        }

        sectionArrow.gameObject.SetActive(true);
        PlaceArrowLeftOf(sectionArrow, rows[row].label, sectionArrowOffset);
    }

    /// <summary>Moves a row's option arrow beside the given value. Used both for browsing and confirming.</summary>
    public void PointOptionArrow(int row, int option)
    {
        if (!IsValidRow(row)) return;
        RectTransform arrow = rows[row].optionArrow;
        if (arrow == null) return;

        // A value row has one target: its number. A choice row has one per option.
        RectTransform target = null;
        if (rows[row].isValueRow)
        {
            if (rows[row].valueLabel != null) target = rows[row].valueLabel.rectTransform;
        }
        else if (option >= 0 && option < rows[row].options.Count && rows[row].options[option] != null)
        {
            target = rows[row].options[option].rectTransform;
        }

        if (target == null)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        arrow.gameObject.SetActive(true);
        PlaceArrowLeftOf(arrow, target);
    }

    /// <summary>
    /// Parks every arrow where the saved values sit, and hides the section arrow — the page's
    /// resting look while the player is still up in the tab bar.
    /// </summary>
    public void ResetCursors()
    {
        EnsureChosenList();
        for (int i = 0; i < rows.Count; i++)
        {
            PointOptionArrow(i, chosenIndices[i]);
            RefreshRowColors(i);
        }
        PointSectionArrow(-1);
    }

    // Puts an arrow just left of a target, vertically centred on it.
    //
    // Works off localPosition rather than anchoredPosition so it doesn't matter how the arrow is
    // anchored, and converts through world space so the arrow and its target can live under
    // different parents (e.g. one shared section arrow pointing at rows nested in sub-groups).
    private void PlaceArrowLeftOf(RectTransform arrow, RectTransform target, Vector2 extraOffset = default)
    {
        RectTransform parent = arrow.parent as RectTransform;
        if (parent == null) return;

        // Force any layout groups to finish rebuilding, then the canvas geometry, before reading
        // any rect sizes or positions. Without this, rect.xMin / rect.center can be stale or zero
        // on the first frame the panel (or a layout group inside it) is (re)activated.
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootCanvas.rootCanvas.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();

        // Every label/option/arrow here sits in an oversized box copy-pasted from a shared text
        // template, with the glyph off-centre inside it by a different amount each time depending
        // on that box's own padding — so BOTH the edge we aim at and the arrow we're placing need
        // to be measured by their actual rendered glyph bounds, not their RectTransform box.
        TextMeshProUGUI targetText = target.GetComponent<TextMeshProUGUI>();
        Vector2 targetLeftEdge = new Vector2(target.rect.xMin, target.rect.center.y);
        if (targetText != null)
        {
            targetText.ForceMeshUpdate();
            Bounds tb = targetText.textBounds;
            targetLeftEdge = new Vector2(tb.min.x, tb.center.y);
        }

        // Middle of the target's left edge, in the arrow's parent space.
        Vector3 leftEdgeWorld = target.TransformPoint(new Vector3(targetLeftEdge.x, targetLeftEdge.y, 0f));
        Vector3 leftEdgeLocal = parent.InverseTransformPoint(leftEdgeWorld);

        TextMeshProUGUI arrowText = arrow.GetComponent<TextMeshProUGUI>();
        float glyphHalfWidth = arrow.rect.width * 0.5f;
        Vector2 glyphCentreOffset = Vector2.zero;   // how far the glyph's own centre sits from the box's pivot
        if (arrowText != null)
        {
            arrowText.ForceMeshUpdate();
            Bounds b = arrowText.textBounds;
            glyphHalfWidth = b.extents.x;
            glyphCentreOffset = new Vector2(b.center.x, b.center.y);
        }

        // Where the glyph's CENTRE should end up.
        float centreX = leftEdgeLocal.x - arrowGap - glyphHalfWidth;
        float centreY = leftEdgeLocal.y;

        // localPosition moves the arrow's PIVOT, which isn't necessarily its centre or the glyph's.
        Vector3 pos = arrow.localPosition;
        pos.x = centreX + (arrow.pivot.x - 0.5f) * arrow.rect.width - glyphCentreOffset.x + extraOffset.x;
        pos.y = centreY + (arrow.pivot.y - 0.5f) * arrow.rect.height - glyphCentreOffset.y + extraOffset.y;
        arrow.localPosition = pos;
    }

    private void RefreshRowColors(int row)
    {
        if (!IsValidRow(row)) return;
        EnsureChosenList();

        if (rows[row].isValueRow)
        {
            if (rows[row].valueLabel != null)
            {
                rows[row].valueLabel.text = BuildBar(chosenIndices[row], rows[row].maxValue);
                rows[row].valueLabel.color = chosenColor;
            }
            return;
        }

        List<TextMeshProUGUI> options = rows[row].options;
        for (int i = 0; i < options.Count; i++)
            if (options[i] != null)
                options[i].color = (i == chosenIndices[row]) ? chosenColor : unchosenColor;
    }

    // "###-------  30%" — a fixed-width bar plus the percentage, so it reads at a glance.
    private string BuildBar(int value, int max)
    {
        if (max <= 0) return "0%";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < max; i++)
            sb.Append(i < value ? barFilledChar : barEmptyChar);
        sb.Append("  ").Append(Mathf.RoundToInt(value * 100f / max)).Append('%');
        return sb.ToString();
    }

    private bool IsValidRow(int row) => row >= 0 && row < rows.Count;

    private void EnsureChosenList()
    {
        while (chosenIndices.Count < rows.Count) chosenIndices.Add(0);
        while (chosenIndices.Count > rows.Count) chosenIndices.RemoveAt(chosenIndices.Count - 1);
    }

    // ---------------------------------------------------------------- paper animation

    /// <summary>
    /// Slides the page in (show) or out (hide) like a sheet of paper being placed down / taken away.
    /// Returns a coroutine the caller can yield on, so input stays locked for the whole animation.
    /// </summary>
    public Coroutine Play(bool show)
    {
        CaptureRest();
        if (paperRoutine != null) StopCoroutine(paperRoutine);
        gameObject.SetActive(true);   // must be active before StartCoroutine can run on this object
        paperRoutine = StartCoroutine(PaperRoutine(show));
        return paperRoutine;
    }

    /// <summary>Hides the page instantly, with no animation (used when the menu first initialises).</summary>
    public void HideInstant()
    {
        CaptureRest();
        if (paperRoutine != null) { StopCoroutine(paperRoutine); paperRoutine = null; }
        if (rect != null)
        {
            rect.anchoredPosition = restPos;
            rect.localScale = baseScale;
        }
        gameObject.SetActive(false);
    }

    // How far down the page has to travel to be fully out of sight.
    //
    // Just enough for its own top edge to clear the bottom of the canvas — so the big outer sheet
    // swings the full screen height while a small inner page only slides its own height plus a
    // little. Both read as the same "sheet of paper" move, but the small one doesn't fly.
    private float HiddenDrop()
    {
        float canvasHeight = Screen.height;
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            canvasHeight = rootCanvas.rootCanvas.GetComponent<RectTransform>().rect.height;

        // Distance from the page's top edge down to the bottom of the canvas, in canvas units.
        float topEdgeFromBottom = canvasHeight * 0.5f;
        if (rootCanvas != null)
        {
            RectTransform canvasRect = rootCanvas.rootCanvas.GetComponent<RectTransform>();
            Vector3 topWorld = rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f));
            float topInCanvas = canvasRect.InverseTransformPoint(topWorld).y;
            topEdgeFromBottom = topInCanvas - canvasRect.rect.yMin;
        }

        return Mathf.Min(canvasHeight, topEdgeFromBottom + 16f);   // 16 = a little clearance
    }

    private IEnumerator PaperRoutine(bool show)
    {
        gameObject.SetActive(true);

        if (rect == null)
        {
            if (!show) gameObject.SetActive(false);
            yield break;
        }

        // Measure from the resting spot, not wherever a half-finished animation left it.
        rect.anchoredPosition = restPos;
        Vector2 hiddenPos = restPos + Vector2.down * HiddenDrop();

        Vector2 from = show ? hiddenPos : restPos;
        Vector2 to = show ? restPos : hiddenPos;

        rect.anchoredPosition = from;

        // Phase 1 — glide into place, held slightly small on the way in.
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float raw = Mathf.Clamp01(t / slideDuration);
            float k = show ? EaseOutCubic(raw) : EaseInCubic(raw);
            rect.anchoredPosition = Vector2.Lerp(from, to, k);
            if (show) rect.localScale = baseScale * slideStartScale;
            yield return null;
        }
        rect.anchoredPosition = to;

        // Phase 2 (show only) — the "placed down" pop: small -> overshoot -> full size.
        if (show)
        {
            float s = 0f;
            while (s < settleDuration)
            {
                s += Time.unscaledDeltaTime;
                float k = EaseOutBack(Mathf.Clamp01(s / settleDuration));
                rect.localScale = baseScale * Mathf.LerpUnclamped(slideStartScale, 1f, k);
                yield return null;
            }
        }
        rect.localScale = baseScale;

        if (!show)
        {
            rect.anchoredPosition = restPos;   // re-arm for next time
            gameObject.SetActive(false);
        }

        paperRoutine = null;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float EaseInCubic(float x) => x * x * x;

    // Overshoots past 1 then settles back — the scale "settle" pop.
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
