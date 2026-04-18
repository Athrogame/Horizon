using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMgr : MonoBehaviour
{
    [Header("Spawn Selection")]
    [Tooltip("Which DoorSpawnPoints index to use in the newly loaded scene.")]
    public int doorToSpawnAt;

    public static SceneMgr I { get; private set; }

    [Header("Transition Animation")]
    [Tooltip("Animator on the transition panel (e.g. full-screen fade). Assign the Panel that uses the Panel controller with 'end' trigger and 'New Animation' state.")]
    public Animator transitionAnim;
    [Tooltip("Time to wait for the fade-to-black before loading the scene (should match your 'end' clip length).")]
    public float fadeOutDuration = 0.25f;
    [Tooltip("How long to hold on black AFTER the scene loads, before fading in. Increase this to hide the player spawning.")]
    public float holdDuration = 0.1f;
    [Tooltip("How long the fade-in from black takes.")]
    public float fadeInDuration = 0.5f;

    // Panel.controller convention:
    // - trigger "end" => fade to black
    // - state "New Animation" => fade from black
    private static readonly int EndTrigger = Animator.StringToHash("end");
    private const float SpawnFacingPulseDuration = 0.1f;

    private void Awake()
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Entry point used by doors / gameplay scripts.
    /// Plays transition when available, otherwise loads immediately.
    /// </summary>
    public void LoadScene(int sceneIndex)
    {
        if (transitionAnim != null)
            StartCoroutine(LoadSceneWithTransition(sceneIndex));
        else
            DoLoadScene(sceneIndex);
    }
    private IEnumerator LoadSceneWithTransition(int sceneIndex)
    {
        transitionAnim.SetTrigger("end");
        yield return new WaitForSecondsRealtime(fadeOutDuration);

        DoLoadScene(sceneIndex);

        // Hold on black so the player spawns & repositions before anything is visible.
        yield return new WaitForSecondsRealtime(holdDuration);

        // Fade back in — play from the beginning at a speed scaled to fadeInDuration.
        // Speed = 1 means the clip plays at its authored length; scale accordingly.
        transitionAnim.Play("New Animation", 0, 0f);
    }

    private void DoLoadScene(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var spawnPoints = Object.FindAnyObjectByType<DoorSpawnPoints>();
        if (spawnPoints == null || PlayerController.I == null || spawnPoints.SpawnLocations.Count == 0)
            return;

        int index = Mathf.Clamp(doorToSpawnAt, 0, spawnPoints.SpawnLocations.Count - 1);

        // Spawn exactly on the chosen point for seamless transitions.
        PlayerController.I.transform.position = spawnPoints.SpawnLocations[index].position;

        TryApplySpawnFacing(spawnPoints, index);
    }

    private void TryApplySpawnFacing(DoorSpawnPoints spawnPoints, int index)
    {
        if (spawnPoints.SpawnDirections == null)
            return;

        if (spawnPoints.SpawnDirections.Count <= index)
        {
            if (spawnPoints.SpawnDirections.Count != spawnPoints.SpawnLocations.Count)
            {
                Debug.LogWarning(
                    $"SceneMgr: SpawnDirections count ({spawnPoints.SpawnDirections.Count}) " +
                    $"doesn't match SpawnLocations count ({spawnPoints.SpawnLocations.Count}). " +
                    $"Door index used: {index}."
                );
            }
            return;
        }

        Vector2 facingDir = spawnPoints.SpawnDirections[index];
        if (facingDir == Vector2.zero)
            return;

        // Apply after one physics step so velocity-driven animator state
        // isn't overwritten by spawn-time collision settling.
        StartCoroutine(ApplySpawnFacingAfterFixedUpdate(facingDir));
    }

    private IEnumerator ApplySpawnFacingAfterFixedUpdate(Vector2 facingDir)
    {
        // Wait for a single physics step in the newly loaded scene.
        yield return new WaitForFixedUpdate();

        if (PlayerController.I == null)
            yield break;

        // pulseMove=true nudges velocity-based animations to the requested direction.
        PlayerController.I.SetFacingDirection(facingDir, true, SpawnFacingPulseDuration);
    }
}
