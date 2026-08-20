using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PaletteSwap
{
    public struct ColorCount
    {
        public Color32 color;
        public int count;
    }

    public enum MergeMode
    {
        Exact,
        Shades,
        Custom
    }

    public struct MergeSettings
    {
        public MergeMode mode;
        public int tolerance;
        public float strength;
        public bool keepShadowsSeparate;

        public static MergeSettings Default => new()
        {
            mode = MergeMode.Shades,
            tolerance = 8,
            strength = 0.5f,
            keepShadowsSeparate = true
        };
    }

    // One swatch in the UI. The representative is the most-used color in the group; the other
    // members are its darker/lighter siblings, remapped relative to it so the ramp survives.
    public class ColorGroup
    {
        public Color32 representative;
        public List<Color32> members = new();
        public int pixelCount;
        public int representativeCount;

        public int ShadeCount => members.Count;

        // Recolor the swatch and every member moves with it, keeping its position in the ramp:
        // pick light green for light grey and the dark grey member lands on dark green.
        public IEnumerable<ColorMapping> MappingsTo(Color32 target)
        {
            Color.RGBToHSV(representative, out float rh, out float rs, out float rv);
            Color.RGBToHSV(target, out float th, out float ts, out float tv);

            foreach (var member in members)
            {
                if (ColorwayAsset.Eq(member, representative))
                {
                    yield return new ColorMapping
                    {
                        from = member,
                        to = new Color32(target.r, target.g, target.b, member.a)
                    };
                    continue;
                }

                Color.RGBToHSV(member, out float mh, out float ms, out float mv);

                float h = Mathf.Repeat(th + Mathf.DeltaAngle(rh * 360f, mh * 360f) / 360f, 1f);
                float s = Mathf.Clamp01(ts + (ms - rs));
                // Value moves multiplicatively — shading in pixel art reads as a ratio, not an offset.
                float v = rv > 0.001f
                    ? Mathf.Clamp01(tv * (mv / rv))
                    : Mathf.Clamp01(tv + (mv - rv));

                var rgb = Color.HSVToRGB(h, s, v);
                yield return new ColorMapping
                {
                    from = member,
                    to = new Color32(
                        (byte)Mathf.RoundToInt(rgb.r * 255f),
                        (byte)Mathf.RoundToInt(rgb.g * 255f),
                        (byte)Mathf.RoundToInt(rgb.b * 255f),
                        member.a)
                };
            }
        }
    }

    public static class ColorExtractor
    {
        public static List<Color32> Extract(Texture2D tex) =>
            FilterAndSort(tex.GetPixels32());

        public static List<Color32> ExtractUnion(IEnumerable<Texture2D> textures)
        {
            var all = new List<Color32>();
            foreach (var tex in textures)
                all.AddRange(tex.GetPixels32());
            return FilterAndSort(all);
        }

        public static List<Color32> ExtractFromPath(string assetPath)
        {
            var tex = LoadFromPath(assetPath);
            var result = Extract(tex);
            Object.DestroyImmediate(tex);
            return result;
        }

        public static List<Color32> ExtractUnionFromPaths(IEnumerable<string> assetPaths)
        {
            var textures = new List<Texture2D>();
            foreach (var path in assetPaths)
                textures.Add(LoadFromPath(path));
            var result = ExtractUnion(textures);
            foreach (var t in textures)
                Object.DestroyImmediate(t);
            return result;
        }

        public static List<Color32> ExtractUnionFromSprites(IEnumerable<Sprite> sprites) =>
            FilterAndSort(SpritePixels(sprites));

        public static List<ColorCount> CountFromSprites(IEnumerable<Sprite> sprites)
        {
            var counts = new Dictionary<uint, ColorCount>();
            foreach (var pixel in SpritePixels(sprites))
            {
                if (pixel.a == 0) continue;
                uint key = Key(pixel);
                counts[key] = counts.TryGetValue(key, out var existing)
                    ? new ColorCount { color = existing.color, count = existing.count + 1 }
                    : new ColorCount { color = pixel, count = 1 };
            }
            return counts.Values.OrderByDescending(c => c.count).ToList();
        }

        // Greedy clustering seeded by pixel count, so the dominant color wins the swatch
        // and its shades fold into it rather than the other way round. A second agglomerative
        // stage then joins groups whose seeds turned out to be related, which one greedy pass
        // always misses: A matches B and B matches C, but C was never compared against A.
        public static List<ColorGroup> Group(IReadOnlyList<ColorCount> counts, MergeSettings settings)
        {
            var groups = new List<ColorGroup>();

            foreach (var entry in counts.OrderByDescending(c => c.count))
            {
                ColorGroup match = null;
                if (settings.mode != MergeMode.Exact)
                    foreach (var group in groups)
                        if (Belongs(group.representative, entry.color, settings))
                        {
                            match = group;
                            break;
                        }

                if (match == null)
                {
                    match = new ColorGroup
                    {
                        representative = entry.color,
                        representativeCount = entry.count
                    };
                    groups.Add(match);
                }

                match.members.Add(entry.color);
                match.pixelCount += entry.count;
            }

            if (settings.mode != MergeMode.Exact) Coalesce(groups, settings);

            groups.Sort((a, b) => CompareForDisplay(a.representative, b.representative));
            return groups;
        }

        static void Coalesce(List<ColorGroup> groups, MergeSettings settings)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                bool anyMerged = false;

                for (int i = 0; i < groups.Count; i++)
                    for (int j = groups.Count - 1; j > i; j--)
                    {
                        if (!Belongs(groups[i].representative, groups[j].representative, settings)) continue;

                        var keep = groups[i].representativeCount >= groups[j].representativeCount
                            ? groups[i] : groups[j];
                        var fold = ReferenceEquals(keep, groups[i]) ? groups[j] : groups[i];

                        keep.members.AddRange(fold.members);
                        keep.pixelCount += fold.pixelCount;
                        groups[i] = keep;
                        groups.RemoveAt(j);
                        anyMerged = true;
                    }

                if (!anyMerged) return;
            }
        }

        // Near-black outlines stay their own swatch — folding them into a ramp recolors outlines.
        const float ShadowCutoff = 0.10f;

        static float HueWindow(float strength) => Mathf.Lerp(10f, 48f, strength);
        static float SaturationWindow(float strength) => Mathf.Lerp(0.15f, 0.65f, strength);
        static float GreyCutoff(float strength) => Mathf.Lerp(0.08f, 0.22f, strength);

        static bool Belongs(Color32 representative, Color32 candidate, MergeSettings settings) =>
            settings.mode == MergeMode.Custom
                ? settings.tolerance > 0 && Distance(representative, candidate) <= settings.tolerance
                : SameRamp(representative, candidate, settings);

        // Same hue family, any lightness: that is what a shading ramp looks like.
        static bool SameRamp(Color32 a, Color32 b, MergeSettings settings)
        {
            Color.RGBToHSV(a, out float ha, out float sa, out float va);
            Color.RGBToHSV(b, out float hb, out float sb, out float vb);

            if (settings.keepShadowsSeparate && va < ShadowCutoff != vb < ShadowCutoff) return false;

            float greyCut = GreyCutoff(settings.strength);
            bool aGrey = sa < greyCut;
            bool bGrey = sb < greyCut;

            if (aGrey && bGrey) return true;
            // A barely-tinted color still belongs with the greys rather than starting its own ramp.
            // Falls through to the hue test rather than returning, so raising strength never
            // un-merges a pair that a lower strength already accepted.
            if (aGrey != bGrey && Mathf.Max(sa, sb) < greyCut * 2f) return true;

            // Hue is unreliable as saturation drops, so widen the window for washed-out colors.
            float minSat = Mathf.Min(sa, sb);
            float widen = Mathf.Clamp(0.5f / Mathf.Max(0.15f, minSat), 1f, 2.5f);
            float hueTolerance = Mathf.Min(60f, HueWindow(settings.strength) * widen);

            return Mathf.Abs(Mathf.DeltaAngle(ha * 360f, hb * 360f)) <= hueTolerance
                   && Mathf.Abs(sa - sb) <= SaturationWindow(settings.strength);
        }

        // Chebyshev distance over RGB: a tolerance of 8 means no channel differs by more than 8.
        static int Distance(Color32 a, Color32 b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        // Samples only the pixels inside each sprite's rect, so a partial tile selection
        // does not pull in every color on the shared sheet.
        static List<Color32> SpritePixels(IEnumerable<Sprite> sprites)
        {
            var byPath = new Dictionary<string, List<Sprite>>();
            foreach (var sprite in sprites)
            {
                if (sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(sprite);
                if (string.IsNullOrEmpty(path)) continue;
                if (!byPath.TryGetValue(path, out var list))
                    byPath[path] = list = new List<Sprite>();
                list.Add(sprite);
            }

            var all = new List<Color32>();
            foreach (var (path, list) in byPath)
            {
                var sheet = LoadFromPath(path);
                var pixels = sheet.GetPixels32();
                foreach (var sprite in list)
                {
                    var r = SpriteRect(sprite, sheet.width, sheet.height);
                    for (int y = r.yMin; y < r.yMax; y++)
                        for (int x = r.xMin; x < r.xMax; x++)
                            all.Add(pixels[y * sheet.width + x]);
                }
                Object.DestroyImmediate(sheet);
            }
            return all;
        }

        // textureRect is in imported-texture space; the raw PNG can differ if maxTextureSize downscaled it.
        public static RectInt SpriteRect(Sprite sprite, int sheetWidth, int sheetHeight)
        {
            var tr = sprite.textureRect;
            var imported = sprite.texture;
            float sx = imported != null && imported.width > 0 ? (float)sheetWidth / imported.width : 1f;
            float sy = imported != null && imported.height > 0 ? (float)sheetHeight / imported.height : 1f;

            int x = Mathf.Clamp(Mathf.RoundToInt(tr.x * sx), 0, sheetWidth);
            int y = Mathf.Clamp(Mathf.RoundToInt(tr.y * sy), 0, sheetHeight);
            int w = Mathf.Clamp(Mathf.RoundToInt(tr.width * sx), 0, sheetWidth - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(tr.height * sy), 0, sheetHeight - y);
            return new RectInt(x, y, w, h);
        }

        static List<Color32> FilterAndSort(IEnumerable<Color32> pixels)
        {
            var seen = new HashSet<uint>();
            var unique = new List<Color32>();
            foreach (var p in pixels)
            {
                if (p.a == 0) continue;
                if (seen.Add(Key(p))) unique.Add(p);
            }
            unique.Sort(CompareForDisplay);
            return unique;
        }

        static int CompareForDisplay(Color32 a, Color32 b)
        {
            Color.RGBToHSV(a, out float ha, out _, out float va);
            Color.RGBToHSV(b, out float hb, out _, out float vb);
            int c = ha.CompareTo(hb);
            return c != 0 ? c : va.CompareTo(vb);
        }

        public static Texture2D LoadFromPath(string assetPath)
        {
            string abs = Application.dataPath + assetPath.Substring("Assets".Length);
            byte[] bytes = File.ReadAllBytes(abs);
            var tex = new Texture2D(2, 2);
            ImageConversion.LoadImage(tex, bytes);
            return tex;
        }

        static uint Key(Color32 c) =>
            ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
    }
}
