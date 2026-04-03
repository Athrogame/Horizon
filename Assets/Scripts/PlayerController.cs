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

    [Header("Spawn Facing Pulse (for velocity-driven anims)")]
    [Tooltip("When SetFacingDirection(..., pulseMove:true) is used, we give the player a tiny velocity in the facing direction so velocity-based animations update immediately.")]
    public float spawnFacingPulseVelocity = 0.25f;

    // Animator parameter hashes (avoids repeated string lookups)
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private const float MoveDeadzone = 0.01f;

    // Last non-zero cardinal direction sent to animator (used for idle facing)
    private float lastMoveX = 0f;
    private float lastMoveY = -1f;

    public bool movementLocked = false;

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

        // Resolve input action from inspector reference.
        if (moveActionReference != null)
        {
            moveAction = moveActionReference.action;

            // Cache latest stick/keyboard vector each time input changes.
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
    /// Direction is snapped to 4-way cardinal and animator parameters are updated.
    /// pulseMove applies a tiny velocity to drive velocity-based animation state.
    /// </summary>
    public void SetFacingDirection(Vector2 direction, bool pulseMove = false, float pulseDuration = 0.1f)
    {
        if (direction == Vector2.zero)
            return;

        Vector2 cardinal = ToCardinal(direction);

        lastMoveX = cardinal.x;
        lastMoveY = cardinal.y;

        // Stop movement / apply a tiny facing impulse, then update animator to that idle pose.
        if (rb != null)
        {
            if (pulseMove)
            {
                // IMPORTANT: animator + IsMoving are driven by actual velocity in FixedUpdate.
                // So we need a small velocity pulse so the next physics tick updates facing correctly.
                rb.linearVelocity = cardinal * spawnFacingPulseVelocity;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
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
            // Avoid fighting FixedUpdate's velocity-driven animator state.
            // Only force IsMoving false if the rigidbody is basically stopped.
            if (rb == null || rb.linearVelocity.sqrMagnitude < MoveDeadzone)
            {
                animator.SetBool(IsMoving, false);
            }
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
        if (movementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 input = moveInput;
        if (input.sqrMagnitude < MoveDeadzone)
            input = Vector2.zero;

        Vector2 cardinal = input == Vector2.zero ? Vector2.zero : ToCardinal(input);

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

        // Drive animations from the velocity that physics produced on the previous step.
        // This keeps animation honest when collisions block movement.
        if (animator != null)
        {
            bool isMoving = currentVelocity.sqrMagnitude > MoveDeadzone;

            if (isMoving)
            {
                Vector2 facing = ToCardinal(currentVelocity);
                lastMoveX = facing.x;
                lastMoveY = facing.y;
            }
            // When idle, keep previous direction for correct idle pose.

            animator.SetBool(IsMoving, isMoving);
            animator.SetFloat(MoveX, lastMoveX);
            animator.SetFloat(MoveY, lastMoveY);
        }
    }

    /// <summary>
    /// Snap any vector to nearest four-way cardinal direction.
    /// </summary>
    private static Vector2 ToCardinal(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return new Vector2(Mathf.Sign(direction.x), 0f);

        return new Vector2(0f, Mathf.Sign(direction.y));
    }

    public void ForceIdle()
    {
        if (animator != null)
        {
            animator.SetBool(IsMoving, false);
            animator.SetFloat(MoveX, lastMoveX);
            animator.SetFloat(MoveY, lastMoveY);
        }
    }
}
