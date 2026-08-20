using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PaletteSwap.Tests
{
    public class PaletteBakerTests
    {
        [Test]
        public void SwapColors_ReplacesMatchingPixel()
        {
            var mappings = new List<ColorMapping>
            {
                new() { from = new Color32(255, 0, 0, 255), to = new Color32(0, 255, 0, 255) }
            };
            var pixels = new Color32[] { new(255, 0, 0, 255), new(0, 0, 255, 255) };

            var result = PaletteBaker.SwapColors(pixels, mappings);

            Assert.AreEqual(new Color32(0, 255, 0, 255), result[0]);
            Assert.AreEqual(new Color32(0, 0, 255, 255), result[1]);
        }

        [Test]
        public void SwapColors_LeavesNonMatchingPixelUnchanged()
        {
            var mappings = new List<ColorMapping>
            {
                new() { from = new Color32(255, 0, 0, 255), to = new Color32(0, 255, 0, 255) }
            };
            var pixels = new Color32[] { new(128, 128, 128, 255) };

            var result = PaletteBaker.SwapColors(pixels, mappings);

            Assert.AreEqual(new Color32(128, 128, 128, 255), result[0]);
        }

        [Test]
        public void SwapColors_EmptyMappings_ReturnsUnchanged()
        {
            var pixels = new Color32[] { new(255, 0, 0, 255) };
            var result = PaletteBaker.SwapColors(pixels, new List<ColorMapping>());
            Assert.AreEqual(new Color32(255, 0, 0, 255), result[0]);
        }
    }
}
