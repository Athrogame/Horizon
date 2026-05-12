#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

// Wipes PlayerPrefs at the start of every editor Play session AND every
// development build. Release builds are unaffected so "Play Only Once"
// cutscenes still gate correctly for shipped players. On macOS editor and
// build share the same PlayerPrefs file, so without this dev builds inherit
// stale flags from the last editor run.
public static class EditorPlayerPrefsReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ClearOnPlay()
    {
        PlayerPrefs.DeleteAll();
    }
}
#endif
