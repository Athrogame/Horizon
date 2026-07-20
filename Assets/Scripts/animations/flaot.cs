using UnityEngine;

/// <summary>
/// Gives whatever object this is attached to a gentle "floating in the air" feel:
/// a soft up/down bob, an optional side-to-side sway, and a slight rocking rotation.
/// All motion is relative to wherever the object starts, so it always drifts around
/// its original spot instead of wandering off.
/// </summary>
public class flaot : MonoBehaviour
{
    [Header("Bob (up / down)")]
    [Tooltip("How far it drifts up and down, in world units.")]
    public float bobHeight = 0.15f;
    [Tooltip("How fast it bobs up and down.")]
    public float bobSpeed = 1.5f;

    [Header("Sway (left / right)")]
    [Tooltip("How far it drifts side to side, in world units. Set to 0 to disable.")]
    public float swayDistance = 0.05f;
    [Tooltip("How fast it sways side to side.")]
    public float swaySpeed = 0.8f;

    [Header("Rocking (tilt)")]
    [Tooltip("How many degrees it gently rocks back and forth. Set to 0 to disable.")]
    public float rockAngle = 3f;
    [Tooltip("How fast it rocks back and forth.")]
    public float rockSpeed = 1f;

    [Tooltip("Randomize the starting phase so multiple floating objects don't move in sync.")]
    public bool randomizeStart = true;

    // The object's resting position/rotation that everything animates around.
    private Vector3 startPos;
    private Quaternion startRot;

    // Per-axis phase offsets so the motions feel independent, not robotic.
    private float bobPhase;
    private float swayPhase;
    private float rockPhase;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;

        if (randomizeStart)
        {
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            swayPhase = Random.Range(0f, Mathf.PI * 2f);
            rockPhase = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        float t = Time.time;

        // Vertical bob and horizontal sway, offset from the resting position.
        float bob = Mathf.Sin(t * bobSpeed + bobPhase) * bobHeight;
        float sway = Mathf.Sin(t * swaySpeed + swayPhase) * swayDistance;
        transform.localPosition = startPos + new Vector3(sway, bob, 0f);

        // Gentle rocking tilt around the resting rotation (Z for 2D-style rocking).
        float rock = Mathf.Sin(t * rockSpeed + rockPhase) * rockAngle;
        transform.localRotation = startRot * Quaternion.Euler(0f, 0f, rock);
    }
}
