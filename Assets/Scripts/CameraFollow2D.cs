using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Usually the player

    [Header("Position")]
    [Tooltip("Offset from the player in world space (X = left/right, Y = up/down).")]
    public Vector2 offset = new Vector2(0f, 1.5f);

    [Header("Smoothing")]
    [Tooltip("How strongly the camera follows the target each frame. 1 = instant, 0 = no follow.")]
    [Range(0f, 1f)]
    public float followLerp = 0.2f;

    [Header("Pixel Perfect")]
    [Tooltip("If true, camera snaps to the pixel grid to avoid jitter with pixel art.")]
    public bool usePixelPerfectSnapping = true;

    [Tooltip("Pixels-per-unit to snap to (match your sprites' PPU). 0 = no snapping.")]
    public int pixelsPerUnitOverride = 0;

    private void LateUpdate()
    {
        if (target == null) return;

        // Desired position: player + offset (keeping camera Z)
        Vector3 desiredPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        // Smoothly move toward the desired position (adds a small delay)
        Vector3 smoothedPos = Vector3.Lerp(transform.position, desiredPos, followLerp);

        // Optional: snap to pixel grid for crisp pixel art
        if (usePixelPerfectSnapping && pixelsPerUnitOverride > 0)
        {
            float unitsPerPixel = 1f / pixelsPerUnitOverride;
            smoothedPos.x = Mathf.Round(smoothedPos.x / unitsPerPixel) * unitsPerPixel;
            smoothedPos.y = Mathf.Round(smoothedPos.y / unitsPerPixel) * unitsPerPixel;
        }

        transform.position = smoothedPos;
    }
}

