using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PaletteSwap
{
    public class PaletteSwapWindow : EditorWindow
    {
        [MenuItem("Window/Palette Swap")]
        static void Open() => GetWindow<PaletteSwapWindow>("Palette Swap");

        List<GameObject> _palettes = new();
        List<ColorCount> _rawColors = new();
        List<ColorGroup> _groups = new();
        List<Sprite> _selectedSprites = new();
        Dictionary<int, Color32> _remapped = new();
        MergeSettings _merge = MergeSettings.Default;
        string _colorwayName = "";
        string _status = NoSelectionHint;
        string _source = "";
        bool _limitToGridSelection = true;
        Vector2 _scroll;

        List<Sprite> _previewSprites = new();
        List<Texture2D> _previewTextures = new();
        Dictionary<string, Texture2D> _sheetCache = new();
        bool _previewDirty = true;
        int _lastBrushHash;

        const int SwatchSize = 32;
        const int SwatchPad = 4;
        const int PreviewSize = 56;
        const int MaxPreviews = 8;
        const string NoSelectionHint =
            "Select a Tile Palette in the Project panel, or open one in the Tile Palette window.";

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            GridPaintingState.paletteChanged += OnPaletteChanged;
            GridSelection.gridSelectionChanged += OnSelectionChanged;
            // Deferred: OnEnable also runs during domain reload, before the AssetDatabase is ready to instantiate.
            EditorApplication.delayCall += OnSelectionChanged;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            GridPaintingState.paletteChanged -= OnPaletteChanged;
            GridSelection.gridSelectionChanged -= OnSelectionChanged;
            EditorApplication.delayCall -= OnSelectionChanged;
            ClearPreviewTextures();
            ClearSheetCache();
        }

        void OnSelectionChanged() { RefreshFromSelection(); Repaint(); }

        void OnPaletteChanged(GameObject palette) => OnSelectionChanged();

        // Picking a tile with the Paint tool fills the brush but raises no event, so poll for it.
        void OnInspectorUpdate()
        {
            int hash = BrushPickHash();
            if (hash == _lastBrushHash) return;
            _lastBrushHash = hash;
            OnSelectionChanged();
        }

        static int BrushPickHash()
        {
            if (GridPaintingState.gridBrush is not GridBrush brush || brush.cells == null) return 0;
            int hash = 17;
            foreach (var cell in brush.cells)
                hash = hash * 31 + (cell?.tile != null ? cell.tile.GetInstanceID() : 0);
            return hash;
        }

        static HashSet<Tile> BrushTiles()
        {
            var picked = new HashSet<Tile>();
            if (GridPaintingState.gridBrush is not GridBrush brush || brush.cells == null) return picked;
            foreach (var cell in brush.cells)
                if (cell?.tile is Tile tile) picked.Add(tile);
            return picked;
        }

        void RefreshFromSelection()
        {
            _palettes = ResolvePalettes(out string rejection, out _source);

            ClearSheetCache();
            _previewDirty = true;

            if (_palettes.Count == 0)
            {
                _rawColors.Clear();
                _groups.Clear();
                _remapped.Clear();
                _previewSprites.Clear();
                _selectedSprites.Clear();
                _status = rejection ?? NoSelectionHint;
                return;
            }

            var brushTiles = BrushTiles();
            bool byGridSelection = _limitToGridSelection && GridSelection.active;
            bool byBrushPick = _limitToGridSelection && !byGridSelection && brushTiles.Count > 0;
            bool narrowed = byGridSelection || byBrushPick;

            var sprites = new List<Sprite>();
            foreach (var p in _palettes)
            {
                var tiles = TilePaletteGenerator.ReadTiles(p);
                if (byGridSelection) tiles = FilterToGridSelection(tiles);
                else if (byBrushPick) tiles = FilterToTiles(tiles, brushTiles);
                foreach (var tile in tiles.Values.Distinct())
                    if (tile.sprite != null) sprites.Add(tile.sprite);
            }
            sprites = sprites.Distinct().ToList();

            _selectedSprites = sprites;
            _rawColors = ColorExtractor.CountFromSprites(sprites);
            _previewSprites = sprites.Take(MaxPreviews).ToList();
            Regroup();

            string how = byGridSelection ? "Tile Palette selection"
                : byBrushPick ? "brush pick"
                : _source;
            string scope = narrowed ? $"{sprites.Count} selected tile(s)" : $"{_palettes.Count} palette(s)";
            _status = _groups.Count == 0
                ? narrowed
                    ? "Nothing matched your Tile Palette selection. Pick again, or untick the box above."
                    : "Palette found, but no colors could be read from its sprite sheets."
                : $"{_groups.Count} swatch(es) across {scope} via {how}{MergeNote()}.";
        }

        void Regroup()
        {
            _groups = ColorExtractor.Group(_rawColors, _merge);
            _remapped.Clear();
            _previewDirty = true;
        }

        string MergeNote()
        {
            int merged = _rawColors.Count - _groups.Count;
            if (merged <= 0) return "";
            int deepest = _groups.Count == 0 ? 0 : _groups.Max(g => g.ShadeCount);
            return _merge.mode == MergeMode.Shades
                ? $" — {merged} shade(s) folded in, deepest ramp {deepest}"
                : $" — {merged} near color(s) merged at tolerance {_merge.tolerance}";
        }

        List<GameObject> ResolvePalettes(out string rejection, out string source)
        {
            rejection = null;

            var fromProject = Selection.objects
                .Select(AsPalettePrefab)
                .Where(go => go != null)
                .Distinct()
                .ToList();

            if (fromProject.Count > 0)
            {
                source = "Project selection";
                return fromProject;
            }

            var active = AsPalettePrefab(GridPaintingState.palette);
            if (active != null)
            {
                source = "Tile Palette window";
                return new List<GameObject> { active };
            }

            source = "";
            var first = Selection.objects.FirstOrDefault();
            if (first != null)
                rejection = $"'{first.name}' is a {first.GetType().Name}, not a Tile Palette. " +
                            "Select the palette asset itself, or open it in the Tile Palette window.";
            return new List<GameObject>();
        }

        // Palettes are prefab assets; a dragged-in instance or a preview-scene object resolves back to its source.
        static GameObject AsPalettePrefab(Object obj)
        {
            if (obj is not GameObject go) return null;

            if (!AssetDatabase.Contains(go))
            {
                go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (go == null) return null;
            }

            return go.GetComponentInChildren<Tilemap>(true) != null ? go : null;
        }

        static Dictionary<Vector3Int, Tile> FilterToGridSelection(Dictionary<Vector3Int, Tile> tiles)
        {
            var bounds = GridSelection.position;
            var result = new Dictionary<Vector3Int, Tile>();
            foreach (var (pos, tile) in tiles)
                if (bounds.Contains(pos)) result[pos] = tile;
            return result;
        }

        static Dictionary<Vector3Int, Tile> FilterToTiles(
            Dictionary<Vector3Int, Tile> tiles, HashSet<Tile> keep)
        {
            var result = new Dictionary<Vector3Int, Tile>();
            foreach (var (pos, tile) in tiles)
                if (keep.Contains(tile)) result[pos] = tile;
            return result;
        }

        void OnGUI()
        {
            // Rebuilding only on Layout keeps the control count identical between Layout and Repaint.
            if (_previewDirty && Event.current.type == EventType.Layout) RebuildPreviews();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSelectionBar();
            EditorGUILayout.Space(6);
            DrawSwatchGrid();
            EditorGUILayout.Space(6);
            DrawPreview();
            EditorGUILayout.Space(6);
            DrawStatusLine();
            EditorGUILayout.Space(6);
            DrawNameAndButtons();
            EditorGUILayout.EndScrollView();
        }

        void DrawSelectionBar()
        {
            EditorGUILayout.LabelField("Selected palettes:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                _palettes.Count == 0 ? "None" : string.Join(", ", _palettes.Select(p => p.name)),
                EditorStyles.wordWrappedMiniLabel);

            EditorGUI.BeginChangeCheck();
            _limitToGridSelection = EditorGUILayout.ToggleLeft(
                "Limit colors to tiles selected in the Tile Palette", _limitToGridSelection);
            if (EditorGUI.EndChangeCheck()) RefreshFromSelection();

            EditorGUI.BeginChangeCheck();

            _merge.mode = (MergeMode)EditorGUILayout.EnumPopup(
                new GUIContent("Merge", "Shades: one swatch per color ramp. Exact: every distinct color. Custom: RGB distance."),
                _merge.mode);

            if (_merge.mode == MergeMode.Custom)
            {
                _merge.tolerance = EditorGUILayout.IntSlider("  RGB tolerance", _merge.tolerance, 1, 64);
            }
            else if (_merge.mode == MergeMode.Shades)
            {
                _merge.strength = EditorGUILayout.Slider(
                    new GUIContent("  Strength", "Higher folds wider hue and saturation differences into one ramp."),
                    _merge.strength, 0f, 1f);
                _merge.keepShadowsSeparate = EditorGUILayout.ToggleLeft(
                    "  Keep near-black outlines separate", _merge.keepShadowsSeparate);
            }

            if (EditorGUI.EndChangeCheck()) Regroup();

            if (_merge.mode == MergeMode.Shades)
                EditorGUILayout.LabelField(
                    "Shading is preserved: recolor the swatch and its darker/lighter shades follow.",
                    EditorStyles.miniLabel);
        }

        void DrawPreview()
        {
            if (_previewTextures.Count == 0) return;

            EditorGUILayout.LabelField(
                _remapped.Count == 0 ? "Preview (no colors remapped yet):" : "Preview:",
                EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _previewTextures.Count; i++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(PreviewSize));
                DrawSourceSprite(_previewSprites[i]);
                DrawTextureBox(_previewTextures[i]);
                EditorGUILayout.EndVertical();
                GUILayout.Space(SwatchPad);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Top row: original. Bottom row: baked result.",
                EditorStyles.miniLabel);
        }

        static void DrawSourceSprite(Sprite sprite)
        {
            var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize,
                GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
            if (sprite == null || sprite.texture == null) return;

            var tr = sprite.textureRect;
            var tex = sprite.texture;
            var coords = new Rect(tr.x / tex.width, tr.y / tex.height,
                tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, coords);
        }

        static void DrawTextureBox(Texture2D tex)
        {
            var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize,
                GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
            if (tex != null) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
        }

        void RebuildPreviews()
        {
            ClearPreviewTextures();
            var mappings = CurrentMappings();

            var kept = new List<Sprite>();
            foreach (var sprite in _previewSprites)
            {
                var sheet = GetSheet(AssetDatabase.GetAssetPath(sprite));
                var tex = PreviewRenderer.Render(sheet, sprite, mappings);
                if (tex == null) continue;
                kept.Add(sprite);
                _previewTextures.Add(tex);
            }
            _previewSprites = kept;
            _previewDirty = false;
        }

        List<ColorMapping> CurrentMappings()
        {
            var mappings = new List<ColorMapping>();
            foreach (var (idx, to) in _remapped)
                mappings.AddRange(_groups[idx].MappingsTo(to));
            return mappings;
        }

        Texture2D GetSheet(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (_sheetCache.TryGetValue(assetPath, out var cached)) return cached;

            var sheet = ColorExtractor.LoadFromPath(assetPath);
            sheet.hideFlags = HideFlags.HideAndDontSave;
            _sheetCache[assetPath] = sheet;
            return sheet;
        }

        void ClearPreviewTextures()
        {
            foreach (var tex in _previewTextures)
                if (tex != null) Object.DestroyImmediate(tex);
            _previewTextures.Clear();
        }

        void ClearSheetCache()
        {
            foreach (var tex in _sheetCache.Values)
                if (tex != null) Object.DestroyImmediate(tex);
            _sheetCache.Clear();
        }

        void DrawSwatchGrid()
        {
            if (_groups.Count == 0) return;
            EditorGUILayout.LabelField("Colors found:", EditorStyles.boldLabel);

            int perRow = Mathf.Max(1, (int)(position.width - 20) / (SwatchSize + SwatchPad));

            for (int i = 0; i < _groups.Count; i++)
            {
                if (i % perRow == 0) EditorGUILayout.BeginHorizontal();

                bool isRemapped = _remapped.ContainsKey(i);
                Color current = isRemapped ? (Color)_remapped[i] : (Color)_groups[i].representative;

                if (isRemapped)
                {
                    var borderRect = GUILayoutUtility.GetRect(SwatchSize + 4, SwatchSize + 4,
                        GUILayout.Width(SwatchSize + 4), GUILayout.Height(SwatchSize + 4));
                    EditorGUI.DrawRect(borderRect, Color.white);
                    var inner = new Rect(borderRect.x + 2, borderRect.y + 2, borderRect.width - 4, borderRect.height - 4);
                    Color picked = EditorGUI.ColorField(inner, GUIContent.none, current, false, false, false);
                    if (picked != current)
                    {
                        _remapped[i] = picked;
                        _previewDirty = true;
                    }
                }
                else
                {
                    Color picked = EditorGUILayout.ColorField(GUIContent.none, current, false, false, false,
                        GUILayout.Width(SwatchSize), GUILayout.Height(SwatchSize));
                    if (picked != current)
                    {
                        _remapped[i] = picked;
                        _previewDirty = true;
                        _status = $"{_remapped.Count} color(s) remapped.";
                    }
                }

                if ((i + 1) % perRow == 0 || i == _groups.Count - 1)
                    EditorGUILayout.EndHorizontal();
            }
        }

        void DrawStatusLine() =>
            EditorGUILayout.LabelField(_status, EditorStyles.wordWrappedMiniLabel);

        void DrawNameAndButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Colorway name:", GUILayout.Width(110));
            _colorwayName = EditorGUILayout.TextField(_colorwayName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load")) LoadColorway();
            bool canBake = _palettes.Count > 0 && !string.IsNullOrWhiteSpace(_colorwayName) && _remapped.Count > 0;
            EditorGUI.BeginDisabledGroup(!canBake);
            if (GUILayout.Button("Bake")) Bake();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        void LoadColorway()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Load Colorway", "Assets", new[] { "Colorway Asset", "asset" });
            if (string.IsNullOrEmpty(path)) return;

            string assetPath = "Assets" + path.Substring(Application.dataPath.Length);
            var asset = AssetDatabase.LoadAssetAtPath<ColorwayAsset>(assetPath);
            if (asset == null) { _status = "Could not load — is it a ColorwayAsset?"; return; }

            int matched = 0;
            for (int i = 0; i < _groups.Count; i++)
                if (FirstMatch(_groups[i], asset, out var to)) { _remapped[i] = to; matched++; }

            _colorwayName = asset.name;
            _previewDirty = true;
            int total = asset.mappings.Count;
            _status = matched == 0
                ? $"0 of {total} colors matched — nothing pre-filled."
                : matched < total
                    ? $"{matched} of {total} colors matched. Fill the rest manually."
                    : $"All {total} colors matched.";
            Repaint();
        }

        static bool FirstMatch(ColorGroup group, ColorwayAsset asset, out Color32 to)
        {
            foreach (var member in group.members)
                foreach (var m in asset.mappings)
                    if (ColorwayAsset.Eq(m.from, member)) { to = m.to; return true; }
            to = default;
            return false;
        }

        void Bake()
        {
            var colorway = ScriptableObject.CreateInstance<ColorwayAsset>();
            colorway.mappings.AddRange(CurrentMappings());

            string savedPath = SaveColorwayAsset(colorway);
            var savedColorway = AssetDatabase.LoadAssetAtPath<ColorwayAsset>(savedPath);

            foreach (var palette in _palettes)
            {
                try { BakePalette(palette, savedColorway); }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Bake Failed", e.Message, "OK");
                    return;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ReloadPaletteWindow();
            _status = $"Baked '{_colorwayName}' — variants added to the palette, below the originals.";
        }

        string SaveColorwayAsset(ColorwayAsset colorway)
        {
            string dir = "Assets/PaletteSwapColorways";
            Directory.CreateDirectory(Application.dataPath + dir.Substring("Assets".Length));
            string path = $"{dir}/{_colorwayName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<ColorwayAsset>(path);
            if (existing != null)
            {
                existing.mappings = colorway.mappings;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(colorway);
            }
            else
            {
                colorway.name = _colorwayName;
                AssetDatabase.CreateAsset(colorway, path);
            }
            AssetDatabase.SaveAssets();
            return path;
        }

        void BakePalette(GameObject palette, ColorwayAsset colorway)
        {
            var sourceTiles = TilePaletteGenerator.ReadTiles(palette);
            var inScope = new HashSet<Sprite>(_selectedSprites);

            // Only the sprites in scope get recolored, and only their tiles get variant assets.
            var newSpritesByName = new Dictionary<string, Sprite>();
            foreach (var group in _selectedSprites.GroupBy(AssetDatabase.GetAssetPath))
            {
                if (string.IsNullOrEmpty(group.Key)) continue;
                foreach (var sprite in PaletteBaker.Bake(group.Key, group.ToList(), colorway, _colorwayName))
                    newSpritesByName[sprite.name] = sprite;
            }

            string paletteDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(palette)).Replace('\\', '/');
            string tileDir = $"{paletteDir}/Variants/{_colorwayName}";
            Directory.CreateDirectory(Application.dataPath + tileDir.Substring("Assets".Length));

            var tileMapping = new Dictionary<Tile, Tile>();
            foreach (var oldTile in sourceTiles.Values.Distinct())
            {
                if (oldTile.sprite == null || !inScope.Contains(oldTile.sprite)) continue;
                if (!newSpritesByName.TryGetValue(oldTile.sprite.name, out var newSprite)) continue;

                string tilePath = $"{tileDir}/{oldTile.name}_{_colorwayName}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                Tile newTile;
                if (existing != null)
                {
                    existing.sprite = newSprite;
                    EditorUtility.SetDirty(existing);
                    newTile = existing;
                }
                else
                {
                    newTile = ScriptableObject.CreateInstance<Tile>();
                    newTile.sprite = newSprite;
                    AssetDatabase.CreateAsset(newTile, tilePath);
                }
                tileMapping[oldTile] = newTile;
            }

            TilePaletteGenerator.AppendVariants(palette, tileMapping, _colorwayName, colorway);
        }

        // The Tile Palette window caches its palette; reassigning forces it to re-read the prefab.
        static void ReloadPaletteWindow()
        {
            var current = GridPaintingState.palette;
            if (current == null) return;
            GridPaintingState.palette = null;
            GridPaintingState.palette = current;
        }
    }
}
