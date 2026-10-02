using System;
using FMOD.Studio;
using FMODUnity;
using Unity.VisualScripting;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;


public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private EventReference defaultMusicEvent;
    [SerializeField] private float smoothSpeed = 2f;
    [SerializeField] private float decayPerSecond = 0.125f;
    [SerializeField] private float decayDelay = 1.5f;
    [SerializeField, Range(0f, 1f)] private float idleFloor = 0f;

    private EventInstance _instance;
    private PARAMETER_ID _intensityParamId;
    private PARAMETER_ID _pauseParamId;
    private bool _hasPauseParam;
    private PARAMETER_ID _loopParamId;
    private bool _hasLoopParam;

    private float _intensityMin;
    private float _intensityMax;

    private float _currentNormalized;
    private float _targetNormalized;
    private float _decayDelayTimer;
    private bool _bossMode;
    private bool _hasUnlockedAudio = false;
    private EventReference _pendingEvent;
    private bool _hasGlobalParams;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheGlobalParameters();

#if !UNITY_WEBGL
        PlayMusic(defaultMusicEvent);
#else
        _pendingEvent = defaultMusicEvent;
#endif
    }
    
    private void CacheGlobalParameters()
    {
        FMOD.RESULT intensityResult = RuntimeManager.StudioSystem.getParameterDescriptionByName("Intensity", out var intensityDesc);

        if (intensityResult != FMOD.RESULT.OK)
        {
            Debug.LogError($"MusicManager: no global 'Intensity' parameter found ({intensityResult})");
            return;
        }
        
        _intensityParamId = intensityDesc.id;
        _intensityMin = intensityDesc.minimum;
        _intensityMax = intensityDesc.maximum;

        _hasPauseParam = RuntimeManager.StudioSystem.getParameterDescriptionByName("Pause", out var pauseDesc) ==
                         FMOD.RESULT.OK;
        if (_hasPauseParam)
            _pauseParamId = pauseDesc.id;

        _hasGlobalParams = true;
    }

    public void PlayMusic(EventReference musicEvent, bool fadeOutPrevious = true)
    {
        StopMusic(fadeOutPrevious);
        
        if (musicEvent.IsNull) return;

        _instance = RuntimeManager.CreateInstance(musicEvent);
        _instance.start();

        _instance.getDescription(out var eventDesc);
        _hasLoopParam = eventDesc.getParameterDescriptionByName("Loop", out var loopDesc) == FMOD.RESULT.OK;

        if (_hasLoopParam)
            _loopParamId = loopDesc.id;
    }

    public void SwitchMusic(EventReference newLevelEvent, bool isBossMusic = false, bool fadeOutPrevious = true)
    {
        _bossMode = isBossMusic;
        PlayMusic(newLevelEvent, fadeOutPrevious);
        _currentNormalized = _targetNormalized = isBossMusic ? 0f : idleFloor;
    }

    public void StopMusic(bool fadeout = true)
    {
        if (!_instance.isValid()) return;

        _instance.stop(fadeout ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
        _instance.release();
    }

    public void SetPaused(bool paused)
    {
        if (!_hasPauseParam)
            return;

        RuntimeManager.StudioSystem.setParameterByID(_pauseParamId, paused ? 1f : 0f);
    }

    public void SetTargetIntensity(float normalized, bool instant = false)
    {
        if (_bossMode)
            return;

        _targetNormalized = Mathf.Clamp01(normalized);
        _decayDelayTimer = decayDelay;

        if (instant)
            _currentNormalized = _targetNormalized;
    }
    public static void AddIntensity(float amount)
    {
        if (Instance == null || Instance._bossMode) 
            return;
        
        Instance.InternalAddIntensity(amount);
    }

    private void InternalAddIntensity(float amount)
    {
        _targetNormalized = Mathf.Clamp01(_targetNormalized + amount);
        _decayDelayTimer = decayDelay;
    }

    public void SetBossIntensity(float healthPercent)
    {
        if (!_bossMode)
            return;

        _targetNormalized = 1f - Mathf.Clamp01(healthPercent);
    }

    public void SetLoop(bool loop)
    {
        if (!_instance.isValid() || !_hasLoopParam)
            return;

        _instance.setParameterByID(_loopParamId, loop ? 1f : 0f);
    }

    private void Update()
    {
#if UNITY_WEBGL
        if (!_hasUnlockedAudio && Input.anyKeyDown)
        {
            _hasUnlockedAudio = true;
            PlayMusic(_pendingEvent);
        }
#endif

        if (!_instance.isValid() || !_hasGlobalParams) return;

        if (!_bossMode)
        {
            if (_decayDelayTimer > 0f)
            {
                _decayDelayTimer -= Time.deltaTime;
            }
            else
            {
                _targetNormalized = Mathf.MoveTowards(_targetNormalized, idleFloor, decayPerSecond * Time.deltaTime);
            }
        }

        _currentNormalized = Mathf.MoveTowards(_currentNormalized, _targetNormalized, smoothSpeed * Time.deltaTime);
        float actualValue = Mathf.Lerp(_intensityMin, _intensityMax, _currentNormalized);
        Debug.Log($"Intensity Value {actualValue}");
        RuntimeManager.StudioSystem.setParameterByID(_intensityParamId, actualValue);
    }
}