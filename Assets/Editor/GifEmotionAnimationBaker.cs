using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GifEmotionAnimation))]
public class GifEmotionAnimationBaker : Editor
{
    // User-selected cap from the dialog: 0.9 FPS
    private const float GifPlaybackFpsCap = 0.9f;
    private const int MaxFrames = 120;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("gifSource"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("loop"));

        EditorGUILayout.Space(8);

        var anim = (GifEmotionAnimation)target;
        EditorGUI.BeginDisabledGroup(anim.gifSource == null);
        if (GUILayout.Button("Bake GIF Frames", GUILayout.Height(30)))
        {
            Bake(anim);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Baked frames:", anim.frames != null ? anim.frames.Count.ToString() : "0");
        EditorGUILayout.LabelField("Ready:", anim.IsReady ? "Yes" : "No");

        serializedObject.ApplyModifiedProperties();
    }

    private void Bake(GifEmotionAnimation anim)
    {
        if (anim == null || anim.gifSource == null)
            return;

        string gifPath = AssetDatabase.GetAssetPath(anim.gifSource);
        if (string.IsNullOrEmpty(gifPath))
        {
            Debug.LogError($"GifEmotionAnimationBaker: Could not get asset path for {anim.gifSource.name}");
            return;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(gifPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"GifEmotionAnimationBaker: Failed to read gif bytes: {e.Message}");
            return;
        }

        // Clear existing subassets (frames/textures) so we don't accumulate stale frames
        string animPath = AssetDatabase.GetAssetPath(anim);
        if (!string.IsNullOrEmpty(animPath))
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(animPath);
            foreach (var obj in existing)
            {
                if (obj == null || obj == anim)
                    continue;

                try
                {
                    AssetDatabase.RemoveObjectFromAsset(obj);
                    DestroyImmediate(obj, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }

        var newFrames = new List<Sprite>();
        var newDelays = new List<float>();

        float minDelay = 1f / Mathf.Max(0.0001f, GifPlaybackFpsCap); // apply FPS cap by slowing down (min delay)

        // Vendored mgGif: Assets/ThirdParty/mgGif/mgGif.cs (MG.GIF.Decoder)
        using (var decoder = new MG.GIF.Decoder(data))
        {
            MG.GIF.Image img = decoder.NextImage();
            int frameIndex = 0;

            while (img != null && frameIndex < MaxFrames)
            {
                Texture2D tex = img.CreateTexture();
                if (tex == null)
                    break;

                tex.name = $"{anim.name}_frame_{frameIndex:000}";
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;

                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                sprite.name = $"{anim.name}_sprite_{frameIndex:000}";

                int delayMs = img.Delay;
                float delaySeconds = Mathf.Max(minDelay, delayMs / 1000f);

                newFrames.Add(sprite);
                newDelays.Add(delaySeconds);

                AssetDatabase.AddObjectToAsset(tex, anim);
                AssetDatabase.AddObjectToAsset(sprite, anim);

                frameIndex++;
                img = decoder.NextImage();
            }
        }

        anim.frames = newFrames;
        anim.frameDelays = newDelays;

        EditorUtility.SetDirty(anim);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"GifEmotionAnimationBaker: Baked {newFrames.Count} frames for {anim.name}");
    }
}

