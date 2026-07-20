using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for what a save slot stores.
/// Serialized to JSON (one file per slot) by <see cref="SaveManager"/>.
///
/// To save "later things", just add a public field here — JsonUtility picks it up
/// automatically — and snapshot/restore it in SaveManager.SaveToSlot / the load coroutine.
/// </summary>
[System.Serializable]
public class GameData
{
    // false = empty slot (no save written yet).
    public bool used = false;

    // Where the player was.
    public string sceneName = "";
    public float playerX = 0f;
    public float playerY = 0f;

    // Which way the player was facing (defaults to "down", matching PlayerController).
    public float facingX = 0f;
    public float facingY = -1f;

    // How long this playthrough has lasted, in seconds.
    public float playTimeSeconds = 0f;

    // Mood as an index into SaveSlotUI.moodSprites (0 = first mood).
    public int moodIndex = 0;

    // Wall-clock stamp of the last save (useful for display / newest-first sorting).
    public string lastSavedIso = "";

    // Progress flags for this playthrough — e.g. "cutscene_intro" once the intro cutscene has
    // played. Anything that should persist per-save (seen cutscenes, one-off world changes,
    // opened doors, picked-up items) can be stored here as a string key.
    public List<string> flags = new List<string>();
}
