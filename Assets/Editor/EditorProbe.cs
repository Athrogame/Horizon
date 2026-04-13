using UnityEngine;
using UnityEditor;
using System.Reflection;

public class EditorProbe
{
    [MenuItem("Tools/Probe Cinemachine")]
    public static void Probe()
    {
        var type = System.Type.GetType("Unity.Cinemachine.CinemachineBasicMultiChannelPerlin, Unity.Cinemachine");
        if (type != null)
        {
            foreach(var p in type.GetProperties()) Debug.LogWarning("PROP: " + p.Name);
            foreach(var f in type.GetFields()) Debug.LogWarning("FIELD: " + f.Name);
            Debug.LogWarning("DONE PROBING!");
        }
        else
        {
            Debug.LogWarning("Could not find Unity.Cinemachine.CinemachineBasicMultiChannelPerlin");
        }
    }
}
