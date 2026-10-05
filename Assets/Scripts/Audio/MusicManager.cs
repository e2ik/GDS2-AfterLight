using System;
using System.Collections;
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
    [SerializeField] private float crossfadeDuration = 1.5f;
    [SerializeField] private float stopFadeDuration = 0.7f;
    [SerializeField, Range(0f, 1f)] private float idleFloor = 0f;
    public float StopFadeDuration => stopFadeDuration;
    
    private EventInstance _instance;
    public bool IsBossMusicActive => _bossMode;

    private Coroutine _crossfadeRoutine;
    
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

    private EventReference _currentEvent;
    private bool _hasCurrentEvent;
    private EventReference _preBossEvent;
    private bool _hasPreBossEvent;
    private bool _holdMusic;

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
        _currentEvent = defaultMusicEvent;
        _hasCurrentEvent = true;
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
        if (musicEvent.IsNull) return;

        EventInstance previousInstance = _instance;
        bool hasPrevious = previousInstance.isValid();

        EventInstance newInstance = RuntimeManager.CreateInstance(musicEvent);

        if (fadeOutPrevious && hasPrevious)
        {
            newInstance.setVolume(0f);
            newInstance.start();

            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);

            _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(previousInstance, newInstance));
        }
        else
        {
            if (hasPrevious)
            {
                previousInstance.stop(STOP_MODE.IMMEDIATE);
                previousInstance.release();
            }

            newInstance.setVolume(1f);
            newInstance.start();
        }

        _instance = newInstance;
        
        newInstance.getDescription(out var eventDesc);
        _hasLoopParam = eventDesc.getParameterDescriptionByName("Loop", out var loopDesc) == FMOD.RESULT.OK;

        if (_hasLoopParam)
            _loopParamId = loopDesc.id;
    }

    private IEnumerator CrossfadeRoutine(EventInstance fadingOut, EventInstance fadingIn)
    {
        float t = 0f;

        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / crossfadeDuration);

            if (fadingOut.isValid())
                fadingOut.setVolume(1f - ratio);

            if (fadingIn.isValid())
                fadingIn.setVolume(ratio);

            yield return null;
        }

        if (fadingOut.isValid())
        {
            fadingOut.stop(STOP_MODE.IMMEDIATE);
            fadingOut.release();
        }

        if (fadingIn.isValid())
            fadingIn.setVolume(1f);

        _crossfadeRoutine = null;
    }

    public void SwitchMusic(EventReference newLevelEvent, bool isBossMusic = false, bool fadeOutPrevious = true)
    {
        bool sameEvent = _hasCurrentEvent && _instance.isValid() && _currentEvent.Guid.Equals(newLevelEvent.Guid);

        if (isBossMusic && !_bossMode)
        {
            _preBossEvent = _currentEvent;
            _hasPreBossEvent = _hasCurrentEvent;
        }
        
        _bossMode = isBossMusic;

        if (sameEvent)
            return;
        
        PlayMusic(newLevelEvent, fadeOutPrevious);
        _currentEvent = newLevelEvent;
        _hasCurrentEvent = true;
        _currentNormalized = _targetNormalized = isBossMusic ? 0f : idleFloor;
    }

    public void StopMusic(bool fadeout = true)
    {
        if (!_instance.isValid()) return;

        if (fadeout && stopFadeDuration > 0f)
        {
            StartCoroutine(FadeOutAndRelease(_instance, stopFadeDuration));
            _instance = default;
            return;
        }

        _instance.stop(STOP_MODE.IMMEDIATE);
        _instance.release();
    }
    
    public IEnumerator FadeOutAndRelease(EventInstance inst, float duration)
    {
        if (!inst.isValid()) yield break;

        inst.getVolume(out float startVolume);
        float t = 0f;
        while (t < duration)
        {
            if (!inst.isValid()) yield break;
            t += Time.unscaledDeltaTime;
            inst.setVolume(startVolume * (1f - Mathf.Clamp01(t / duration)));
            yield return null;
        }

        if (inst.isValid())
        {
            inst.stop(STOP_MODE.IMMEDIATE);
            inst.release();
        }
    }

    public void SetPaused(bool paused)
    {
        if (!_hasPauseParam)
            return;

        RuntimeManager.StudioSystem.setParameterByID(_pauseParamId, paused ? 1f : 0f);
    }

    public void StopMusicForCutscene()
    {
        _holdMusic = true;

        if (_crossfadeRoutine != null)
        {
            StopCoroutine(_crossfadeRoutine);
            _crossfadeRoutine = null;
        }
        
        StopMusic(fadeout: true);
    }

    public void RestartMusic()
    {
        _holdMusic = false;

        EventReference toPlay = _hasCurrentEvent ? _currentEvent : defaultMusicEvent;
        PlayMusic(toPlay, fadeOutPrevious: false);
        _currentEvent = toPlay;
        _hasCurrentEvent = true;
        
#if UNITY_WEBGL
        _hasUnlockedAudio = true;
#endif
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

    public void ExitBossMusicToPrevious(bool fadeOutPrevious = true)
    {
        if (!_hasPreBossEvent)
            return;
        
        SwitchMusic(_preBossEvent, isBossMusic: false, fadeOutPrevious);
    }

    private void Update()
    {
#if UNITY_WEBGL
        if (!_hasUnlockedAudio && !_holdMusic && Input.anyKeyDown)
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
        RuntimeManager.StudioSystem.setParameterByID(_intensityParamId, actualValue);
    }
}