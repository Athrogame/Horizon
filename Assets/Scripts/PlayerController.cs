using UnityEngine;
using UnityEngine.InputSystem; // New Input System

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController I { get; private set; }



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
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
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

    /// <summary>
    /// Force the player to face a specific direction (used when spawning from doors).
    /// Direction will be snapped to the nearest 4-way cardinal and applied to the animator.
    /// If pulseMove is true, IsMoving will briefly be set to true to play a step, then turned off.
    /// </summary>
    public void SetFacingDirection(Vector2 direction, bool pulseMove = false, float pulseDuration = 0.1f)
    {
        if (direction == Vector2.zero)
            return;

        // Snap to 4-way cardinal
        Vector2 cardinal;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            cardinal = new Vector2(Mathf.Sign(direction.x), 0f);
        }
        else
        {
            cardinal = new Vector2(0f, Mathf.Sign(direction.y));
        }

        lastMoveX = cardinal.x;
        lastMoveY = cardinal.y;

        // Stop movement and update animator to idle in that direction
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            if (pulseMove)
            {
                // Briefly mark as moving to ensure the walk animation direction updates
                animator.SetBool(IsMoving, true);
                StartCoroutine(PulseIsMoving(pulseDuration));
            }
            else
            {
                animator.SetBool(IsMoving, false);
            }

            animator.SetFloat(MoveX, lastMoveX);
            animator.SetFloat(MoveY, lastMoveY);
        }
    }

    private System.Collections.IEnumerator PulseIsMoving(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (animator != null)
        {
            animator.SetBool(IsMoving, false);
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

        // Drive animations from the velocity physics *actually produced* last frame
        // (currentVelocity = rb.linearVelocity before we modify it this frame).
        // Because collision resolution runs after FixedUpdate, a wall will have
        // zeroed out the velocity by the time the next frame reads it here — so
        // IsMoving stays false when the player is blocked.
        if (animator != null)
        {
            // Use a small speed threshold so micro-jitter doesn't trigger IsMoving
            bool isMoving = currentVelocity.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                // Dominant axis of actual velocity decides facing direction
                if (Mathf.Abs(currentVelocity.x) >= Mathf.Abs(currentVelocity.y))
                {
                    lastMoveX = Mathf.Sign(currentVelocity.x);
                    lastMoveY = 0f;
                }
                else
                {
                    lastMoveX = 0f;
                    lastMoveY = Mathf.Sign(currentVelocity.y);
                }
            }
            // When idle, lastMoveX / lastMoveY stay as-is so the idle pose
            // matches the direction the player was last moving.

            animator.SetBool(IsMoving, isMoving);
            animator.SetFloat(MoveX, lastMoveX);
            animator.SetFloat(MoveY, lastMoveY);
        }
    }
}
