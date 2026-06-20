using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;


public class BattleManager : MonoBehaviour
{
    public Color selectedColor;
    public Color unselectedColor;
    private InputAction moveAction;
    private InputAction Interact;
    private InputAction cancel;
    [SerializeField] private int index;
    private bool hasMovementLock;
    public List<Image> battleMenuButtons = new List<Image>();
    private bool subMenu;
    public GameObject submenuoptions;
    public GameObject battleChooser;

   
    public static BattleManager I{ get; private set; }
    void Awake(){
        if(I == null){
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
        moveAction = InputSystem.actions.FindAction("Move");
        Interact = InputSystem.actions.FindAction("Interact");
        cancel = InputSystem.actions.FindAction("Cancel");
        index = 0;
        selectedColor.a = 255f;
        unselectedColor.a = 255f;
        for (int i = 0; i < battleMenuButtons.Count; i++)
        {
            if (battleMenuButtons[i] != null)
            {
                battleMenuButtons[i].color = (i == 0) ? selectedColor : unselectedColor;
            }
        }
        UpdateBattleMovementLock();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame && BattleCanvas != null)
        {
            BattleCanvas.SetActive(!BattleCanvas.activeSelf);
        }

        UpdateBattleMovementLock();

        if (BattleCanvas == null || !BattleCanvas.activeSelf)
        {
            return;
        }

        if (moveAction == null)
        {
            return;
        }

        if (TryGetBattleMove(out Vector2 moveDirection))
        {
            ApplyBattleMove(moveDirection);
        }

        AnimateButtons();

        if(Interact.WasPressedThisFrame()&&index == 0){
            Attack();
        }
        if(cancel.WasPressedThisFrame()&&subMenu){
            AttackBack();
        }
    }

    private bool TryGetBattleMove(out Vector2 moveDirection)
    {
        moveDirection = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                moveDirection = Vector2.up;
                return true;
            }

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                moveDirection = Vector2.down;
                return true;
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                moveDirection = Vector2.left;
                return true;
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                moveDirection = Vector2.right;
                return true;
            }
        }

        if (!moveAction.triggered)
        {
            return false;
        }

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude < 0.25f)
        {
            return false;
        }

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            moveDirection = input.x < 0 ? Vector2.left : Vector2.right;
        }
        else
        {
            moveDirection = input.y < 0 ? Vector2.down : Vector2.up;
        }

        return true;
    }

    private void ApplyBattleMove(Vector2 moveDirection)
    {
        if (moveDirection.y < 0)
        {
            if (index == 0)
            {
                index++;
            }
        }
        else if (moveDirection.y > 0)
        {
            if (index > 0)
            {
                index = 0;
            }
        }
        else if (moveDirection.x < 0)
        {
            index = 1;
        }
        else if (moveDirection.x > 0)
        {
            index = 2;
        }

        if (battleMenuButtons.Count > 0)
        {
            index = Mathf.Clamp(index, 0, battleMenuButtons.Count - 1);
        }
    }

    private void OnDisable()
    {
        ReleaseMovementLock();
    }

    private void OnDestroy()
    {
        ReleaseMovementLock();
    }

    private void UpdateBattleMovementLock()
    {
        bool battleActive = BattleCanvas != null && BattleCanvas.activeSelf;

        if (battleActive && !hasMovementLock && PlayerController.I != null)
        {
            PlayerController.I.LockMovement();
            hasMovementLock = true;
        }
        else if (!battleActive)
        {
            ReleaseMovementLock();
        }
    }

    private void ReleaseMovementLock()
    {
        if (!hasMovementLock)
        {
            return;
        }

        if (PlayerController.I != null)
        {
            PlayerController.I.UnlockMovement();
        }

        hasMovementLock = false;
    }

    private void AnimateButtons()
    {
        if (index < 0 || index >= battleMenuButtons.Count) return;

        for (int i = 0; i < battleMenuButtons.Count; i++)
        {
            Image buttonImage = battleMenuButtons[i];
            if (buttonImage == null) continue;
            Color targetColor;

            if (i == index)
            {
                targetColor = selectedColor;
            }
            else
            {
                targetColor = unselectedColor;
            }

            buttonImage.color = targetColor;
        }
    }
    public void Attack(){
        subMenu=true;
        submenuoptions.SetActive(true);
        battleChooser.SetActive(false);
    }
    public void AttackBack(){
        subMenu=false;
        submenuoptions.SetActive(false);
        battleChooser.SetActive(true);

    }
}
