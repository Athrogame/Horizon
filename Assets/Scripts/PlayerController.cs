using UnityEngine;
using UnityEngine.InputSystem; // New Input System

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 20f;

    [Header("Input")]
    public InputActionReference moveActionReference;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private InputAction moveAction;

    // Animation parameter names (left = -1, right = 1; only horizontal for left/right idles)
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    // Last direction sent to animator (so idle keeps the same pose as when you stopped)
    private float lastMoveX = 0f;
    private float lastMoveY = -1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // For a top-down controller we don't want gravity pulling the player down
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // Smooth position between physics steps so the camera (LateUpdate) doesn't see jitter/teleporting
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Get the Move action from the reference
        if (moveActionReference != null)
        {
            moveAction = moveActionReference.action;

            // Listen to Move input
            moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            moveAction.canceled += ctx => moveInput = Vector2.zero;
        }
        else
        {
            Debug.LogError("PlayerController: moveActionReference is not assigned in the inspector.");
        }
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.Disable();
        }
    }

    private void FixedUpdate()
    {
        // Read raw input
        Vector2 input = moveInput;

        // Deadzone so tiny inputs don't move the player
        if (input.sqrMagnitude < 0.01f)
        {
            input = Vector2.zero;
        }

        // Lock to 4 directions (up, down, left, right)
        Vector2 cardinal = Vector2.zero;
        if (input != Vector2.zero)
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                // Horizontal movement
                cardinal = new Vector2(Mathf.Sign(input.x), 0f);
            }
            else
            {
                // Vertical movement
                cardinal = new Vector2(0f, Mathf.Sign(input.y));
            }
        }

        // Update animations: keep same MoveX/MoveY when you stop so idle matches last walk direction
        if (animator != null)
        {
            bool isMoving = cardinal != Vector2.zero;

            if (isMoving)
            {
                // While moving: set direction and remember it for when we stop
                if (cardinal.x != 0f)
                {
                    lastMoveX = cardinal.x;
                    lastMoveY = 0f;
                }
                else
                {
                    lastMoveX = 0f;
                    lastMoveY = cardinal.y;
                }
            }
            // When idle we keep lastMoveX and lastMoveY unchanged

            animator.SetBool(IsMoving, isMoving);
            animator.SetFloat(MoveX, lastMoveX);
            animator.SetFloat(MoveY, lastMoveY);
        }

        // Target velocity based on cardinal direction
        Vector2 targetVelocity = cardinal * moveSpeed;

        // Tween / smooth toward the target velocity
        Vector2 currentVelocity = rb.linearVelocity;
        Vector2 newVelocity = Vector2.Lerp(
            currentVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = newVelocity;
    }
}
