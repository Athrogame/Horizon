using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Keyboard-driven Settings screen.
///
/// Focus moves down three levels and back out with Cancel:
///   Tabs     — Left/Right cycles Video | Audio | Exit; the active one turns scarlet and its page
///              paper-slides in. Interact (or Down) drops into the page. Cancel closes settings.
///   Sections — Up/Down moves the section arrow between the page's rows. Interact picks a row.
///              Cancel goes back up to the tabs.
///   Options  — Left/Right moves that row's option arrow (or nudges a volume). The section arrow
///              stays put on the row. Interact applies and returns to Sections; Cancel returns
///              without changing anything.
///
/// All input is ignored while any page is animating, so the menu never moves under the player.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public enum TabKind { Video, Audio, Exit }

    [Serializable]
    public class Tab
    {
        [Tooltip("Which settings this tab holds — decides how its rows are interpreted.")]
        public TabKind kind;
        [Tooltip("The tab's text in the top bar. Tinted scarlet while the tab is active.")]
        public TextMeshProUGUI label;
        [Tooltip("The page that slides in for this tab. Leave empty for a tab with no page yet.")]
        public SettingsPanel panel;
    }

    [Header("Tabs (left to right, in the order they appear in the bar)")]
    public List<Tab> tabs = new List<Tab>();

    [Header("Tab colours")]
    [Tooltip("Colour of the tab the player is currently on.")]
    public Color tabSelectedColor = new Color(0.784f, 0.063f, 0.180f, 1f);   // scarlet
    [Tooltip("Colour of the other tabs.")]
    public Color tabUnselectedColor = Color.white;

    [Header("Whole-menu paper animation")]
    [Tooltip("The panel that slides in when Settings opens — normally the child 'Panel' holding the " +
             "title, tab bar and pages. It needs its own SettingsPanel component (with no rows).")]
    public SettingsPanel rootPaper;

    /// <summary>True from the moment Settings starts opening until it has fully slid away.</summary>
    public bool IsOpen { get; private set; }

    private enum Focus { Tabs, Sections, Options }
    private Focus focus = Focus.Tabs;

    private int tabIndex;
    private int rowIndex;
    private int optionIndex;   // where the option arrow is while browsing (not yet applied)

    // True while a page is sliding — swallows every input so nothing moves mid-animation.
    private bool busy;
    // The Interact press that opened the menu is still down on the first frame; don't act on it.
    private bool ignoreInteractThisFrame;

    [Header("Exit tab")]
    [Tooltip("Row index of \"Return to Desktop\" (quit) on the Exit page.")]
    public int exitQuitRow = 0;
    [Tooltip("Row index of \"Exit to Title Screen\" on the Exit page.")]
    public int exitTitleRow = 1;
    [Tooltip("Untick if this Settings instance can be reached from inside a level (e.g. a future " +
             "pause menu) — leave ticked for the Main Menu, where \"Exit to Title Screen\" doesn't apply.")]
    public bool disableExitToTitle = true;
    [Tooltip("Shown while browsing the quit row, warning that unsaved data will be lost. Hidden the rest of the time.")]
    public GameObject exitQuitWarning;

    [Header("Volume step sizes")]
    [Tooltip("How much Left/Right nudges a volume row per press.")]
    public int volumeStep = 5;
    [Tooltip("How much Left/Right nudges a volume row per press while the Sprint action (Shift / gamepad stick-click) is held.")]
    public int volumeStepFine = 1;

    [Header("Input Actions (drag from your Input Action Asset)")]
    [SerializeField] private InputActionReference moveActionRef;
    [SerializeField] private InputActionReference interactActionRef;
    [SerializeField] private InputActionReference cancelActionRef;

    private InputAction moveAction;
    private InputAction interactAction;
    private InputAction cancelAction;
    private InputAction sprintAction;   // reused as the volume slider's fine-step modifier

    private void Start()
    {
        moveAction     = moveActionRef     != null ? moveActionRef.action     : InputSystem.actions.FindAction("Move");
        interactAction = interactActionRef != null ? interactActionRef.action : InputSystem.actions.FindAction("Interact");

        // The Cancel Action Ref in the Inspector is wired to the UI map's Cancel (Escape/gamepad
        // East), not the Player map's Cancel (X) — the same key everything else in this menu uses.
        // Go straight to the Player map's Cancel so X actually backs out, regardless of what's
        // dragged into the Inspector slot.
        cancelAction = InputSystem.actions.FindAction("Player/Cancel")
                     ?? (cancelActionRef != null ? cancelActionRef.action : InputSystem.actions.FindAction("Cancel"));

        // Reuses the Player map's Sprint action (Shift / gamepad stick-click) as the volume
        // slider's fine-step modifier — nothing else needs it while the menu has input focus.
        sprintAction = InputSystem.actions.FindAction("Player/Sprint");

        if (moveAction     == null) Debug.LogError("[SettingsMenu] 'Move' action not found. Drag it into Move Action Ref in the Inspector.");
        if (interactAction == null) Debug.LogError("[SettingsMenu] 'Interact' action not found. Drag it into Interact Action Ref in the Inspector — without this you cannot enter sections or confirm choices.");
        if (cancelAction   == null) Debug.LogWarning("[SettingsMenu] 'Cancel' action not found. Drag it into Cancel Action Ref in the Inspector — without this you cannot back out of the menu.");

        moveAction?.Enable();
        interactAction?.Enable();
        cancelAction?.Enable();
        sprintAction?.Enable();

        foreach (Tab tab in tabs)
        {
            if (tab.panel == null) continue;
            SettingsPanel panel = tab.panel;
            TabKind kind = tab.kind;
            panel.onOptionChosen += (row, option) => ApplyChoice(kind, row, option);
            panel.HideInstant();
        }

        if (rootPaper != null) rootPaper.HideInstant();

        if (disableExitToTitle)
            foreach (Tab tab in tabs)
                if (tab.kind == TabKind.Exit && tab.panel != null)
                    tab.panel.SetRowDisabled(exitTitleRow, true);

        if (exitQuitWarning != null) exitQuitWarning.SetActive(false);
    }

    // ---------------------------------------------------------------- open / close

    /// <summary>Slides the settings screen in and hands it input. Called by the main menu.</summary>
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        focus = Focus.Tabs;
        tabIndex = 0;
        rowIndex = 0;
        ignoreInteractThisFrame = true;

        RefreshTabColors();

        // Activate the whole sheet FIRST (parked off screen, not yet sliding) — a page set active
        // while rootPaper is still inactive never finishes initialising (TMP/layout skip their
        // setup for a child under an inactive ancestor), which used to leave arrows measured wrong
        // for one frame and visibly snap once OpenRoutine re-measured them after the slide-in.
        if (rootPaper != null) rootPaper.PrepareHidden();

        // Show the starting tab's page immediately — it rides in with the whole sheet. Activate it
        // BEFORE seeding values: arrow placement reads real layout, and an inactive object's rect
        // isn't kept up to date, so seeding first left the arrows parked in the wrong place.
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].panel == null) continue;
            if (i == tabIndex)
                tabs[i].panel.gameObject.SetActive(true);
            else
                tabs[i].panel.HideInstant();
        }

        SeedFromSavedSettings();

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        busy = true;
        if (rootPaper != null) yield return rootPaper.Play(true);
        // Re-place the arrows now that everything is active and laid out, in case a layout group
        // moved the rows after they were first positioned.
        if (CurrentPanel != null) CurrentPanel.ResetCursors();
        busy = false;
    }

    /// <summary>Slides the settings screen away and gives input back to the main menu.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        busy = true;
        if (rootPaper != null) yield return rootPaper.Play(false);
        foreach (Tab tab in tabs)
            if (tab.panel != null) tab.panel.HideInstant();
        busy = false;
        IsOpen = false;
    }

    // ---------------------------------------------------------------- input

    private void Update()
    {
        if (!IsOpen || busy) return;

        if (ignoreInteractThisFrame)
        {
            ignoreInteractThisFrame = false;
            return;
        }

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            Back();
            return;
        }

        if (moveAction != null && moveAction.WasPressedThisFrame())
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                if (input.x != 0f) Horizontal(input.x > 0 ? 1 : -1);
            }
            else if (input.y != 0f)
            {
                Vertical(input.y > 0 ? -1 : 1);   // screen-up = earlier row
            }
        }

        if (interactAction != null && interactAction.WasPressedThisFrame())
            Confirm();
    }

    private void Horizontal(int dir)
    {
        switch (focus)
        {
            case Focus.Tabs:
                if (tabs.Count == 0) return;
                int next = (tabIndex + dir + tabs.Count) % tabs.Count;
                if (next != tabIndex) StartCoroutine(SwitchTab(next));
                break;

            case Focus.Options:
                SettingsPanel panel = CurrentPanel;
                if (panel == null) return;
                int count = panel.OptionCount(rowIndex);
                if (count <= 0) return;

                if (panel.IsValueRow(rowIndex))
                {
                    // Volumes clamp at the ends and apply as you move, so you hear the change.
                    bool fineStep = sprintAction != null && sprintAction.IsPressed();
                    int step = fineStep ? volumeStepFine : volumeStep;
                    int value = Mathf.Clamp(optionIndex + dir * step, 0, count - 1);
                    if (value == optionIndex) return;
                    optionIndex = value;
                    panel.ChooseOption(rowIndex, optionIndex);
                }
                else
                {
                    // Choice rows wrap, and only move the arrow — nothing applies until Interact.
                    // Wraps within the selectable range only, so a capped-off option (e.g. a
                    // window scale too big for this display) is never reachable.
                    int range = Mathf.Min(count - 1, panel.GetMaxSelectableOption(rowIndex)) + 1;
                    optionIndex = (optionIndex + dir + range) % range;
                    panel.PointOptionArrow(rowIndex, optionIndex);
                }
                break;
        }
    }

    private void Vertical(int dir)
    {
        SettingsPanel panel = CurrentPanel;

        // Down from the tab bar drops into the page, same as pressing Interact.
        if (focus == Focus.Tabs)
        {
            if (dir > 0) EnterSections();
            return;
        }

        if (focus != Focus.Sections || panel == null || panel.RowCount == 0) return;

        int next = rowIndex;
        for (int i = 0; i < panel.RowCount; i++)
        {
            next = (next + dir + panel.RowCount) % panel.RowCount;
            if (!panel.IsRowDisabled(next)) break;   // stops on the first enabled row it finds
        }
        rowIndex = next;
        panel.PointSectionArrow(rowIndex);
    }

    private void Confirm()
    {
        SettingsPanel panel = CurrentPanel;

        switch (focus)
        {
            case Focus.Tabs:
                EnterSections();
                break;

            case Focus.Sections:
                if (panel == null || panel.OptionCount(rowIndex) == 0 || panel.IsRowDisabled(rowIndex)) return;
                focus = Focus.Options;
                optionIndex = panel.GetChosen(rowIndex);
                // A value row's arrow always rests beside the bar/percentage regardless of the
                // option index (ResetCursors already parked it there) — re-pointing it here just
                // caused a visible jump toward the bar for no reason. Choice rows still need this,
                // since their target changes per option.
                if (!panel.IsValueRow(rowIndex)) panel.PointOptionArrow(rowIndex, optionIndex);
                if (IsExitQuitRow(rowIndex) && exitQuitWarning != null) exitQuitWarning.SetActive(true);
                break;

            case Focus.Options:
                if (panel == null) return;
                panel.ChooseOption(rowIndex, optionIndex);   // applies + saves via ApplyChoice
                focus = Focus.Sections;
                if (exitQuitWarning != null) exitQuitWarning.SetActive(false);
                break;
        }
    }

    // True if the currently-focused row is the Exit tab's "Return to Desktop" (quit) row.
    private bool IsExitQuitRow(int row) =>
        tabIndex >= 0 && tabIndex < tabs.Count && tabs[tabIndex].kind == TabKind.Exit && row == exitQuitRow;

    private void Back()
    {
        SettingsPanel panel = CurrentPanel;

        switch (focus)
        {
            case Focus.Options:
                // Snap the arrow back to whatever is actually in effect — nothing was applied.
                // Value rows don't need this: their arrow was never moved in the first place (see
                // Confirm()), so there's nothing to snap back.
                if (panel != null && !panel.IsValueRow(rowIndex))
                    panel.PointOptionArrow(rowIndex, panel.GetChosen(rowIndex));
                if (exitQuitWarning != null) exitQuitWarning.SetActive(false);
                focus = Focus.Sections;
                break;

            case Focus.Sections:
                if (panel != null) panel.PointSectionArrow(-1);
                focus = Focus.Tabs;
                break;

            case Focus.Tabs:
                Close();
                break;
        }
    }

    private void EnterSections()
    {
        SettingsPanel panel = CurrentPanel;
        if (panel == null || panel.RowCount == 0) return;

        focus = Focus.Sections;
        rowIndex = 0;
        for (int i = 0; i < panel.RowCount && panel.IsRowDisabled(rowIndex); i++)
            rowIndex = (rowIndex + 1) % panel.RowCount;
        panel.PointSectionArrow(rowIndex);
    }

    // Slides the old page out and the new one in, each with its own paper animation.
    private IEnumerator SwitchTab(int next)
    {
        busy = true;

        SettingsPanel outgoing = CurrentPanel;
        if (outgoing != null)
        {
            outgoing.PointSectionArrow(-1);
            yield return outgoing.Play(false);
        }

        tabIndex = next;
        focus = Focus.Tabs;
        rowIndex = 0;
        RefreshTabColors();

        SettingsPanel incoming = CurrentPanel;
        if (incoming != null)
        {
            incoming.ResetCursors();
            yield return incoming.Play(true);
            incoming.ResetCursors();   // re-place once it's on screen and laid out
        }

        busy = false;
    }

    private SettingsPanel CurrentPanel =>
        (tabIndex >= 0 && tabIndex < tabs.Count) ? tabs[tabIndex].panel : null;

    private void RefreshTabColors()
    {
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i].label != null)
                tabs[i].label.color = (i == tabIndex) ? tabSelectedColor : tabUnselectedColor;
    }

    // ---------------------------------------------------------------- settings values

    // Puts every page's arrows where the saved settings say they should be, before the menu appears.
    private void SeedFromSavedSettings()
    {
        foreach (Tab tab in tabs)
        {
            if (tab.panel == null) continue;
            switch (tab.kind)
            {
                case TabKind.Video:
                    tab.panel.SetChosen(0, GameSettings.WindowScale - 1);   // rows: 0 = scale, 1 = fullscreen
                    tab.panel.SetChosen(1, GameSettings.Fullscreen ? 0 : 1); // options: 0 = Yes, 1 = No

                    // Grey out/skip scale options too big for this display — re-checked on every
                    // open in case the window moved to a different monitor since last time.
                    int maxScale = ResolutionManager.Instance != null ? ResolutionManager.Instance.GetMaxFittingScale() : 4;
                    tab.panel.SetMaxSelectableOption(0, maxScale - 1);
                    break;

                case TabKind.Audio:
                    tab.panel.SetChosen(0, GameSettings.MasterVolume);      // rows: master, music, sfx
                    tab.panel.SetChosen(1, GameSettings.MusicVolume);
                    tab.panel.SetChosen(2, GameSettings.SfxVolume);
                    break;

                case TabKind.Exit:
                    // Both rows are single-action rows (Return to Desktop / Exit to Title Screen),
                    // not a choice to seed — chosenIndices already defaults to 0 for each.
                    break;
            }
            tab.panel.ResetCursors();
        }
    }

    // Turns a (tab, row, option) choice into an actual setting change. GameSettings saves + applies.
    private void ApplyChoice(TabKind kind, int row, int option)
    {
        switch (kind)
        {
            case TabKind.Video:
                if (row == 0) GameSettings.WindowScale = option + 1;         // 1x .. 4x
                else if (row == 1) GameSettings.Fullscreen = (option == 0);  // Yes / No
                break;

            case TabKind.Audio:
                if (row == 0) GameSettings.MasterVolume = option;
                else if (row == 1) GameSettings.MusicVolume = option;
                else if (row == 2) GameSettings.SfxVolume = option;
                break;

            case TabKind.Exit:
                if (row == exitQuitRow) Quit();
                // exitTitleRow is disabled (see disableExitToTitle) so this shouldn't normally fire
                // from the Main Menu; Back() is just a safe fallback if it somehow does.
                else if (row == exitTitleRow) Back();
                break;
        }
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
