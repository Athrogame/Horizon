using UnityEngine;
using System.Reflection;

public class UnityProbe : MonoBehaviour
{
    private void Start()
    {
        var type = System.Type.GetType("Unity.Cinemachine.CinemachineBasicMultiChannelPerlin, Unity.Cinemachine");
        if (type != null)
        {
            foreach(var p in type.GetProperties()) Debug.LogWarning("PROP: " + p.Name);
            foreach(var f in type.GetFields()) Debug.LogWarning("FIELD: " + f.Name);
        }
        else
        {
            Debug.LogWarning("Could not find Unity.Cinemachine.CinemachineBasicMultiChannelPerlin");
        }
    }
}
