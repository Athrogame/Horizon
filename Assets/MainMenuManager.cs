using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    public Color selectedColor;
    public Color unselectedColor;
    public List<Image> mainMenuButtons = new List<Image>();

    void Start()
    {
        selectedColor.a = 255f;
        unselectedColor.a = 255f;
        for (int i = 0; i < mainMenuButtons.Count; i++)
        {
            mainMenuButtons[i].color = unselectedColor;
            if(i ==0 ){
                mainMenuButtons[i].color = selectedColor;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
