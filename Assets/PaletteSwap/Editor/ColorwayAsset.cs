using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaletteSwap
{
    [Serializable]
    public struct ColorMapping
    {
        public Color32 from;
        public Color32 to;
    }

    [CreateAssetMenu(menuName = "Palette Swap/Colorway", fileName = "NewColorway")]
    public class ColorwayAsset : ScriptableObject
    {
        public List<ColorMapping> mappings = new();

        public int CountMatches(IReadOnlyList<Color32> colors)
        {
            int count = 0;
            foreach (var color in colors)
                foreach (var m in mappings)
                    if (Eq(m.from, color)) { count++; break; }
            return count;
        }

        internal static bool Eq(Color32 a, Color32 b) =>
            a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
