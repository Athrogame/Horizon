using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class doorTransition : MonoBehaviour
{
    [Tooltip("Assign Player/Interact from InputSystem_Actions. Falls back to Space/Z/Enter if empty.")]
    public InputActionReference advanceAction;

    private InputAction advanceInput;
    private bool playerInside = false;
    private bool isLoading = false;
    private bool ownsFallbackAction = false;

    public int sceneToLoad; 
    public int doorToLoad;

    void Awake()
    {
        if (advanceAction != null)
        {
            advanceInput = advanceAction.action;
        }
        else
        {
            advanceInput = new InputAction(type: InputActionType.Button);
            ownsFallbackAction = true;
            advanceInput.AddBinding("<Keyboard>/space");
            advanceInput.AddBinding("<Keyboard>/z");
            advanceInput.AddBinding("<Keyboard>/enter");
        }
    }

    void OnEnable()
    {
        if (advanceInput == null)
            return;

        advanceInput.started += OnInteract;
        advanceInput.Enable();
    }

    void OnDisable()
    {
        if (advanceInput != null)
            advanceInput.started -= OnInteract;

        if (ownsFallbackAction)
            advanceInput.Disable();
    }

    void OnDestroy()
    {
        if (ownsFallbackAction)
            advanceInput.Dispose();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            playerInside = true;
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            playerInside = false;
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (!playerInside || isLoading)
            return;

        isLoading = true;

        if (SceneMgr.I != null)
        {
            SceneMgr.I.doorToSpawnAt = doorToLoad;
            SceneMgr.I.LoadScene(sceneToLoad);
            return;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
