using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public enum MusicState {Chill, Explore, Combat}

public static class MusicRanges
{
    public static (float min, float max) GetRange(MusicState state) => state switch
    {
        MusicState.Chill => (0f, 3f),
        MusicState.Explore => (5f, 8f),
        MusicState.Combat => (11f, 13f),
        _ => (0f, 3f)
    };
}

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private EventReference musicEvent;
    [SerializeField] private float smoothSpeed = 2f;
    [SerializeField] private float decayPerSecond = 0.125f;

    private EventInstance _instance;
    private PARAMETER_ID _intensityParamId;
    private float _currentIntensity;
    private float _targetIntensity;
    private MusicState _currentState = MusicState.Chill;
    private bool _hasUnlockedAudio = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if !UNITY_WEBGL
        PlayMusic();
#endif
    }

    public void PlayMusic()
    {
        if (musicEvent.IsNull) return;

        _instance = RuntimeManager.CreateInstance(musicEvent);
        _instance.start();

   
        RuntimeManager.StudioSystem.getParameterDescriptionByName("Intensity", out var paramDesc);
        _intensityParamId = paramDesc.id;
    }

    public void StopMusic(bool fadeout = true)
    {
        if (!_instance.isValid()) return;

        _instance.stop(fadeout ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
        _instance.release();
    }

    public void SetState(MusicState newState)
    {
        if (newState == _currentState) return;

        bool ascending = (int)newState > (int)_currentState;
        _currentState = newState;

        var (min, max) = MusicRanges.GetRange(newState);
        _targetIntensity = ascending ? min : max;
    }

    public void SetIntensityNormalized(float normalized)
    {
        var (min, max) = MusicRanges.GetRange(_currentState);
        _targetIntensity = Mathf.Lerp(min, max, Mathf.Clamp01(normalized));
    }

    public static void AddIntensity(float amount)
    {
        if (Instance == null) return;
    }

    private void InternalAddIntensity(float amount)
    {
        var (min, max) = MusicRanges.GetRange(_currentState);
        _targetIntensity = Mathf.Clamp(_targetIntensity + amount, min, max);
    }

    private void Update()
    {
#if UNITY_WEBGL
        if (!_hasUnlockedAudio && Input.anyKeyDown)
        {
            _hasUnlockedAudio = true;
            PlayMusic();
        }
#endif

        if (!_instance.isValid()) return;

        var (min, _) = MusicRanges.GetRange(_currentState);

        _targetIntensity = Mathf.MoveTowards(_targetIntensity, min, decayPerSecond * Time.deltaTime);
        
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, smoothSpeed * Time.deltaTime);

        RuntimeManager.StudioSystem.setParameterByID(_intensityParamId, _currentIntensity);
    }
}