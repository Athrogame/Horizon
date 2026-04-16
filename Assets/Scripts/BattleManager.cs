using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager I{ get; private set; }
    void Awake(){
        if(I ==null){
            I = this;
        }
        else{
            Destroy(gameObject);
            DontDestroyOnLoad(I);
        }

    }

    public GameObject BattleCanvas;
    
    void Start()
    {
        
    }
    void Update()
    {
        if(UnityEngine.InputSystem.Keyboard.current.qKey.wasPressedThisFrame){
BattleCanvas.SetActive(!BattleCanvas.activeSelf);
        }
        
    }
}
