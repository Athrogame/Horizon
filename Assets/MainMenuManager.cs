using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public Color selectedColor;
    public Color unselectedColor;
    public List<Image> mainMenuButtons = new List<Image>();
    public GameObject SettingMenu;
    public GameObject MainMenu;
    private int index;
    public InputActionReference moveActionReference;
    private InputAction moveAction;
    private InputAction Interact;
    private InputAction cancel;
    public bool IsSettingActive = false;
    public Image fadeImage;
    public float fadeDuration = 1f;
    public string sceneToLoad;
    private bool isFading = false;

    // True while the main menu is hidden because the load menu is showing over it.
    private bool mainMenuHiddenForLoad = false;

    [Header("Button roles (assign the SAME Image objects that are in Main Menu Buttons)")]
    [Tooltip("Opens the Saves/Load panel. Greyed out and skipped when no saves exist.")]
    public Image playGameButton;
    [Tooltip("Starts a fresh game (resets play time / mood, loads the first scene).")]
    public Image newGameButton;
    [Tooltip("Opens the Settings panel.")]
    public Image settingsButton;
    [Tooltip("Colour used for a disabled button (e.g. Play Game when there are no saves).")]
    public Color disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("Float (base values — each button varies around these)")]
    [Tooltip("How far buttons drift up/down, in UI units.")]
    public float bobHeight = 12f;
    [Tooltip("How fast buttons bob up/down.")]
    public float bobSpeed = 1.5f;
    [Tooltip("How far buttons drift side to side, in UI units.")]
    public float swayDistance = 6f;
    [Tooltip("How fast buttons sway side to side.")]
    public float swaySpeed = 0.8f;
    [Tooltip("How many degrees buttons gently rock back and forth.")]
    public float rockAngle = 2.5f;
    [Tooltip("How fast buttons rock back and forth.")]
    public float rockSpeed = 1f;
    [Tooltip("0 = every button floats identically, 1 = big random differences per button.")]
    [Range(0f, 1f)]
    public float variation = 0.4f;

    [Header("Selection")]
    [Tooltip("Amount added to the selected button's scale on each axis (added on select, removed on deselect).")]
    public float selectedScaleAmount = 0.15f;
    [Tooltip("How fast the selected button tweens to its bigger size. Higher = snappier.")]
    public float scaleLerpSpeed = 12f;

    [Header("Menu slide (sheet from bottom)")]
    [Tooltip("How long a menu takes to slide up or down, in seconds.")]
    public float menuSlideDuration = 0.35f;

    // Resting anchoredPositions of the slide-in panels, captured before they're hidden off-screen.
    private Vector2 settingRestPos;
    private Coroutine settingRoutine;

    // Per-button resting state + individual float parameters, so each floats differently.
    private List<RectTransform> buttonRects = new List<RectTransform>();
    private List<Vector3> startPositions = new List<Vector3>();
    private List<Quaternion> startRotations = new List<Quaternion>();
    private List<Vector3> startScales = new List<Vector3>();
    private List<FloatParams> floatParams = new List<FloatParams>();

    // Randomized floating settings for one button.
    private struct FloatParams
    {
        public float bobHeight, bobSpeed, bobPhase;
        public float swayDistance, swaySpeed, swayPhase;
        public float rockAngle, rockSpeed, rockPhase;
    }

    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        Interact = InputSystem.actions.FindAction("Interact");
        cancel = InputSystem.actions.FindAction("Cancel");
        index = 0;
        selectedColor.a = 255f;
        unselectedColor.a = 255f;
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
            fadeImage.raycastTarget = false;
        }

        SetupFloating();
        EnsureValidSelection();
        RefreshSelection();

        // Remember where the settings panel is meant to sit, then hide it below the screen.
        if (SettingMenu != null)
        {
            settingRestPos = SettingMenu.GetComponent<RectTransform>().anchoredPosition;
            SettingMenu.SetActive(false);
        }
    }

    void Update()
    {
        if(isFading) return;

        FloatButtons();

        // The shared save/load menu (SaveSlotSelectMenu) owns input while it's open — don't
        // navigate the main menu buttons underneath it.
        bool menuOpen = SaveSlotSelectMenu.I != null && SaveSlotSelectMenu.I.IsOpen;

        // If we hid the main menu to show the load menu, restore it once that menu closes (cancel).
        if (mainMenuHiddenForLoad && !menuOpen)
        {
            mainMenuHiddenForLoad = false;
            if (MainMenu != null) MainMenu.SetActive(true);
            RefreshSelection();
        }

        if(!menuOpen && !IsSettingActive){
            if(moveAction.WasPressedThisFrame()){
                Vector2 input = moveAction.ReadValue<Vector2>();
                if(input.y < 0) MoveSelection(1);
                else if(input.y > 0) MoveSelection(-1);
            }
            if(Interact.WasPressedThisFrame()){
                ActivateSelected();
            }
        }
        if(cancel.WasPressedThisFrame()&&IsSettingActive){
            SettingsBack();
        }

    }

    // Captures each button's resting position/rotation and rolls a unique set of float values for it.
    private void SetupFloating()
    {
        buttonRects.Clear();
        startPositions.Clear();
        startRotations.Clear();
        startScales.Clear();
        floatParams.Clear();

        for (int i = 0; i < mainMenuButtons.Count; i++)
        {
            RectTransform rt = GetButtonRectTransform(mainMenuButtons[i]);
            buttonRects.Add(rt);
            startPositions.Add(rt != null ? rt.anchoredPosition3D : Vector3.zero);
            startRotations.Add(rt != null ? rt.localRotation : Quaternion.identity);
            startScales.Add(rt != null ? rt.localScale : Vector3.one);

            floatParams.Add(new FloatParams
            {
                bobHeight = bobHeight * Vary(),
                bobSpeed = bobSpeed * Vary(),
                bobPhase = Random.Range(0f, Mathf.PI * 2f),
                swayDistance = swayDistance * Vary(),
                swaySpeed = swaySpeed * Vary(),
                swayPhase = Random.Range(0f, Mathf.PI * 2f),
                rockAngle = rockAngle * Vary(),
                rockSpeed = rockSpeed * Vary(),
                rockPhase = Random.Range(0f, Mathf.PI * 2f),
            });
        }
    }

    // A random multiplier within +/- variation of 1 (e.g. variation 0.4 => 0.6 to 1.4).
    private float Vary()
    {
        return 1f + Random.Range(-variation, variation);
    }

    // Applies the floating drift + rock to every button each frame, around its resting spot.
    private void FloatButtons()
    {
        float t = Time.time;
        for (int i = 0; i < buttonRects.Count; i++)
        {
            RectTransform rt = buttonRects[i];
            if (rt == null) continue;

            FloatParams p = floatParams[i];
            float bob = Mathf.Sin(t * p.bobSpeed + p.bobPhase) * p.bobHeight;
            float sway = Mathf.Sin(t * p.swaySpeed + p.swayPhase) * p.swayDistance;
            float rock = Mathf.Sin(t * p.rockSpeed + p.rockPhase) * p.rockAngle;

            rt.anchoredPosition3D = startPositions[i] + new Vector3(sway, bob, 0f);
            rt.localRotation = startRotations[i] * Quaternion.Euler(0f, 0f, rock);

            // Smoothly tween scale toward the selected/unselected target.
            Vector3 targetScale = (i == index)
                ? startScales[i] + new Vector3(selectedScaleAmount, selectedScaleAmount, selectedScaleAmount)
                : startScales[i];
            rt.localScale = Vector3.Lerp(rt.localScale, targetScale, Time.deltaTime * scaleLerpSpeed);
        }
    }

    // Instantly colors the buttons so the selected one stands out. No animation.
    private void RefreshSelection()
    {
        for (int i = 0; i < mainMenuButtons.Count; i++)
        {
            if (mainMenuButtons[i] == null) continue;
            if (IsButtonDisabled(i))
                mainMenuButtons[i].color = disabledColor;
            else
                mainMenuButtons[i].color = (i == index) ? selectedColor : unselectedColor;
        }
    }

    // Play Game is disabled (greyed out + skipped) while there are no saves to load.
    private bool IsButtonDisabled(int i)
    {
        if (i < 0 || i >= mainMenuButtons.Count) return true;
        return mainMenuButtons[i] != null && mainMenuButtons[i] == playGameButton && !AnySaveExists();
    }

    private bool AnySaveExists()
    {
        if (SaveManager.I == null) return false;
        for (int i = 0; i < SaveManager.I.slotCount; i++)
            if (SaveManager.I.HasSave(i)) return true;
        return false;
    }

    // Advances the selection by dir (+1 down / -1 up), wrapping and skipping disabled buttons.
    private void MoveSelection(int dir)
    {
        if (mainMenuButtons.Count == 0) return;
        int start = index;
        do
        {
            index = (index + dir + mainMenuButtons.Count) % mainMenuButtons.Count;
        }
        while (IsButtonDisabled(index) && index != start);
        RefreshSelection();
    }

    // If the currently-selected button is disabled (e.g. Play Game with no saves), move off it.
    private void EnsureValidSelection()
    {
        if (mainMenuButtons.Count == 0) return;
        if (IsButtonDisabled(index)) MoveSelection(1);
    }

    // Runs the action for whichever button is currently selected, matched by role.
    private void ActivateSelected()
    {
        if (index < 0 || index >= mainMenuButtons.Count) return;
        if (IsButtonDisabled(index)) return;

        Image selected = mainMenuButtons[index];
        if (selected == null) return;

        if (selected == playGameButton)
        {
            if (AnySaveExists() && SaveSlotSelectMenu.I != null)
            {
                if (MainMenu != null) MainMenu.SetActive(false);   // hide the main menu behind the load menu
                mainMenuHiddenForLoad = true;
                SaveSlotSelectMenu.I.Open(SaveSlotSelectMenu.SaveMenuMode.Load);
            }
        }
        else if (selected == newGameButton)
        {
            if (SaveManager.I != null) SaveManager.I.NewGame();
            StartCoroutine(FadeAndLoadScene());
        }
        else if (selected == settingsButton)
        {
            Settings();
        }
    }

    private RectTransform GetButtonRectTransform(Image image)
    {
        if (image == null) return null;

        // If the image sits on a child (border/background) under a Button, float the whole button
        // so text and border move together.
        Button parentButton = image.GetComponentInParent<Button>();
        if (parentButton != null)
        {
            return parentButton.GetComponent<RectTransform>();
        }
        return image.rectTransform;
    }

    public void Settings(){
        IsSettingActive = true;
        if (settingRoutine != null) StopCoroutine(settingRoutine);
        settingRoutine = StartCoroutine(SlidePanel(SettingMenu, settingRestPos, true));
    }
    public void SettingsBack(){
        IsSettingActive = false;
        if (settingRoutine != null) StopCoroutine(settingRoutine);
        settingRoutine = StartCoroutine(SlidePanel(SettingMenu, settingRestPos, false));
    }
    // Slides a panel up from below the screen (show) or back down and hides it (hide),
    // like a sheet of paper. Works entirely with the Canvas RectTransform.
    private IEnumerator SlidePanel(GameObject panel, Vector2 restPos, bool show)
    {
        if (panel == null) yield break;

        RectTransform rt = panel.GetComponent<RectTransform>();

        // Off-screen position = resting spot dropped down by the full canvas height.
        float canvasHeight = Screen.height;
        Canvas rootCanvas = panel.GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            canvasHeight = rootCanvas.rootCanvas.GetComponent<RectTransform>().rect.height;
        }
        Vector2 hiddenPos = restPos + Vector2.down * canvasHeight;

        Vector2 from = show ? hiddenPos : restPos;
        Vector2 to = show ? restPos : hiddenPos;

        panel.SetActive(true);
        rt.anchoredPosition = from;

        float t = 0f;
        while (t < menuSlideDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / menuSlideDuration));
            rt.anchoredPosition = Vector2.Lerp(from, to, k);
            yield return null;
        }
        rt.anchoredPosition = to;

        // Once it's slid back down, actually deactivate it.
        if (!show) panel.SetActive(false);
    }

    IEnumerator FadeAndLoadScene()
    {
        isFading = true;
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true;
            Color c = fadeImage.color;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                c.a = Mathf.Clamp01(t / fadeDuration);
                fadeImage.color = c;
                yield return null;
            }
            c.a = 1f;
            fadeImage.color = c;
        }
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }
}
