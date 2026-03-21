using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GifEmotionAnimation", menuName = "Dialogue/Gif Emotion Animation") ]
public class GifEmotionAnimation : ScriptableObject
{
    [Tooltip("Drag the .gif asset here. If frames aren't baked yet, the SpeakerBox will decode it at runtime (editor/Play Mode) so you don't need a manual bake step.")]
    public Object gifSource;

    [Tooltip("Baked sprites (one per frame).")]
    public List<Sprite> frames = new List<Sprite>();

    [Tooltip("Delay per frame in seconds (same count/order as frames).")]
    public List<float> frameDelays = new List<float>();

    [Tooltip("If true, the animation loops continuously.")]
    public bool loop = true;

    public bool IsReady =>
        frames != null &&
        frameDelays != null &&
        frames.Count > 0 &&
        frames.Count == frameDelays.Count;

    public float GetDelay(int index)
    {
        if (frameDelays == null || index < 0 || index >= frameDelays.Count)
            return 0.1f;
        return frameDelays[index];
    }
}

