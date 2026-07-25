using UnityEngine;

// Wipes PlayerPrefs whenever the running build is different from the one that
// last wrote to disk. Unity regenerates Application.buildGUID on every build,
// so each rebuild causes a one-time wipe; within a single shipped build the
// GUID is constant, so "play only once" cutscenes still gate correctly for
// real players. Editor runs are skipped (buildGUID is empty there) —
// EditorPlayerPrefsReset already handles the editor case.
//
// NOTE: wipes everything EXCEPT the Settings menu's keys (see
// GameSettings.WipeAllExceptSettings) — resolution and volume choices should
// outlive a rebuild. If you start storing progress in PlayerPrefs, narrow this
// further to only delete cutscene_* keys.
public static class BuildPrefsResetter
{
    private const string BuildIdKey = "__lastBuildGUID";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnNewBuild()
    {
        string current = Application.buildGUID;
        if (string.IsNullOrEmpty(current)) return;

        if (PlayerPrefs.GetString(BuildIdKey, "") == current) return;

        GameSettings.WipeAllExceptSettings();
        PlayerPrefs.SetString(BuildIdKey, current);
        PlayerPrefs.Save();
        Debug.Log($"[BuildPrefsResetter] New build detected ({current}). PlayerPrefs wiped.");
    }
}
