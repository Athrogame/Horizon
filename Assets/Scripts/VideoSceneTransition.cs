using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Loads the next scene once the attached VideoPlayer finishes playing.
/// Put this on the same GameObject as a VideoPlayer (or assign one below).
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class VideoSceneTransition : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("The VideoPlayer to watch. Defaults to the one on this GameObject.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Next Scene")]
    [Tooltip("Name of the scene to load. Leave empty to use the build index below.")]
    [SerializeField] private string nextSceneName = "";

    [Tooltip("Build index of the scene to load. Used only when Next Scene Name is empty. -1 = load the next scene in build order.")]
    [SerializeField] private int nextSceneBuildIndex = -1;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        int indexToLoad = nextSceneBuildIndex >= 0
            ? nextSceneBuildIndex
            : SceneManager.GetActiveScene().buildIndex + 1;

        if (indexToLoad < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(indexToLoad);
        else
            Debug.LogWarning($"[VideoSceneTransition] No scene at build index {indexToLoad}. Add scenes to Build Settings.");
    }
}
