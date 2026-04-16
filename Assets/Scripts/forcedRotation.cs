using UnityEngine;

[ExecuteAlways]
public class forcedRotation : MonoBehaviour
{
    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
