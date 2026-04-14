using UnityEngine;

/// <summary>
/// Snaps this GameObject's position to the nearest pixel boundary every LateUpdate.
/// Attach to any sprite that shows sub-pixel jitter (player, NPCs, items, etc.)
///
/// HOW IT FIXES JITTER:
/// Unity physics (Rigidbody2D) operates in float-precision world space.
/// Even one frame of movement can land a sprite at e.g. (1.0312499, 0.9999998),
/// which the GPU renders at a fractional pixel offset.  Pixel Perfect Camera
/// handles the camera's own alignment but cannot retroactively snap child objects.
/// This script forces every position to a multiple of (1 / pixelsPerUnit) so the
/// rendered pixel always corresponds to an exact texel on the sprite sheet.
/// </summary>
[DefaultExecutionOrder(100)] // Run after Cinemachine brain (order 50) so snapping is final
public class PixelSnap : MonoBehaviour
{
    [Tooltip("Must match the Pixels Per Unit on your sprite import settings and Pixel Perfect Camera.")]
    public int pixelsPerUnit = 16;

    // Cached for performance — avoid division every frame
    private float _pixelSize;

    // If a Rigidbody2D is present we snap its internal position too,
    // keeping physics in sync and preventing the rigidbody from fighting
    // back to the un-snapped position on the next FixedUpdate.
    private Rigidbody2D _rb;

    private void Awake()
    {
        _pixelSize = 1f / pixelsPerUnit;
        _rb = GetComponent<Rigidbody2D>();
    }

    private void LateUpdate()
    {
        SnapToPixelGrid();
    }

    /// <summary>
    /// Rounds position to the nearest pixel boundary.
    /// Call this yourself if you need to snap at a specific moment
    /// (e.g. immediately after a teleport/warp).
    /// </summary>
    public void SnapToPixelGrid()
    {
        Vector3 pos = transform.position;

        // Round each axis independently — keeps Z unchanged (important for
        // sorting layers; don't snap Z even in 2D projects).
        pos.x = Mathf.Round(pos.x / _pixelSize) * _pixelSize;
        pos.y = Mathf.Round(pos.y / _pixelSize) * _pixelSize;

        transform.position = pos;

        // Sync the rigidbody's internal position so the physics engine doesn't
        // "correct" back to the pre-snap float position on the next FixedUpdate.
        // This is a direct assignment (teleport), not MovePosition, so it takes
        // effect immediately without queuing for the next physics step.
        if (_rb != null)
            _rb.position = new Vector2(pos.x, pos.y);
    }
}
