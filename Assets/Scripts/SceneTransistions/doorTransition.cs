using UnityEngine;
using UnityEngine.SceneManagement;

public class doorTransition : MonoBehaviour
{
    public int sceneToLoad; // The build index of the scene to load when the player enters the trigger
    public int doorToLoad;
    void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            SceneMgr.I.doorToSpawnAt = doorToLoad;
            SceneMgr.I.LoadScene(sceneToLoad);
        }
    }
}
