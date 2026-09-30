using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public static class AudioManager
{
    public static void PlaySFX(EventReference sfxEvent, Object context = null)
    {
        if (sfxEvent.IsNull)
        {
            LogMissingEvent(context);
            return;
        }

        RuntimeManager.PlayOneShot(sfxEvent);
    }

    public static void PlaySFX(EventReference sfxEvent, Vector3 position, Object context = null)
    {
        if (sfxEvent.IsNull)
        {
            LogMissingEvent(context);
            return;
        }

        RuntimeManager.PlayOneShot(sfxEvent, position);
    }

    public static void PlaySFXAttached(EventReference sfxEvent, GameObject target, Object context = null)
    {
        if (sfxEvent.IsNull)
        {
            LogMissingEvent(context);
            return;
        }

        if (target == null) return;

        RuntimeManager.PlayOneShotAttached(sfxEvent, target);
    }

    public static EventInstance StartVariableLengthSFX(EventReference sfxEvent, GameObject target, Object context = null)
    {
        if (sfxEvent.IsNull)
        {
            LogMissingEvent(context);
            return default;
        }
         if (target == null) return default;

        var instance = RuntimeManager.CreateInstance(sfxEvent);
        RuntimeManager.AttachInstanceToGameObject(instance,target);
        instance.start();
        return instance;
    }

    public static void StopandReleaseVariableLengthSFX(EventInstance instance)
    {
        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        instance.release();
    }

    private static void LogMissingEvent(Object context)
    {
        string name = context != null ? context.name : "unknown caller";
        Debug.LogWarning($"[AudioManager] PlaySFX called with an empty EventReference (from '{name}'). No sound will play — did you forget to assign it in the inspector?", context);
    }
}