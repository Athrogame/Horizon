using UnityEngine;
using UnityEngine.InputSystem; // New Input System

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in units per second.")]
    public float moveSpeed = 5f;

    [Tooltip("How quickly the player accelerates toward the target speed.")]
    public float acceleration = 20f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    [Header("Input")]
    [Tooltip("Reference to the Move action from your Input Actions asset.")]
    public InputActionReference moveActionReference;

    private InputAction moveAction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // For a top-down controller we don't want gravity pulling the player down
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

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
