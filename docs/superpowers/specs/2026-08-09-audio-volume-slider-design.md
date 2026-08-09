# Audio volume slider — design

## Purpose

The Settings menu's Audio tab (Master/Music/Sfx rows) currently adjusts volume in
whole steps of 1 across a 0–10 range, via Left/Right. This changes it to a
0–100 percent range, adjusted by Left/Right in steps of 5 (or 1 while
holding Shift / the gamepad equivalent), with the value shown as a real
segmented bar instead of a text-character bar.

## Scope

Three existing files change (`GameSettings.cs`, `SettingsMenu.cs`,
`SettingsPanel.cs`), one new script is added (`SegmentedBar.cs`), and one new
prefab is added (`VolumeBar.prefab`). Video and Exit tabs are untouched.
Master/Music/Sfx row wiring in the Audio tab's page (dragging the new prefab
into each row) is a manual Editor step, not code.

## `GameSettings.cs` — range change

- `MaxVolumeStep` (currently `10`) is renamed `MaxVolume` and becomes `100`.
- `ReadVolume` / `WriteVolume` clamp to `0–100` instead of `0–10`.
- `MasterVolume`, `MusicVolume`, `SfxVolume`, `MusicScalar`, `SfxScalar` keep
  their existing shape; only the range they operate over changes
  (`AudioListener.volume = MasterVolume / 100f`, etc).
- **No save migration.** Old 0–10 saves under `settings_volMaster` /
  `settings_volMusic` / `settings_volSfx` are simply reinterpreted as a low
  percentage under the new scale until the player nudges them. Acceptable
  because the project is pre-release.

## `SettingsMenu.cs` — step size + modifier

- `Horizontal()`'s value-row branch changes from `optionIndex + dir` to
  `optionIndex + dir * step`, where `step` is `5` normally, or `1` while the
  `Player/Sprint` action is held.
- `Player/Sprint` (already bound to `Keyboard/leftShift` and
  `Gamepad/leftStickPress` in `InputSystem_Actions.inputactions`) is resolved
  and enabled in `Start()` the same way `Cancel` already force-resolves to
  `Player/Cancel`, and read with `.IsPressed()` inside `Horizontal()`.
- No `.inputactions` changes. No change to which axis does what — Up/Down
  still moves between rows, Left/Right still adjusts the focused row's value.
- Clamping at 0/100 is unchanged in spirit (e.g. from 98, `+5` clamps to 100,
  not 103).

## `SegmentedBar.cs` — new script

A `UnityEngine.UI.Graphic` subclass (not `Image`), so the prefab that uses it
only needs to reference this script's own GUID plus rock-solid built-in
engine types (`GameObject`, `RectTransform`, `CanvasRenderer`) — no
dependency on getting `UnityEngine.UI.Image`'s or `HorizontalLayoutGroup`'s
script GUIDs right from memory in a hand-written prefab.

- `[ExecuteAlways]`, draws its own mesh in `OnPopulateMesh`: `segmentCount`
  (`20`, i.e. 5% per segment) rectangles left-to-right with a small gap
  (`segmentSpacing`), each tinted `filledColor` or `emptyColor`.
- `public void SetValue(int value, int max)` — recomputes how many segments
  are filled (`Mathf.RoundToInt(value / (float)max * segmentCount)`) and
  calls `SetVerticesDirty()`.
- Public fields: `segmentCount`, `segmentSpacing`, `filledColor`,
  `emptyColor` (defaults: 20, a few UI units, white, the same grey
  `SettingsPanel` already uses for `unchosenColor`).

## `VolumeBar.prefab` — new prefab

- `Assets/Prefabs/VolumeBar.prefab`, hand-written YAML (matching the flat
  layout of the existing `Assets/Prefabs` folder).
- One GameObject: `RectTransform` (default size ~200×20) + `CanvasRenderer` +
  `SegmentedBar`.
- **Verification step (manual, since this file is hand-authored rather than
  built through the Editor):** open the project in Unity right after this
  lands and confirm the prefab imports with no console errors and renders as
  a bar in the Scene view, before dragging instances into the Audio rows. If
  Unity flags anything, delete the file and recreate the GameObject manually
  in the Editor using `SegmentedBar` — a couple of minutes of work — rather
  than debugging the hand-written YAML.

## `SettingsPanel.cs` — row wiring

- `Row` gets a new field: `public SegmentedBar bar;` — the dragged-in
  `VolumeBar.prefab` instance for that row, set in the Inspector once the
  prefab exists. Only used by value rows.
- `valueLabel` keeps showing just the percentage text (e.g. `"55%"`) instead
  of the old character bar.
- `RefreshRowColors`, on a value row, additionally calls
  `rows[row].bar?.SetValue(chosenIndices[row], rows[row].maxValue)`.
- Arrow placement (`PlaceArrowLeftOf`) and the paper slide animation are
  unchanged — the arrow keeps pointing at `valueLabel`.

## Out of scope

- No change to Video or Exit tabs.
- No change to `.inputactions` (Shift/gamepad-stick-click reuses the
  existing `Sprint` action).
- No migration of old 0–10 saved volume values.
- Wiring the three Audio rows' `bar` fields to `VolumeBar.prefab` instances
  in the Inspector is a manual step the player of this doc (the dev) does in
  the Editor, not something delivered as code.
