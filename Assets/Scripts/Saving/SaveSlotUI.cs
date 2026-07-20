using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays one save slot: time played, scene name, and a mood image.
/// The mood is an int index into <see cref="moodSprites"/>, so setting the int swaps the picture.
///
/// Used both in the main-menu Saves panel (load) and the in-game save-point menu (save).
/// Call <see cref="Refresh"/> whenever the slot's data might have changed.
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [Header("Which slot this panel represents")]
    public int slotIndex = 0;

    [Header("UI references (auto-wired from children if left empty)")]
    [Tooltip("Shows the play time, e.g. 12:34.")]
    public TextMeshProUGUI timeText;
    [Tooltip("Shows the saved scene name.")]
    public TextMeshProUGUI sceneText;
    [Tooltip("Image whose sprite is chosen from Mood Sprites by the saved mood index.")]
    public Image moodImage;

    [Header("Mood sprites (index 0 = first mood, 1 = second, ...)")]
    public List<Sprite> moodSprites = new List<Sprite>();

    [Header("Empty-slot handling")]
    [Tooltip("Optional object shown only when the slot is empty (e.g. an 'Empty' label). All the normal time/scene/mood content is hidden for empty slots regardless.")]
    public GameObject emptyOverlay;

    private void Awake()
    {
        // Match the house style: auto-wire refs if the inspector left them blank.
        if (timeText == null || sceneText == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            if (timeText == null && texts.Length > 0) timeText = texts[0];
            if (sceneText == null && texts.Length > 1) sceneText = texts[1];
        }
        if (moodImage == null) moodImage = GetComponentInChildren<Image>(true);
    }

    /// <summary>Re-read this slot's save data from disk and update the display.</summary>
    public void Refresh()
    {
        if (SaveManager.I == null)
            return;

        GameData data = SaveManager.I.LoadSlotData(slotIndex);

        if (!data.used)
        {
            // Empty slot: hide every piece of slot content so nothing shows at all.
            if (timeText != null) timeText.gameObject.SetActive(false);
            if (sceneText != null) sceneText.gameObject.SetActive(false);
            if (moodImage != null) moodImage.gameObject.SetActive(false);
            if (emptyOverlay != null) emptyOverlay.SetActive(true);
            return;
        }

        // Occupied slot: make sure content is visible and filled in.
        if (emptyOverlay != null) emptyOverlay.SetActive(false);

        if (timeText != null)
        {
            timeText.gameObject.SetActive(true);
            timeText.text = SaveManager.FormatTime(data.playTimeSeconds);
        }
        if (sceneText != null)
        {
            sceneText.gameObject.SetActive(true);
            sceneText.text = data.sceneName;
        }
        if (moodImage != null)
        {
            bool hasSprite = moodSprites.Count > 0;
            if (hasSprite)
            {
                int idx = Mathf.Clamp(data.moodIndex, 0, moodSprites.Count - 1);
                moodImage.sprite = moodSprites[idx];
                hasSprite = moodImage.sprite != null;
            }
            moodImage.gameObject.SetActive(hasSprite);
        }
    }
}
