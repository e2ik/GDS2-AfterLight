using UnityEngine;
using FMODUnity;

public static class AudioManager
{
    public static void PlaySFX(EventReference sfxEvent)
    {
        if (sfxEvent.IsNull) return;
        RuntimeManager.PlayOneShot(sfxEvent);
    }

    public static void PlaySFX(EventReference sfxEvent, Vector3 position)
    {
        if (sfxEvent.IsNull) return;
        RuntimeManager.PlayOneShot(sfxEvent, position);
    }

    public static void PlaySFXAttached(EventReference sfxEvent, GameObject target)
    {
        if (sfxEvent.IsNull || target == null) return;
        
        RuntimeManager.PlayOneShotAttached(sfxEvent, target);
    }
}
