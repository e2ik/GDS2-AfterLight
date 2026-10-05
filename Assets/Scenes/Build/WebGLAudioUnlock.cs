using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class WebGLAudioUnlock : MonoBehaviour
{
    [SerializeField] private GameObject clickToStartPanel;
    [SerializeField] private bool pauseUntilUnlocked = true;
    [SerializeField] private bool loadNextSceneAfterUnlock = true;
    [SerializeField] private string nextSceneName;

    private bool unlocked;

#if UNITY_WEBGL && !UNITY_EDITOR
    private const bool IsWebGLBuild = true;
#else
    private const bool IsWebGLBuild = false;
#endif
    private float previousTimeScale = 1f;

    private void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (clickToStartPanel != null) clickToStartPanel.SetActive(true);
        if (pauseUntilUnlocked)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
#else
        if (clickToStartPanel != null) clickToStartPanel.SetActive(false);
        unlocked = true;
#endif
    }

    private void Start()
    {
        Debug.Log($"[WebGLAudioUnlock] Start. WebGL build: {IsWebGLBuild}, load next: {loadNextSceneAfterUnlock}, next scene name: '{nextSceneName}', active scene index: {SceneManager.GetActiveScene().buildIndex}, scenes in build: {SceneManager.sceneCountInBuildSettings}", this);
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        if (loadNextSceneAfterUnlock) LoadNextScene();
        enabled = false;
#endif
    }

    private void Update()
    {
        if (unlocked) return;
        if (!AnyPress()) return;

        Unlock();
    }

    private static bool AnyPress()
    {
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    private void Unlock()
    {
        unlocked = true;

        FMOD.Studio.System studio = FMODUnity.RuntimeManager.StudioSystem;
        if (studio.isValid())
        {
            FMODUnity.RuntimeManager.CoreSystem.mixerSuspend();
            FMODUnity.RuntimeManager.CoreSystem.mixerResume();
        }

        if (clickToStartPanel != null) clickToStartPanel.SetActive(false);
        if (pauseUntilUnlocked) Time.timeScale = previousTimeScale;

        enabled = false;

        if (loadNextSceneAfterUnlock) LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log($"[WebGLAudioUnlock] Loading scene '{nextSceneName}'", this);
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"[WebGLAudioUnlock] Loading build index {nextIndex}", this);
            SceneManager.LoadScene(nextIndex);
        }
        else
            Debug.LogWarning("[WebGLAudioUnlock] No next scene in Build Settings to load.", this);
    }
}