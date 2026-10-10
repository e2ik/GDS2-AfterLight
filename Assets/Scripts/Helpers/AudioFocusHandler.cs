using FMODUnity;
using UnityEngine;

public class AudioFocusHandler : MonoBehaviour
{
    [SerializeField] private bool webGLOnly = true;
    [SerializeField] private bool suspendOnFocusLost = true;
    [SerializeField] private bool suspendOnPause = true;

    private bool suspended;

    private void Awake()
    {
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (suspendOnFocusLost) SetSuspended(!hasFocus);
    }

    private void OnApplicationPause(bool paused)
    {
        if (suspendOnPause) SetSuspended(paused);
    }

    private void OnDestroy()
    {
        SetSuspended(false);
    }

    private bool IsActivePlatform()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return true;
#else
        return !webGLOnly;
#endif
    }

    private void SetSuspended(bool value)
    {
        if (suspended == value) return;
        if (value && !IsActivePlatform()) return;
        if (!RuntimeManager.IsInitialized) return;

        suspended = value;

        if (value)
        {
            RuntimeManager.PauseAllEvents(true);
            RuntimeManager.CoreSystem.mixerSuspend();
        }
        else
        {
            RuntimeManager.CoreSystem.mixerResume();
            RuntimeManager.PauseAllEvents(false);
        }
    }
}