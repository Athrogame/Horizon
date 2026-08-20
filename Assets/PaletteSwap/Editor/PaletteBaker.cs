using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PaletteSwap
{
    public static class PaletteBaker
    {
        public static Sprite[] Bake(string sourceAssetPath, ColorwayAsset colorway, string variantName) =>
            Bake(sourceAssetPath, null, colorway, variantName);

        // With spritesToRecolor set, only those rects are remapped. Everything else on the sheet
        // is carried over from the previous bake if there is one, so partial bakes accumulate
        // instead of wiping each other out. Mapped pixels always come from the pristine source,
        // so re-baking a rect with a different color still matches the original 'from' values.
        public static Sprite[] Bake(
            string sourceAssetPath,
            IReadOnlyList<Sprite> spritesToRecolor,
            ColorwayAsset colorway,
            string variantName)
        {
            var source = ColorExtractor.LoadFromPath(sourceAssetPath);
            var sourcePixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;

            string srcDir = Path.GetDirectoryName(sourceAssetPath).Replace('\\', '/');
            string fileName = Path.GetFileName(sourceAssetPath);
            string variantDir = $"{srcDir}/Variants/{variantName}";
            string variantAssetPath = $"{variantDir}/{fileName}";

            Color32[] outPixels;
            if (spritesToRecolor == null || spritesToRecolor.Count == 0)
            {
                outPixels = SwapColors(sourcePixels, colorway.mappings);
            }
            else
            {
                outPixels = CarryOverPixels(variantAssetPath, sourcePixels, width, height);
                var lookup = BuildLookup(colorway.mappings);
                foreach (var sprite in spritesToRecolor)
                {
                    var r = ColorExtractor.SpriteRect(sprite, width, height);
                    for (int y = r.yMin; y < r.yMax; y++)
                        for (int x = r.xMin; x < r.xMax; x++)
                        {
                            int i = y * width + x;
                            var src = sourcePixels[i];
                            outPixels[i] = lookup.TryGetValue(Key(src), out var rep) ? rep : src;
                        }
                }
            }

            source.SetPixels32(outPixels);
            source.Apply();
            byte[] outBytes = source.EncodeToPNG();
            Object.DestroyImmediate(source);

            Directory.CreateDirectory(AbsPath(variantDir));
            File.WriteAllBytes(AbsPath(variantAssetPath), outBytes);

            AssetDatabase.ImportAsset(variantAssetPath, ImportAssetOptions.ForceUpdate);

            var srcImporter = AssetImporter.GetAtPath(sourceAssetPath) as TextureImporter;
            var dstImporter = AssetImporter.GetAtPath(variantAssetPath) as TextureImporter;
            if (srcImporter != null && dstImporter != null)
            {
                var settings = new TextureImporterSettings();
                srcImporter.ReadTextureSettings(settings);
                dstImporter.SetTextureSettings(settings);
                dstImporter.spritesheet = srcImporter.spritesheet;
                dstImporter.spritePivot = srcImporter.spritePivot;
                dstImporter.spritePixelsPerUnit = srcImporter.spritePixelsPerUnit;
                dstImporter.SaveAndReimport();
            }

            return AssetDatabase.LoadAllAssetsAtPath(variantAssetPath)
                .OfType<Sprite>()
                .ToArray();
        }

        static Color32[] CarryOverPixels(string variantAssetPath, Color32[] sourcePixels, int width, int height)
        {
            if (!File.Exists(AbsPath(variantAssetPath))) return (Color32[])sourcePixels.Clone();

            var previous = ColorExtractor.LoadFromPath(variantAssetPath);
            var carried = previous.width == width && previous.height == height
                ? previous.GetPixels32()
                : (Color32[])sourcePixels.Clone();
            Object.DestroyImmediate(previous);
            return carried;
        }

        public static Color32[] SwapColors(Color32[] pixels, IReadOnlyList<ColorMapping> mappings)
        {
            var lookup = BuildLookup(mappings);
            var result = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                result[i] = lookup.TryGetValue(Key(pixels[i]), out var rep) ? rep : pixels[i];
            return result;
        }

        static Dictionary<uint, Color32> BuildLookup(IReadOnlyList<ColorMapping> mappings)
        {
            var lookup = new Dictionary<uint, Color32>(mappings.Count);
            foreach (var m in mappings)
                lookup[Key(m.from)] = m.to;
            return lookup;
        }

        static string AbsPath(string assetPath) =>
            Application.dataPath + assetPath.Substring("Assets".Length);

        static uint Key(Color32 c) =>
            ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
    }
}
