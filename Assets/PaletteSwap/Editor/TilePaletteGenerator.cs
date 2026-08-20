using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PaletteSwap
{
    public static class TilePaletteGenerator
    {
        public static Dictionary<Vector3Int, Tile> ReadTiles(GameObject palettePrefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(palettePrefab);
            var tilemap = instance.GetComponentInChildren<Tilemap>();
            var result = new Dictionary<Vector3Int, Tile>();

            if (tilemap != null)
            {
                foreach (var pos in tilemap.cellBounds.allPositionsWithin)
                {
                    if (tilemap.GetTile(pos) is Tile tile)
                        result[pos] = tile;
                }
            }

            Object.DestroyImmediate(instance);
            return result;
        }

        public static HashSet<string> GetSheetPaths(Dictionary<Vector3Int, Tile> tiles)
        {
            var paths = new HashSet<string>();
            foreach (var tile in tiles.Values)
                if (tile.sprite != null)
                    paths.Add(AssetDatabase.GetAssetPath(tile.sprite));
            return paths;
        }

        // Adds the recolored tiles onto the source palette itself, in a block below the existing
        // content. Re-baking the same colorway clears its old block first, so it never stacks up.
        public static void AppendVariants(
            GameObject sourcePalette,
            Dictionary<Tile, Tile> tileMapping,
            string variantName,
            ColorwayAsset colorway)
        {
            string palettePath = AssetDatabase.GetAssetPath(sourcePalette);
            var contents = PrefabUtility.LoadPrefabContents(palettePath);

            try
            {
                var tilemap = contents.GetComponentInChildren<Tilemap>(true);
                if (tilemap == null) return;

                ClearVariantBlock(tilemap, variantName);
                tilemap.CompressBounds();

                var placements = new List<(Vector3Int pos, Tile tile)>();
                foreach (var pos in tilemap.cellBounds.allPositionsWithin)
                    if (tilemap.GetTile(pos) is Tile tile && tileMapping.ContainsKey(tile))
                        placements.Add((pos, tile));

                if (placements.Count == 0) return;

                int sourceTop = placements.Max(p => p.pos.y);
                int dy = tilemap.cellBounds.yMin - 2 - sourceTop;

                foreach (var (pos, oldTile) in placements)
                    if (tileMapping.TryGetValue(oldTile, out var newTile))
                        tilemap.SetTile(new Vector3Int(pos.x, pos.y + dy, pos.z), newTile);

                tilemap.CompressBounds();
                PrefabUtility.SaveAsPrefabAsset(contents, palettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            WriteVariantLink(sourcePalette, variantName, colorway);
            AssetDatabase.SaveAssets();
        }

        static void ClearVariantBlock(Tilemap tilemap, string variantName)
        {
            string marker = $"/Variants/{variantName}/";
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(pos);
                if (tile == null) continue;
                if (AssetDatabase.GetAssetPath(tile).Replace('\\', '/').Contains(marker))
                    tilemap.SetTile(pos, null);
            }
        }

        static void WriteVariantLink(GameObject sourcePalette, string variantName, ColorwayAsset colorway)
        {
            string sourceDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sourcePalette)).Replace('\\', '/');
            string sourceName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourcePalette));
            string variantDir = $"{sourceDir}/Variants/{variantName}";
            Directory.CreateDirectory(Application.dataPath + variantDir.Substring("Assets".Length));

            string linkPath = $"{variantDir}/{sourceName}_{variantName}_link.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VariantLink>(linkPath);
            if (existing != null)
            {
                existing.sourcePalette = sourcePalette;
                existing.colorway = colorway;
                EditorUtility.SetDirty(existing);
                return;
            }

            var link = ScriptableObject.CreateInstance<VariantLink>();
            link.sourcePalette = sourcePalette;
            link.colorway = colorway;
            AssetDatabase.CreateAsset(link, linkPath);
        }

        public static GameObject Generate(
            GameObject sourcePalette,
            Dictionary<Tile, Tile> tileMapping,
            string variantName,
            ColorwayAsset colorway)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourcePalette);
            string sourceDir = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string variantDir = $"{sourceDir}/Variants/{variantName}";
            string variantPrefabPath = $"{variantDir}/{sourceName}_{variantName}.prefab";

            Directory.CreateDirectory(Application.dataPath + variantDir.Substring("Assets".Length));

            var sourceGrid = sourcePalette.GetComponent<Grid>();
            var newGO = new GameObject($"{sourceName}_{variantName}");
            var grid = newGO.AddComponent<Grid>();
            if (sourceGrid != null)
            {
                grid.cellSize = sourceGrid.cellSize;
                grid.cellGap = sourceGrid.cellGap;
                grid.cellLayout = sourceGrid.cellLayout;
                grid.cellSwizzle = sourceGrid.cellSwizzle;
            }

            var layerGO = new GameObject("Layer1");
            layerGO.transform.SetParent(newGO.transform, false);
            var newTilemap = layerGO.AddComponent<Tilemap>();
            layerGO.AddComponent<TilemapRenderer>();

            var sourceTilemap = sourcePalette.GetComponentInChildren<Tilemap>();
            if (sourceTilemap != null)
            {
                newTilemap.tileAnchor = sourceTilemap.tileAnchor;
                newTilemap.orientation = sourceTilemap.orientation;
            }

            var sourceTiles = ReadTiles(sourcePalette);
            foreach (var (pos, oldTile) in sourceTiles)
                if (tileMapping.TryGetValue(oldTile, out var newTile))
                    newTilemap.SetTile(pos, newTile);

            var prefab = PrefabUtility.SaveAsPrefabAsset(newGO, variantPrefabPath);
            Object.DestroyImmediate(newGO);

            var link = ScriptableObject.CreateInstance<VariantLink>();
            link.sourcePalette = sourcePalette;
            link.colorway = colorway;
            AssetDatabase.CreateAsset(link, $"{variantDir}/{sourceName}_{variantName}_link.asset");
            AssetDatabase.SaveAssets();

            return prefab;
        }
    }
}
