using NUnit.Framework;
using UnityEngine;

namespace PaletteSwap.Tests
{
    public class ColorExtractorTests
    {
        [Test]
        public void Extract_DeduplicatesColors()
        {
            var tex = MakeTex(new Color32[]
            {
                new(255, 0, 0, 255),
                new(255, 0, 0, 255),
                new(0, 255, 0, 255),
            });
            var result = ColorExtractor.Extract(tex);
            Object.DestroyImmediate(tex);
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Extract_ExcludesTransparentPixels()
        {
            var tex = MakeTex(new Color32[] { new(255, 0, 0, 0) });
            var result = ColorExtractor.Extract(tex);
            Object.DestroyImmediate(tex);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Extract_SortsByHueThenValue()
        {
            // Red hue ~0, blue hue ~0.66 — red should come first
            var tex = MakeTex(new Color32[]
            {
                new(0, 0, 255, 255),
                new(255, 0, 0, 255),
            });
            var result = ColorExtractor.Extract(tex);
            Object.DestroyImmediate(tex);
            Assert.AreEqual(new Color32(255, 0, 0, 255), result[0]);
            Assert.AreEqual(new Color32(0, 0, 255, 255), result[1]);
        }

        [Test]
        public void ExtractUnion_DeduplicatesAcrossTextures()
        {
            var t1 = MakeTex(new Color32[] { new(255, 0, 0, 255) });
            var t2 = MakeTex(new Color32[] { new(255, 0, 0, 255) });
            var result = ColorExtractor.ExtractUnion(new[] { t1, t2 });
            Object.DestroyImmediate(t1);
            Object.DestroyImmediate(t2);
            Assert.AreEqual(1, result.Count);
        }

        static Texture2D MakeTex(Color32[] pixels)
        {
            var tex = new Texture2D(pixels.Length, 1);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
