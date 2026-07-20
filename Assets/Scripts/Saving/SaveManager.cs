using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central save/load authority. Persistent singleton (survives scene loads), mirroring
/// the PlayerController / SceneMgr pattern.
///
/// Saves are written as JSON files under Application.persistentDataPath — deliberately NOT
/// PlayerPrefs, because BuildPrefsResetter / EditorPlayerPrefsReset call PlayerPrefs.DeleteAll().
///
/// Runtime state (play time, current mood) lives here during gameplay; SaveToSlot snapshots
/// it, LoadGame restores it.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager I { get; private set; }

    [Header("Config")]
    [Tooltip("Number of save slots. UI (main menu / save point) should match this.")]
    public int slotCount = 3;

    [Tooltip("Name of the main menu scene. Play time does not accumulate while this scene is active.")]
    public string mainMenuSceneName = "Main Menu";

    // --- Runtime state for the current playthrough ---
    private float playTimeSeconds = 0f;
    private int currentMoodIndex = 0;

    // Progress flags for this playthrough (seen cutscenes, one-off world changes, etc.).
    private readonly System.Collections.Generic.HashSet<string> flags = new System.Collections.Generic.HashSet<string>();

    // When a load is requested we stash the data and apply it after the scene finishes loading.
    private GameData pendingRestore = null;

    public int CurrentMood => currentMoodIndex;

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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        // Only count time while actually playing, not while sitting in the main menu.
        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
            playTimeSeconds += Time.deltaTime;
    }

    // ------------------------------------------------------------------ Mood API

    /// <summary>Set the player's current mood (index into the mood sprite list). Call from story/events.</summary>
    public void SetMood(int index)
    {
        currentMoodIndex = Mathf.Max(0, index);
    }

    // ------------------------------------------------------------------ Progress flags API

    /// <summary>Mark a progress flag (e.g. "cutscene_intro"). Persisted with the next save.</summary>
    public void SetFlag(string key)
    {
        if (!string.IsNullOrEmpty(key)) flags.Add(key);
    }

    /// <summary>Clear a progress flag.</summary>
    public void ClearFlag(string key)
    {
        if (!string.IsNullOrEmpty(key)) flags.Remove(key);
    }

    /// <summary>True if the flag has been set this playthrough.</summary>
    public bool HasFlag(string key)
    {
        return !string.IsNullOrEmpty(key) && flags.Contains(key);
    }

    // ------------------------------------------------------------------ Save / Load API

    /// <summary>Reset runtime state for a brand-new game (used by the Play button).</summary>
    public void NewGame()
    {
        playTimeSeconds = 0f;
        currentMoodIndex = 0;
        flags.Clear();
        pendingRestore = null;
    }

    /// <summary>Snapshot the current game state into the given slot and write it to disk.</summary>
    public void SaveToSlot(int slot)
    {
        GameData data = new GameData
        {
            used = true,
            sceneName = SceneManager.GetActiveScene().name,
            playTimeSeconds = playTimeSeconds,
            moodIndex = currentMoodIndex,
            lastSavedIso = System.DateTime.Now.ToString("s"),
            flags = new System.Collections.Generic.List<string>(flags),
        };

        if (PlayerController.I != null)
        {
            Vector3 pos = PlayerController.I.transform.position;
            data.playerX = pos.x;
            data.playerY = pos.y;

            Vector2 facing = PlayerController.I.FacingDirection;
            data.facingX = facing.x;
            data.facingY = facing.y;
        }

        try
        {
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
            Debug.Log($"SaveManager: saved slot {slot} -> {SlotPath(slot)}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager: failed to write slot {slot}: {e.Message}");
        }
    }

    /// <summary>
    /// Read a slot's data for display (slot UI). Never returns null — an empty/missing/corrupt
    /// slot comes back as a fresh GameData with used = false.
    /// </summary>
    public GameData LoadSlotData(int slot)
    {
        string path = SlotPath(slot);
        if (!File.Exists(path))
            return new GameData();

        try
        {
            GameData data = JsonUtility.FromJson<GameData>(File.ReadAllText(path));
            return data ?? new GameData();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager: failed to read slot {slot}: {e.Message}");
            return new GameData();
        }
    }

    /// <summary>True if the slot holds a real save.</summary>
    public bool HasSave(int slot)
    {
        GameData data = LoadSlotData(slot);
        return data.used;
    }

    /// <summary>
    /// Load a slot: restore runtime state, load its scene, and reposition the player once the
    /// scene is ready. Does nothing for an empty slot.
    /// </summary>
    public void LoadGame(int slot)
    {
        GameData data = LoadSlotData(slot);
        if (!data.used)
        {
            Debug.LogWarning($"SaveManager: LoadGame called on empty slot {slot}.");
            return;
        }

        playTimeSeconds = data.playTimeSeconds;
        currentMoodIndex = data.moodIndex;
        flags.Clear();
        if (data.flags != null)
            foreach (string f in data.flags) flags.Add(f);
        pendingRestore = data;

        // Stop SceneMgr from teleporting the player to a door spawn — the saved position wins.
        SceneMgr.SuppressAutoSpawn = true;

        SceneManager.LoadScene(data.sceneName);
    }

    // ------------------------------------------------------------------ Restore after load

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingRestore == null)
            return;

        StartCoroutine(ApplyRestore(pendingRestore));
        pendingRestore = null;
    }

    private IEnumerator ApplyRestore(GameData data)
    {
        // Wait long enough to land AFTER SceneMgr's own reposition (synchronous in OnSceneLoaded)
        // and its facing pulse (applied after a FixedUpdate), so the saved transform wins.
        yield return null;
        yield return null;
        yield return new WaitForFixedUpdate();

        if (PlayerController.I == null)
        {
            Debug.LogWarning("SaveManager: no PlayerController to restore into after load.");
            yield break;
        }

        PlayerController.I.transform.position = new Vector3(data.playerX, data.playerY, PlayerController.I.transform.position.z);

        Vector2 facing = new Vector2(data.facingX, data.facingY);
        if (facing != Vector2.zero)
            PlayerController.I.SetFacingDirection(facing);
    }

    // ------------------------------------------------------------------ Helpers

    private string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

    /// <summary>Format seconds as H:MM:SS (or MM:SS under an hour) for slot UI.</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }
}
