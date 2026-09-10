using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

[Serializable]
public struct AnimationSFXEntry
{
    public string id;
    public EventReference sfxEvent;
}

public class AnimationSFXPlayer : MonoBehaviour
{
    [SerializeField] private List<AnimationSFXEntry> sfxEntries = new();

    private Dictionary<string, EventReference> _lookup;

    private void Awake()
    {
        _lookup = new Dictionary<string, EventReference>();

        foreach (AnimationSFXEntry entry in sfxEntries)
            _lookup[entry.id] = entry.sfxEvent;
    }

    public void PlaySFXEvent(string id)
    {
        if (!_lookup.TryGetValue(id, out var evt))
        {
            Debug.LogWarning($"AnimationSFXPlayer: no SFX for id: {id} on {gameObject.name}");
            return;
        }
        
        AudioManager.PlaySFX(evt, transform.position);
    }

    public void PlaySFXEventAttached(string id)
    {
        if (!_lookup.TryGetValue(id, out var evt))
        {
            Debug.LogWarning($"AnimationSFXPlayer: no SFX for id: {id} on {gameObject.name}");
            return;
        }
        
        AudioManager.PlaySFXAttached(evt, gameObject);
    }
}
