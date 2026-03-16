using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMgr : MonoBehaviour
{
    public int doorToSpawnAt; // The index of the door spawn point to use when the new scene loads
    public static SceneMgr I { get; private set; }

    [Header("Transition Animation")]
    [Tooltip("Animator on the transition panel (e.g. full-screen fade). Assign the Panel that uses the Panel controller with 'end' trigger and 'New Animation' state.")]
    public Animator transitionAnim;
    [Tooltip("Time to wait for the fade-to-black before loading the scene (should match your 'end' clip length).")]
    public float fadeOutDuration = 0.25f;

    // Panel.controller: trigger "end" = fade to black, state "New Animation" = fade from black
    private static readonly int EndTrigger = Animator.StringToHash("end");

    void Awake()
    {
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void LoadScene(int sceneIndex)
    {
        if (transitionAnim != null)
            StartCoroutine(LoadSceneWithTransition(sceneIndex));
        else
            DoLoadScene(sceneIndex);
    }

    private IEnumerator LoadSceneWithTransition(int sceneIndex)
    {
        transitionAnim.SetTrigger(EndTrigger);
        yield return new WaitForSecondsRealtime(fadeOutDuration);

        DoLoadScene(sceneIndex);

        // Fade back out (panel goes from black to transparent)
        transitionAnim.Play("New Animation", 0, 0f);
    }

    private void DoLoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
        var spawnPoints = FindFirstObjectByType<DoorSpawnPoints>();
        if (spawnPoints != null && PlayerController.I != null)
            PlayerController.I.transform.position = spawnPoints.SpawnLocations[doorToSpawnAt].position;
    }
}
