using UnityEngine;
using UnityEngine.InputSystem; // New Input System

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PixelSnap))]  // Enforce pixel-snapping on the same GameObject
public class PlayerController : MonoBehaviour
{
    public static PlayerController I { get; private set; }

    public Vector2 FacingDirection => new Vector2(lastMoveX, lastMoveY);

    [Header("Movement")]
    public float moveSpeed = 5f;
    // 'acceleration' removed — instant velocity response is intentional.
    // Lerp-based acceleration moves the player to non-pixel-aligned positions
    // every frame, which is the primary cause of sub-pixel jitter.

    [Header("Input")]
    public InputActionReference moveActionReference;

    private Rigidbody2D rb;
    private Animator animator;
    private PixelSnap pixelSnap;
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

    // Reference-counted movement lock.
    // Call LockMovement() to lock and UnlockMovement() to release.
    // Movement is only re-enabled when every lock has been released,
    // so overlapping cutscenes can't accidentally unlock each other early.
    private int _movementLockCount = 0;
    public bool movementLocked => _movementLockCount > 0;

    // Legacy direct set — kept so existing Inspector references don't break.
    // Prefer LockMovement / UnlockMovement from scripts.
    public void LockMovement()        => _movementLockCount++;
    public void UnlockMovement()      => _movementLockCount = Mathf.Max(0, _movementLockCount - 1);
    public void ForceUnlockMovement() => _movementLockCount = 0;

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
        pixelSnap = GetComponent<PixelSnap>();

        // For a top-down controller we don't want gravity pulling the player down
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // IMPORTANT: Interpolation is DISABLED on purpose.
        // Rigidbody interpolation blends positions between physics steps using floats,
        // which produces sub-pixel positions that the Pixel Perfect Camera cannot correct.
        // PixelSnap.LateUpdate() handles alignment instead — no interpolation needed.
        rb.interpolation = RigidbodyInterpolation2D.None;

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

        // Always stop movement when forcing a facing direction (e.g. spawning from a door).
        // The old pulse velocity approach applied a fractional velocity that could land the
        // player on a sub-pixel boundary.  Animator facing is now driven directly from
        // lastMoveX/Y so no velocity pulse is needed to update the idle direction frame.
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // Snap to pixel grid after any teleport/warp so the first rendered frame is clean.
        if (pixelSnap != null)
            pixelSnap.SnapToPixelGrid();

        if (animator != null)
        {
            if (pulseMove)
            {
                // Briefly mark as moving so the walk-direction frame shows before returning to idle.
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
            // Flush any input the player pressed during the cutscene so it
            // doesn't carry over the moment the lock is lifted.
            moveInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
            ForceIdle();
            return;
        }

        // Read velocity BEFORE reassigning — at the top of FixedUpdate, rb.linearVelocity
        // reflects what physics resolved on the previous step (zero when blocked by a wall),
        // not the requested velocity we wrote ourselves.
        Vector2 actualVelocity = rb.linearVelocity;

        Vector2 input = moveInput;
        if (input.sqrMagnitude < MoveDeadzone)
            input = Vector2.zero;

        Vector2 cardinal = input == Vector2.zero ? Vector2.zero : ToCardinal(input);

        rb.linearVelocity = cardinal * moveSpeed;

        if (animator != null)
        {
            bool isMoving = actualVelocity.sqrMagnitude > MoveDeadzone * MoveDeadzone;

            // Facing is driven by input so the idle pose still faces whichever way
            // the player is pressing into a wall.
            if (cardinal != Vector2.zero)
            {
                lastMoveX = cardinal.x;
                lastMoveY = cardinal.y;
            }

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
