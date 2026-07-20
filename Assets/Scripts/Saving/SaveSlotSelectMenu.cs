using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// In-game "pick a slot to save into" menu, opened by a <see cref="SavePoint"/>.
/// Shows the 3 slots (reusing SaveSlotUI) so the player sees existing saves before overwriting,
/// and navigates with the same Move / Interact / Cancel input as the main menu.
///
/// Freezes the player while open.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SaveSlotSelectMenu : MonoBehaviour
{
    [Header("Show / Hide")]
    [Tooltip("Optional. The CHILD object holding the menu visuals — toggled active on open/close. " +
             "If empty, the menu instead hides by fading its CanvasGroup. " +
             "NEVER set this to the menu's own root object, or it can't reopen.")]
    public GameObject contentPanel;

    [Header("Slots (one SaveSlotUI per save slot)")]
    public List<SaveSlotUI> slots = new List<SaveSlotUI>();

    [Header("Selection highlight")]
    [Tooltip("One background Image per slot (same order as Slots), tinted to show which is selected.")]
    public List<Image> slotBackgrounds = new List<Image>();
    [Tooltip("Colour of the currently-selected slot's background.")]
    public Color selectedColor = Color.red;
    [Tooltip("Colour of the non-selected slots' backgrounds.")]
    public Color unselectedColor = Color.white;
    [Tooltip("How much bigger the selected slot gets (1 = no size change). Works even without Slot Backgrounds.")]
    public float selectedScale = 1.12f;

    [Header("Slot idle float (subtle — keep smaller than the main-menu buttons)")]
    [Tooltip("How far each slot drifts up/down, in UI units.")]
    public float floatBobHeight = 4f;
    public float floatBobSpeed = 1.1f;
    [Tooltip("How far each slot drifts side to side, in UI units.")]
    public float floatSwayDistance = 2f;
    public float floatSwaySpeed = 0.7f;
    [Tooltip("How many degrees each slot gently rocks.")]
    public float floatRockAngle = 1f;
    public float floatRockSpeed = 0.8f;

    // Original scales of each slot, captured once, so the selected-scale can be applied cleanly.
    private readonly List<Vector3> slotBaseScales = new List<Vector3>();

    // Rest position/rotation of each slot + a per-slot phase, so they float around their spot out of sync.
    private readonly List<Vector3> slotBasePositions = new List<Vector3>();
    private readonly List<Quaternion> slotBaseRotations = new List<Quaternion>();
    private readonly List<float> slotFloatPhases = new List<float>();

    [Header("Slide animation")]
    [Tooltip("How long the menu takes to slide up from / down to the bottom, in seconds.")]
    public float slideDuration = 0.35f;
    [Tooltip("Scale the panel starts at while sliding in, before it pops to full size (1 = no pop).")]
    public float slideStartScale = 0.85f;
    [Tooltip("How long the 'settle' pop to full size takes AFTER the slide finishes. Raise this to see it more.")]
    public float settleDuration = 0.35f;

    [Header("Load fade (optional)")]
    [Tooltip("Full-screen black Image on THIS canvas (a direct child of the canvas, NOT inside Content Panel). " +
             "Used to fade to/from black when loading a save. Leave empty for an instant load.")]
    public Image fadeOverlay;
    [Tooltip("How long the fade to/from black takes when loading a save. Increase for a slower, smoother transition.")]
    public float loadFadeDuration = 0.7f;

    [Header("Confirmation")]
    [Tooltip("Optional text briefly shown after saving, e.g. 'Saved!'.")]
    public TextMeshProUGUI confirmationText;
    public float confirmationDuration = 1.2f;

    [Header("Overwrite prompt")]
    [Tooltip("Your DialogueBox that asks 'Overwrite?' when saving onto an occupied slot. " +
             "Wire its Yes option's onChosen to ConfirmOverwrite() and its No option to CancelOverwrite(). " +
             "Leave empty to overwrite silently.")]
    public DialogueBox overwriteDialogue;

    private int pendingOverwriteSlot;

    private InputAction moveAction;
    private InputAction interactAction;
    private InputAction cancelAction;

    // Save = pick a slot to write into (at a save point). Load = pick a slot to load (title screen).
    public enum SaveMenuMode { Save, Load }
    private SaveMenuMode mode = SaveMenuMode.Save;

    private int index;
    private bool isOpen;
    private bool transitioning;   // true during the load fade, so callers still treat us as "busy"
    private bool ready;           // false until the open slide+settle animation finishes — blocks input
    private bool ignoreInteractThisFrame;

    // "Busy" = the menu is open OR mid load-transition. MainMenuManager uses this to know when
    // it's safe to bring the main menu back.
    public bool IsOpen => isOpen || transitioning;

    // The one live menu. SavePoints in any scene use this automatically.
    public static SaveSlotSelectMenu I { get; private set; }

    // Hidden/shown via CanvasGroup so the GameObject stays ACTIVE (Update keeps running and
    // the menu can always reopen). Never SetActive(false) the object this script lives on.
    private CanvasGroup canvasGroup;

    // Disabling this Canvas hides EVERYTHING under it instantly while the GameObject stays active.
    private Canvas menuCanvas;

    // Slide state: the panel's on-screen resting spot, captured before it's first hidden.
    private RectTransform panelRect;
    private Vector2 panelRestPos;
    private bool panelRestCaptured;
    private Coroutine slideRoutine;

    [Header("Persistence")]
    [Tooltip("Keep this menu alive across scene loads so every SavePoint can share it (add it to ONE scene only). " +
             "Use a dedicated Screen Space - Overlay canvas so it doesn't need a scene camera.")]
    public bool persistAcrossScenes = true;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        menuCanvas = GetComponentInParent<Canvas>();

        if (persistAcrossScenes)
        {
            // Only one persistent menu ever lives; destroy any duplicate a later scene brings in.
            if (I != null && I != this)
            {
                Destroy(transform.root.gameObject);
                return;
            }
            I = this;
            // DontDestroyOnLoad only works on a ROOT object, so persist the whole canvas root.
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            I = this;
        }
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    private void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        interactAction = InputSystem.actions.FindAction("Interact");
        cancelAction = InputSystem.actions.FindAction("Cancel");

        // Remember where the panel sits on-screen, then hide it (off-screen + not interactable).
        CapturePanelRest();

        if (confirmationText != null) confirmationText.gameObject.SetActive(false);
        SetInteractable(false);
        if (menuCanvas != null) menuCanvas.enabled = false;   // hidden until opened

        // Auto-build a full-screen black fade image if none was assigned, so load always fades.
        EnsureFadeOverlay();
        if (fadeOverlay != null)
        {
            Color c = fadeOverlay.color; c.a = 0f; fadeOverlay.color = c;
            fadeOverlay.raycastTarget = false;
            fadeOverlay.gameObject.SetActive(false);
        }

        if (contentPanel == null)
            Debug.LogWarning("SaveSlotSelectMenu: 'Content Panel' is NOT assigned. Assign the child Panel that holds the slots, " +
                             "or the menu can't hide/slide and will stay open after loading.");
    }

    // Creates a full-screen black Image on the menu's own canvas if the user didn't assign one.
    private void EnsureFadeOverlay()
    {
        if (fadeOverlay != null || menuCanvas == null) return;

        GameObject go = new GameObject("SaveLoadFadeOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(menuCanvas.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();   // on top of everything

        fadeOverlay = go.GetComponent<Image>();
        fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
        fadeOverlay.raycastTarget = false;
        go.SetActive(false);
    }

    private void CapturePanelRest()
    {
        if (panelRestCaptured || contentPanel == null) return;
        panelRect = contentPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRestPos = panelRect.anchoredPosition;
            panelRestCaptured = true;
        }
    }

    // Toggles whether the menu blocks/receives input (does NOT move it).
    private void SetInteractable(bool on)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = on ? 1f : 0f;
        canvasGroup.interactable = on;
        canvasGroup.blocksRaycasts = on;
    }

    /// <summary>Open in Save mode (freezes the player). Called by a SavePoint.</summary>
    public void Open()
    {
        Open(SaveMenuMode.Save);
    }

    /// <summary>Open the menu in the given mode. Save = write a slot, Load = load a slot.</summary>
    public void Open(SaveMenuMode openMode)
    {
        if (isOpen) return;
        mode = openMode;
        isOpen = true;
        ready = false;   // no input until the open animation finishes
        index = 0;
        // The same Interact press that opened us is still down this frame — don't treat it as "save".
        ignoreInteractThisFrame = true;

        CapturePanelRest();
        if (menuCanvas != null) menuCanvas.enabled = true;
        if (contentPanel != null) contentPanel.SetActive(true);   // a prior load may have deactivated it
        SetInteractable(true);
        if (confirmationText != null) confirmationText.gameObject.SetActive(false);

        RefreshSlots();
        RefreshSelection();

        Debug.Log($"SaveSlotSelectMenu: opened (mode={mode}). Slots={slots.Count}, SlotBackgrounds={slotBackgrounds.Count}, ContentPanel={(contentPanel != null)}, FadeOverlay={(fadeOverlay != null)}");

        if (PlayerController.I != null)
            PlayerController.I.LockMovement();

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideMenu(true));
    }

    /// <summary>Close the menu and release the player.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        // Stop accepting input immediately; the panel slides back down and then deactivates.
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (PlayerController.I != null)
            PlayerController.I.UnlockMovement();

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideMenu(false));
    }

    /// <summary>Hide the menu immediately with no slide (used right before loading a game).</summary>
    public void CloseInstant()
    {
        isOpen = false;
        if (slideRoutine != null) StopCoroutine(slideRoutine);

        // Disabling the Canvas hides everything under it, regardless of panel structure.
        if (menuCanvas != null) menuCanvas.enabled = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (contentPanel != null) contentPanel.SetActive(false);

        if (PlayerController.I != null)
            PlayerController.I.UnlockMovement();
    }

    // Fade to black, load the save, then fade back in once the new scene has settled.
    // The menu persists across the load (DontDestroyOnLoad), so this one coroutine spans both scenes.
    private IEnumerator FadeAndLoad(int slot)
    {
        // Stop input but KEEP the slot screen visible while we fade to black over it.
        // transitioning keeps IsOpen true so the main menu doesn't pop back in mid-fade.
        isOpen = false;
        transitioning = true;
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }

        yield return Fade(0f, 1f);            // fade to black over the still-visible slots

        // Now fully black: hide the slots (canvas stays enabled so the screen stays black).
        if (contentPanel != null) contentPanel.SetActive(false);

        SaveManager.I.LoadGame(slot);         // load the scene, still under black

        // Let the scene load + player reposition happen while the screen is black.
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.15f);

        yield return Fade(1f, 0f);            // fade in to the new scene (slots already hidden)

        // Fully hide the menu and re-arm the panel for next time.
        if (menuCanvas != null) menuCanvas.enabled = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
        if (contentPanel != null) contentPanel.SetActive(true);
        transitioning = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.raycastTarget = true;
        Color c = fadeOverlay.color;
        c.a = from;
        fadeOverlay.color = c;

        float t = 0f;
        while (t < loadFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / loadFadeDuration));
            fadeOverlay.color = c;
            yield return null;
        }
        c.a = to;
        fadeOverlay.color = c;

        if (to <= 0f)
        {
            fadeOverlay.raycastTarget = false;
            fadeOverlay.gameObject.SetActive(false);
        }
    }

    // Slides the panel up from below the screen (show) or back down (hide), then hides it.
    private IEnumerator SlideMenu(bool show)
    {
        if (contentPanel != null) contentPanel.SetActive(true);

        // No panel/RectTransform to move — just fall back to an instant show/hide.
        if (panelRect == null)
        {
            SetInteractable(show);
            if (show) ready = true;   // no animation to wait for
            if (!show && contentPanel != null) contentPanel.SetActive(false);
            yield break;
        }

        // Off-screen = resting spot dropped down by the full canvas height.
        float canvasHeight = Screen.height;
        Canvas rootCanvas = panelRect.GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            canvasHeight = rootCanvas.rootCanvas.GetComponent<RectTransform>().rect.height;
        Vector2 hiddenPos = panelRestPos + Vector2.down * canvasHeight;

        Vector2 from = show ? hiddenPos : panelRestPos;
        Vector2 to = show ? panelRestPos : hiddenPos;

        panelRect.anchoredPosition = from;

        Vector3 baseScale = panelRect.localScale;

        // Phase 1 — slide the panel into place (kept slightly small on the way in).
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float raw = Mathf.Clamp01(t / slideDuration);

            // Position: smooth glide (no overshoot, so it can't fling off-screen).
            float posK = show ? EaseOutCubic(raw) : EaseInCubic(raw);
            panelRect.anchoredPosition = Vector2.Lerp(from, to, posK);

            if (show) panelRect.localScale = baseScale * slideStartScale;

            yield return null;
        }
        panelRect.anchoredPosition = to;

        // Phase 2 (show only) — the visible "placed down" pop: small -> overshoot -> full size,
        // on its own timeline so it's slow enough to actually see.
        if (show)
        {
            float s = 0f;
            while (s < settleDuration)
            {
                s += Time.unscaledDeltaTime;
                float k = EaseOutBack(Mathf.Clamp01(s / settleDuration));
                panelRect.localScale = baseScale * Mathf.LerpUnclamped(slideStartScale, 1f, k);
                yield return null;
            }
        }
        panelRect.localScale = baseScale;

        // Animation finished — now the player may navigate/select.
        if (show) ready = true;

        // Fully hidden once it's slid back down.
        if (!show)
        {
            SetInteractable(false);
            if (menuCanvas != null) menuCanvas.enabled = false;
        }
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float EaseInCubic(float x) => x * x * x;

    // Overshoots past 1 then settles back to 1 — used for the scale "settle" pop.
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private void Update()
    {
        if (!isOpen) return;

        FloatSlots();

        if (ignoreInteractThisFrame)
        {
            // Consume the opening frame so the open-press can't instantly save.
            ignoreInteractThisFrame = false;
            return;
        }

        // Ignore all input until the open slide + settle animation has finished.
        if (!ready) return;

        // While the overwrite question (a DialogueBox) is up, it owns input — the menu stays put.
        if (DialogueBox.AnyActive) return;

        if (moveAction != null && slots.Count > 0 && moveAction.WasPressedThisFrame())
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            if (input.y < 0) { index = (index + 1) % slots.Count; RefreshSelection(); }
            else if (input.y > 0) { index = (index - 1 + slots.Count) % slots.Count; RefreshSelection(); }
        }

        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            ActivateSelected();
        }

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            Close();
        }
    }

    private void ActivateSelected()
    {
        if (SaveManager.I == null) return;
        int slot = slots[index].slotIndex;

        if (mode == SaveMenuMode.Save)
        {
            // Occupied slot + an overwrite dialogue assigned → ask before overwriting.
            if (SaveManager.I.HasSave(slot) && overwriteDialogue != null)
            {
                pendingOverwriteSlot = slot;
                overwriteDialogue.StartDialogue();   // your question box handles Yes/No
            }
            else
            {
                DoSave(slot);
            }
        }
        else // Load
        {
            if (SaveManager.I.HasSave(slot))
            {
                if (fadeOverlay != null)
                {
                    StartCoroutine(FadeAndLoad(slot));   // fade to black, load, fade back in
                }
                else
                {
                    CloseInstant();                       // no fade overlay assigned — instant load
                    SaveManager.I.LoadGame(slot);
                }
            }
            // Empty slot in Load mode: do nothing (load-only).
        }
    }

    private void DoSave(int slot)
    {
        SaveManager.I.SaveToSlot(slot);
        RefreshSlots();
        if (confirmationText != null)
            StartCoroutine(ShowConfirmation());
    }

    /// <summary>Wire this to the "Yes / Overwrite" option's onChosen — writes the save.</summary>
    public void ConfirmOverwrite()
    {
        ignoreInteractThisFrame = true;   // don't let the confirming press re-trigger the menu
        DoSave(pendingOverwriteSlot);
    }

    /// <summary>Wire this to the "No / Back" option's onChosen — saves nothing.</summary>
    public void CancelOverwrite()
    {
        ignoreInteractThisFrame = true;
    }

    private IEnumerator ShowConfirmation()
    {
        confirmationText.text = "Saved!";
        confirmationText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(confirmationDuration);
        if (confirmationText != null) confirmationText.gameObject.SetActive(false);
    }

    private void RefreshSlots()
    {
        foreach (SaveSlotUI slot in slots)
            if (slot != null) slot.Refresh();
    }

    private void RefreshSelection()
    {
        // Colour highlight (needs Slot Backgrounds assigned).
        for (int i = 0; i < slotBackgrounds.Count; i++)
            if (slotBackgrounds[i] != null)
                slotBackgrounds[i].color = (i == index) ? selectedColor : unselectedColor;

        // Scale highlight (works off the Slots list — no extra wiring needed).
        EnsureBaseScales();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            RectTransform rt = slots[i].transform as RectTransform;
            if (rt == null) continue;
            rt.localScale = slotBaseScales[i] * (i == index ? selectedScale : 1f);
        }
    }

    private void EnsureBaseScales()
    {
        if (slotBaseScales.Count == slots.Count) return;
        slotBaseScales.Clear();
        foreach (SaveSlotUI s in slots)
        {
            RectTransform rt = s != null ? s.transform as RectTransform : null;
            slotBaseScales.Add(rt != null ? rt.localScale : Vector3.one);
        }
    }

    // Captures each slot's resting position/rotation once, so the float drifts around it.
    private void EnsureSlotFloatBase()
    {
        if (slotBasePositions.Count == slots.Count) return;
        slotBasePositions.Clear();
        slotBaseRotations.Clear();
        slotFloatPhases.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            RectTransform rt = slots[i] != null ? slots[i].transform as RectTransform : null;
            slotBasePositions.Add(rt != null ? rt.anchoredPosition3D : Vector3.zero);
            slotBaseRotations.Add(rt != null ? rt.localRotation : Quaternion.identity);
            slotFloatPhases.Add(i * 2.1f);   // spread them out so they don't move in unison
        }
    }

    // Gentle idle drift + rock on each slot around its resting spot (subtler than the main-menu buttons).
    private void FloatSlots()
    {
        EnsureSlotFloatBase();
        float time = Time.unscaledTime;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            RectTransform rt = slots[i].transform as RectTransform;
            if (rt == null) continue;

            float phase = slotFloatPhases[i];
            float bob = Mathf.Sin(time * floatBobSpeed + phase) * floatBobHeight;
            float sway = Mathf.Sin(time * floatSwaySpeed + phase * 1.3f) * floatSwayDistance;
            float rock = Mathf.Sin(time * floatRockSpeed + phase * 0.7f) * floatRockAngle;

            rt.anchoredPosition3D = slotBasePositions[i] + new Vector3(sway, bob, 0f);
            rt.localRotation = slotBaseRotations[i] * Quaternion.Euler(0f, 0f, rock);
        }
    }
}
