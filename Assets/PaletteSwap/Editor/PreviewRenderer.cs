using System.Collections.Generic;
using UnityEngine;

namespace PaletteSwap
{
    public static class PreviewRenderer
    {
        // In-memory only: crops one sprite out of the decoded sheet and applies the mapping.
        // Nothing touches the AssetDatabase, so this is safe to call every repaint.
        public static Texture2D Render(Texture2D sheet, Sprite sprite, IReadOnlyList<ColorMapping> mappings)
        {
            if (sheet == null || sprite == null) return null;

            var r = ColorExtractor.SpriteRect(sprite, sheet.width, sheet.height);
            if (r.width <= 0 || r.height <= 0) return null;

            var sheetPixels = sheet.GetPixels32();
            var cropped = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                    cropped[y * r.width + x] = sheetPixels[(r.yMin + y) * sheet.width + (r.xMin + x)];

            var tex = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(PaletteBaker.SwapColors(cropped, mappings));
            tex.Apply();
            return tex;
        }
    }
}
