using UnityEngine;

/// <summary>
/// Everything the Settings menu can change, persisted in PlayerPrefs and applied on the spot.
///
/// Setting a property here BOTH saves it and makes it take effect, so the menu never has to
/// know how a setting is actually applied — it just assigns.
/// </summary>
public static class GameSettings
{
    // All keys share this prefix so the PlayerPrefs wipers can spare them (see WipeAllExceptSettings).
    private const string Prefix = "settings_";

    private const string ScaleKey = Prefix + "windowScale";
    private const string FullscreenKey = Prefix + "fullscreen";
    private const string MasterKey = Prefix + "volMaster";
    private const string MusicKey = Prefix + "volMusic";
    private const string SfxKey = Prefix + "volSfx";

    /// <summary>Volumes are whole steps 0–10, matching the arrow-key menu (no fractional slider).</summary>
    public const int MaxVolumeStep = 10;

    private static readonly string[] AllKeys = { ScaleKey, FullscreenKey, MasterKey, MusicKey, SfxKey };

    // ---------------------------------------------------------------- video

    /// <summary>Integer window scale multiplier, 1–4. Applied immediately when windowed.</summary>
    public static int WindowScale
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(ScaleKey, 1), 1, 4);
        set
        {
            int scale = Mathf.Clamp(value, 1, 4);
            PlayerPrefs.SetInt(ScaleKey, scale);
            PlayerPrefs.Save();
            if (ResolutionManager.Instance != null)
                ResolutionManager.Instance.SetWindowScale(scale);
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (ResolutionManager.Instance == null) return;
            if (value) ResolutionManager.Instance.ApplyFullscreen();
            else       ResolutionManager.Instance.ApplyWindowedScale(WindowScale);
        }
    }

    // ---------------------------------------------------------------- audio

    /// <summary>0–10. Drives AudioListener.volume, so it affects everything at once.</summary>
    public static int MasterVolume
    {
        get => ReadVolume(MasterKey);
        set
        {
            WriteVolume(MasterKey, value);
            AudioListener.volume = MasterVolume / (float)MaxVolumeStep;
        }
    }

    /// <summary>0–10. Read by music AudioSources as a multiplier — nothing applies it globally.</summary>
    public static int MusicVolume
    {
        get => ReadVolume(MusicKey);
        set => WriteVolume(MusicKey, value);
    }

    /// <summary>0–10. Read by SFX AudioSources as a multiplier — nothing applies it globally.</summary>
    public static int SfxVolume
    {
        get => ReadVolume(SfxKey);
        set => WriteVolume(SfxKey, value);
    }

    /// <summary>MusicVolume as a 0–1 multiplier, for setting an AudioSource's volume.</summary>
    public static float MusicScalar => MusicVolume / (float)MaxVolumeStep;

    /// <summary>SfxVolume as a 0–1 multiplier, for setting an AudioSource's volume.</summary>
    public static float SfxScalar => SfxVolume / (float)MaxVolumeStep;

    private static int ReadVolume(string key) => Mathf.Clamp(PlayerPrefs.GetInt(key, MaxVolumeStep), 0, MaxVolumeStep);

    private static void WriteVolume(string key, int steps)
    {
        PlayerPrefs.SetInt(key, Mathf.Clamp(steps, 0, MaxVolumeStep));
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- startup

    /// <summary>True once the player has picked a scale or fullscreen mode in the Settings menu.</summary>
    public static bool HasSavedVideo => PlayerPrefs.HasKey(ScaleKey) || PlayerPrefs.HasKey(FullscreenKey);

    /// <summary>Pushes the saved volumes onto the live game. Safe to call before anything else exists.</summary>
    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume / (float)MaxVolumeStep;
    }

    /// <summary>
    /// Pushes the saved window scale / fullscreen mode onto the live game. Called by
    /// ResolutionManager.Start so the player's last choice wins over ResolutionConfig's defaults.
    /// </summary>
    public static void ApplyVideo()
    {
        if (ResolutionManager.Instance == null) return;
        if (Fullscreen) ResolutionManager.Instance.ApplyFullscreen();
        else            ResolutionManager.Instance.ApplyWindowedScale(WindowScale);
    }

    // ---------------------------------------------------------------- persistence helpers

    /// <summary>
    /// Saves a setting WITHOUT applying it — used by ResolutionManager's debug keybinds (F11, 1–4)
    /// so they stay in sync with the menu without re-triggering the change they just made.
    /// </summary>
    public static void RecordWindowScale(int scale)
    {
        PlayerPrefs.SetInt(ScaleKey, Mathf.Clamp(scale, 1, 4));
        PlayerPrefs.Save();
    }

    /// <summary>Saves the fullscreen flag without applying it. See <see cref="RecordWindowScale"/>.</summary>
    public static void RecordFullscreen(bool on)
    {
        PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// PlayerPrefs.DeleteAll, but the player's settings survive. The prefs wipers use this so a
    /// rebuild / new editor session clears cutscene flags without resetting the options menu.
    /// </summary>
    public static void WipeAllExceptSettings()
    {
        var kept = new System.Collections.Generic.Dictionary<string, int>();
        foreach (string key in AllKeys)
            if (PlayerPrefs.HasKey(key)) kept[key] = PlayerPrefs.GetInt(key);

        PlayerPrefs.DeleteAll();

        foreach (var pair in kept)
            PlayerPrefs.SetInt(pair.Key, pair.Value);
        PlayerPrefs.Save();
    }
}
