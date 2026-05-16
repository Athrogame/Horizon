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
        for (int i = 0; i < mainMenuButtons.Count; i++)
        {
            mainMenuButtons[i].color = unselectedColor;
            if(i ==0 ){
                mainMenuButtons[i].color = selectedColor;
            }
        }
    }

    void Update()
    {
        if(isFading) return;
        foreach(Image image in mainMenuButtons){
            if(mainMenuButtons.IndexOf(image) == index){
                image.color = selectedColor;
            }
            else{
                image.color = unselectedColor;
            }
        }
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
