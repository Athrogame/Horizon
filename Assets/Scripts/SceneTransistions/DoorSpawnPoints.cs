using System.Collections.Generic;
using UnityEngine;

public class DoorSpawnPoints : MonoBehaviour
{
    public List<Transform> SpawnLocations = new List<Transform>();
    
    // Optional facing directions for each spawn location.
    // If set, index should match the corresponding entry in SpawnLocations.
    public List<Vector2> SpawnDirections = new List<Vector2>();
}
