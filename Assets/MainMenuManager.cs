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

    [Header("Animations")]
    public Vector3 selectedScale = new Vector3(1.25f, 1.25f, 1f);
    public Vector3 unselectedScale = new Vector3(0.9f, 0.9f, 1f);
    public Vector3 floatOffset = new Vector3(30f, 0f, 0f);
    public float siblingShiftAmount = 25f;
    public float lerpSpeed = 12f;

    private List<Vector3> startPositions = new List<Vector3>();
    private List<RectTransform> buttonRects = new List<RectTransform>();

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

        startPositions.Clear();
        buttonRects.Clear();
        for (int i = 0; i < mainMenuButtons.Count; i++)
        {
            if (mainMenuButtons[i] != null)
            {
                RectTransform rt = GetButtonRectTransform(mainMenuButtons[i]);
                buttonRects.Add(rt);
                startPositions.Add(rt != null ? rt.anchoredPosition3D : Vector3.zero);

                // Set initial immediate states to prevent popping
                mainMenuButtons[i].color = (i == 0) ? selectedColor : unselectedColor;
                if (rt != null)
                {
                    rt.localScale = (i == 0) ? selectedScale : unselectedScale;
                    if (i == 0)
                    {
                        rt.anchoredPosition3D = startPositions[0] + floatOffset;
                    }
                }
            }
            else
            {
                buttonRects.Add(null);
                startPositions.Add(Vector3.zero);
            }
        }
    }

    void Update()
    {
        if(isFading) return;
        
        AnimateButtons();

        if(moveAction.WasPressedThisFrame()){
            Vector2 input = moveAction.ReadValue<Vector2>();
            if(input.y < 0){
                index++;
                if(index == mainMenuButtons.Count){
                    index = 0;
                }
            }
            else if(input.y > 0){
                index--;
                if(index < 0){
                    index = mainMenuButtons.Count - 1;
                }
            }
        }
        if(Interact.WasPressedThisFrame()&&index == 2){
            Settings();
        }
        if(Interact.WasPressedThisFrame()&&index == 0&&!IsSettingActive){
            StartCoroutine(FadeAndLoadScene());
        }
        if(cancel.WasPressedThisFrame()&&IsSettingActive){
            SettingsBack();
        }

    }

    private void AnimateButtons()
    {
        if (index < 0 || index >= mainMenuButtons.Count) return;

        for (int i = 0; i < mainMenuButtons.Count; i++)
        {
            Image buttonImage = mainMenuButtons[i];
            if (buttonImage == null) continue;

            RectTransform rt = buttonRects[i];
            if (rt == null) continue;

            Vector3 targetPosition;
            Vector3 targetScale;
            Color targetColor;

            if (i == index)
            {
                // Selected button
                targetPosition = startPositions[i] + floatOffset;
                targetScale = selectedScale;
                targetColor = selectedColor;
            }
            else
            {
                // Unselected button
                Vector3 direction = startPositions[i] - startPositions[index];
                Vector3 pushDir;
                if (direction.sqrMagnitude > 0.001f)
                {
                    pushDir = direction.normalized;
                }
                else
                {
                    pushDir = (i > index) ? Vector3.down : Vector3.up;
                }

                targetPosition = startPositions[i] + pushDir * siblingShiftAmount;
                targetScale = unselectedScale;
                targetColor = unselectedColor;
            }

            // Smoothly interpolate position, scale, and color
            rt.anchoredPosition3D = Vector3.Lerp(rt.anchoredPosition3D, targetPosition, Time.deltaTime * lerpSpeed);
            rt.localScale = Vector3.Lerp(rt.localScale, targetScale, Time.deltaTime * lerpSpeed);
            buttonImage.color = Color.Lerp(buttonImage.color, targetColor, Time.deltaTime * lerpSpeed);
        }
    }

    private RectTransform GetButtonRectTransform(Image image)
    {
        if (image == null) return null;

        // If the image is on a child object (like a border or background) and the parent has a Button,
        // we want to animate the parent so the text and border move together.
        Button parentButton = image.GetComponentInParent<Button>();
        if (parentButton != null)
        {
            return parentButton.GetComponent<RectTransform>();
        }

        // Fallback to the image's own RectTransform
        return image.rectTransform;
    }
    public void Settings(){
        IsSettingActive = true;
        MainMenu.SetActive(false);
        SettingMenu.SetActive(true);
    }
    public void SettingsBack(){
        IsSettingActive = false;
        MainMenu.SetActive(true);
        SettingMenu.SetActive(false);
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
