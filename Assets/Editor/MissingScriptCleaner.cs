using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class MissingScriptCleaner
{
    [MenuItem("Tools/Find Missing Scripts in Scene")]
    static void FindMissingScripts()
    {
        var found = new List<string>();

        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            // Skip assets — only look at scene objects.
            if (!go.scene.IsValid()) continue;

            Component[] components = go.GetComponents<Component>();
            foreach (Component c in components)
            {
                if (c == null)
                {
                    found.Add(GetPath(go));
                    break;
                }
            }
        }

        if (found.Count == 0)
        {
            Debug.Log("[MissingScriptCleaner] No missing scripts found.");
            return;
        }

        Debug.LogWarning($"[MissingScriptCleaner] Found {found.Count} GameObject(s) with missing scripts:");
        foreach (string path in found)
            Debug.LogWarning("  " + path);

        Debug.LogWarning("Run Tools > Remove Missing Scripts in Scene to clean them up.");
    }

    [MenuItem("Tools/Remove Missing Scripts in Scene")]
    static void RemoveMissingScripts()
    {
        int removed = 0;

        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid()) continue;

            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (count > 0)
            {
                Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                removed += count;
                Debug.Log($"[MissingScriptCleaner] Removed {count} missing script(s) from: {GetPath(go)}");
            }
        }

        if (removed == 0)
            Debug.Log("[MissingScriptCleaner] Nothing to remove — scene is clean.");
        else
            Debug.Log($"[MissingScriptCleaner] Done. Removed {removed} missing script component(s). Save the scene to keep this change.");
    }

    static string GetPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return go.scene.name + ": " + path;
    }
}
