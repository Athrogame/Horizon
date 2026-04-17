#if UNITY_EDITOR
using UnityEngine;

public static class EditorPlayerPrefsReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ClearOnPlay()
    {
        PlayerPrefs.DeleteAll();
    }
}
#endif
