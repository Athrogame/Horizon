using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to a Screen Space - Camera Canvas. The MainCamera in this project is a CHILD
// of the DontDestroyOnLoad'd Player ("Horizon"). On scene reload, the duplicate
// scene-instance Player + child Camera are destroyed in Awake, so the canvas's
// serialized worldCamera reference ends up pointing at the dead duplicate — and
// Unity AUTO-DOWNGRADES the canvas's renderMode from ScreenSpaceCamera to
// ScreenSpaceOverlay as a fallback. We rebind to the persistent MainCamera and
// force the canvas back to ScreenSpaceCamera.
[RequireComponent(typeof(Canvas))]
public class CanvasCameraBinder : MonoBehaviour
{
    [Tooltip("Plane distance to apply when restoring ScreenSpaceCamera mode. Match what the canvas was authored with.")]
    public float planeDistance = 100f;

    private Canvas canvas;
    private Coroutine rebindRoutine;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartRebind();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isActiveAndEnabled) return;
        StartRebind();
    }

    private void StartRebind()
    {
        if (rebindRoutine != null) StopCoroutine(rebindRoutine);
        rebindRoutine = StartCoroutine(RebindLoop());
    }

    // Retry every frame for ~1 second. The persistent PlayerController.I may not have
    // its child camera resolvable on the exact frame a scene finishes loading because
    // the duplicate scene-instance Player is still pending end-of-frame Destroy.
    private IEnumerator RebindLoop()
    {
        const int maxFrames = 60;
        for (int i = 0; i < maxFrames; i++)
        {
            yield return null;
            if (TryBind()) yield break;
        }
        Debug.LogWarning("[CanvasCameraBinder] Gave up after 60 frames — no usable Camera found.", this);
        rebindRoutine = null;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        var cam = canvas.worldCamera;
        if (canvas.renderMode != RenderMode.ScreenSpaceCamera
            || cam == null
            || !cam.gameObject.activeInHierarchy
            || !cam.CompareTag("MainCamera"))
        {
            TryBind();
        }
    }

    private bool TryBind()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();

        Camera persistent = FindPersistentMainCamera();
        if (persistent == null) return false;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = persistent;
        canvas.planeDistance = planeDistance;
        return true;
    }

    private static Camera FindPersistentMainCamera()
    {
        if (PlayerController.I != null)
        {
            var cam = PlayerController.I.GetComponentInChildren<Camera>(includeInactive: false);
            if (cam != null && cam.CompareTag("MainCamera"))
                return cam;
        }

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cam.CompareTag("MainCamera"))
                return cam;
        }
        return null;
    }
}
