using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Enemies;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

public class MusicIntensityDriver : MonoBehaviour
{
    [Serializable]
    public class IntroStep
    {
        public string marker = "";
        [Min(1)] public int bars = 4;
        [Min(0f)] public float nextIntensity = 1f;
    }

    [Serializable]
    public class IntensityProfile
    {
        public string name = "Track";
        public EventReference musicEvent;
        [FormerlySerializedAs("calmIntensity"), Min(0f)] public float baselineIntensity = 0f;
        [Min(0f)] public float onScreenIntensity = 3f;
        [Min(0f)] public float engagedIntensity = 5f;
        [Min(0f)] public float engagedStep = 2f;
        [Min(0f)] public float perExtraEnemyIntensity = 1f;
        [Min(0f)] public float maximumIntensity = 99f;
        public List<IntroStep> introSteps = new List<IntroStep>();

        public IntroStep FindIntroStep(string markerName)
        {
            if (introSteps == null || string.IsNullOrEmpty(markerName)) return null;

            string trimmed = markerName.Trim();
            foreach (IntroStep step in introSteps)
            {
                if (step != null && string.Equals(step.marker.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                    return step;
            }

            string number = TrailingNumber(trimmed);
            if (number.Length == 0) return null;

            foreach (IntroStep step in introSteps)
            {
                if (step != null && TrailingNumber(step.marker.Trim()) == number)
                    return step;
            }

            return null;
        }

        private static string TrailingNumber(string text)
        {
            int start = text.Length;
            while (start > 0 && char.IsDigit(text[start - 1])) start--;
            return text.Substring(start).TrimStart('0');
        }
    }

    [SerializeField] private string intensityParameter = "Intensity";

    [Header("Profiles")]
    [SerializeField] private List<IntensityProfile> trackProfiles = new List<IntensityProfile>
    {
        new IntensityProfile
        {
            name = "Hospital",
            baselineIntensity = 2.5f,
            onScreenIntensity = 4.5f,
            engagedIntensity = 3.5f,
            engagedStep = 2f,
            perExtraEnemyIntensity = 1f,
            maximumIntensity = 13f,
            introSteps = new List<IntroStep>
            {
                new IntroStep { marker = "Hospital1", bars = 8, nextIntensity = 0.5f },
                new IntroStep { marker = "Hospital2", bars = 8, nextIntensity = 1.5f },
                new IntroStep { marker = "Hospital3", bars = 8, nextIntensity = 2.5f }
            }
        },
        new IntensityProfile
        {
            name = "Tower",
            baselineIntensity = 2f,
            onScreenIntensity = 4f,
            engagedIntensity = 4f,
            engagedStep = 4f,
            perExtraEnemyIntensity = 2f,
            maximumIntensity = 13f,
            introSteps = new List<IntroStep>
            {
                new IntroStep { marker = "Tower1", bars = 4, nextIntensity = 2f }
            }
        }
    };

    [Header("Enemies")]
    [SerializeField, Min(0f)] private float deaggroFadeDuration = 4f;

    [Header("Fast Travel")]
    [SerializeField, Min(0f)] private float fastTravelFadeDuration = 1.5f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float riseSpeed = 4f;
    [SerializeField, Min(0f)] private float fallSpeed = 1.5f;

    [Header("Start Lock")]
    [SerializeField] private bool lockUntilTriggered = true;
    [SerializeField, Min(0f)] private float lockedIntensity = 0f;

    [Header("Intro")]
    [SerializeField, Min(0)] private int introLeadBeats = 1;
    [SerializeField, Min(1)] private int defaultBeatsPerBar = 4;
    [SerializeField, Min(0f)] private float introTimeout = 30f;

    [Header("Debug")]
    [SerializeField] private bool debugIntensity = false;
    [SerializeField] private bool debugTiming = false;
    [SerializeField] private bool debugEnemies = false;

    private struct MarkerEvent
    {
        public IntPtr instance;
        public bool isMarker;
        public string name;
        public int position;
        public int beatsPerBar;
        public float tempo;
    }

    private static readonly Queue<MarkerEvent> PendingMarkers = new Queue<MarkerEvent>();
    private static readonly object MarkerLock = new object();
    private static readonly EVENT_CALLBACK MarkerCallback = OnMarkerCallback;

    private static readonly HashSet<object> FastTravelSources = new HashSet<object>();
    private static readonly Dictionary<object, Vector2> IntensityZones = new Dictionary<object, Vector2>();

    private static bool intensityUnlocked;
    public static bool IntensityUnlocked => intensityUnlocked;

    public static void UnlockIntensity() => intensityUnlocked = true;
    public static void LockIntensity() => intensityUnlocked = false;

    private EventInstance playingInstance;
    private bool callbackSet;
    private float nextPlayingCheck;
    private string currentSection = "";

    private bool introActive;
    private float introValue;
    private IntroStep activeStep;
    private int beatsSinceMarker;
    private int lastBeatPosition = -1;
    private bool needsBeatSync;
    private int beatsPerBar;
    private float lastMarkerTime;

    private float current;
    private float combatFade;
    private float combatPeak;
    private float fastTravelWeight = 1f;
    private float lastLogged = -1f;
    private IntensityProfile profile;
    private Camera gameCamera;
    private readonly List<Enemy> onScreenEnemies = new List<Enemy>();
    private int lastEngagedCount;
    private float combatBase;
    private string lastEnemyReport = "";

    private bool hasRange;
    private float rangeMin;
    private float rangeMax = 1f;

    public float RangeMin => rangeMin;
    public float RangeMax => rangeMax;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        FastTravelSources.Clear();
        IntensityZones.Clear();
        lock (MarkerLock) PendingMarkers.Clear();
        intensityUnlocked = false;
    }

    public static void SetNearFastTravel(object source, bool near)
    {
        if (source == null) return;
        if (near) FastTravelSources.Add(source);
        else FastTravelSources.Remove(source);
    }

    public static void SetIntensityZone(object source, float minimum, float maximum)
    {
        if (source == null) return;
        IntensityZones[source] = new Vector2(minimum, maximum);
    }

    public static void ClearIntensityZone(object source)
    {
        if (source == null) return;
        IntensityZones.Remove(source);
    }

    [MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private static FMOD.RESULT OnMarkerCallback(EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        try
        {
            if (type == EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
            {
                var props = (TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(TIMELINE_MARKER_PROPERTIES));
                MarkerEvent evt = new MarkerEvent { instance = instancePtr, isMarker = true, name = props.name, position = props.position };
                lock (MarkerLock) PendingMarkers.Enqueue(evt);
            }
            else if (type == EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            {
                var props = (TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(TIMELINE_BEAT_PROPERTIES));
                MarkerEvent evt = new MarkerEvent { instance = instancePtr, position = props.position, beatsPerBar = props.timesignatureupper, tempo = props.tempo };
                lock (MarkerLock) PendingMarkers.Enqueue(evt);
            }
        }
        catch (Exception)
        {
        }

        return FMOD.RESULT.OK;
    }

    private void OnDisable()
    {
        ReleasePlaying();
    }

    private bool TryCacheRange()
    {
        if (hasRange) return true;

        FMOD.RESULT result = RuntimeManager.StudioSystem.getParameterDescriptionByName(intensityParameter, out var description);
        if (result != FMOD.RESULT.OK) return false;

        rangeMin = description.minimum;
        rangeMax = description.maximum;
        hasRange = true;
        current = rangeMin;
        return true;
    }

    private void Update()
    {
        MusicManager music = MusicManager.Instance;
        if (music == null || !TryCacheRange()) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Title)
            intensityUnlocked = false;

        RefreshPlaying();
        ProcessMarkers();
        if (profile == null) return;

        float dt = Time.unscaledDeltaTime;
        bool locked = lockUntilTriggered && !intensityUnlocked;
        float target;

        if (introActive && !locked && introTimeout > 0f && Time.unscaledTime - lastMarkerTime > introTimeout)
        {
            introActive = false;
            if (debugTiming) Debug.Log($"[MusicTiming] No intro markers for {introTimeout}s, handing over to normal intensity", this);
        }

        if (locked)
        {
            target = Mathf.Clamp(lockedIntensity, rangeMin, rangeMax);
            current = target;
            combatFade = rangeMin;
        }
        else if (introActive)
        {
            target = Mathf.Clamp(introValue, rangeMin, rangeMax);
            current = target;
            combatFade = rangeMin;
        }
        else
        {
            target = ComputeTarget(dt);
            float speed = target > current ? riseSpeed : fallSpeed;
            current = Mathf.MoveTowards(current, target, speed * dt);
        }

        float weightTarget = FastTravelSources.Count > 0 ? 0f : 1f;
        fastTravelWeight = fastTravelFadeDuration > 0f
            ? Mathf.MoveTowards(fastTravelWeight, weightTarget, dt / fastTravelFadeDuration)
            : weightTarget;

        float output = Mathf.Lerp(rangeMin, current, fastTravelWeight);
        float normalized = rangeMax > rangeMin ? Mathf.InverseLerp(rangeMin, rangeMax, output) : 0f;

        if (!music.IsBossMusicActive) music.SetTargetIntensity(normalized);

        if (debugIntensity && Mathf.Abs(output - lastLogged) >= 0.25f)
        {
            lastLogged = output;
            Debug.Log($"[MusicIntensity] {output:0.00}{(locked ? " LOCKED" : introActive ? " INTRO" : "")} (target {target:0.00}, engaged {EnemyCombatTracker.TargetingCount}, combat fade {combatFade:0.00}, fast travel weight {fastTravelWeight:0.00}, profile {profile.name}, section '{currentSection}', range {rangeMin:0.##}-{rangeMax:0.##})", this);
        }
    }

    private void ReleasePlaying()
    {
        if (callbackSet && playingInstance.isValid()) playingInstance.setCallback(null, EVENT_CALLBACK_TYPE.ALL);
        callbackSet = false;
        playingInstance.clearHandle();
        profile = null;
        currentSection = "";
        introActive = false;
        activeStep = null;
    }

    private void RefreshPlaying()
    {
        bool alive = profile != null && IsPlaying(playingInstance);
        if (alive && Time.unscaledTime < nextPlayingCheck)
        {
            UpdateCallback();
            return;
        }

        nextPlayingCheck = Time.unscaledTime + 0.25f;

        IntensityProfile bestProfile = null;
        EventInstance bestInstance = default;
        float bestVolume = -1f;

        if (trackProfiles != null)
        {
            foreach (IntensityProfile candidate in trackProfiles)
            {
                if (candidate == null || candidate.musicEvent.IsNull) continue;

                EventDescription description;
                try
                {
                    description = RuntimeManager.GetEventDescription(candidate.musicEvent);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!description.isValid() || description.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK) continue;

                foreach (EventInstance instance in instances)
                {
                    if (!IsPlaying(instance)) continue;

                    instance.getVolume(out float volume);
                    if (volume <= bestVolume) continue;

                    bestVolume = volume;
                    bestProfile = candidate;
                    bestInstance = instance;
                }
            }
        }

        if (bestProfile == null)
        {
            ReleasePlaying();
            return;
        }

        if (bestInstance.handle != playingInstance.handle)
        {
            ReleasePlaying();
            playingInstance = bestInstance;
            profile = bestProfile;
            StartIntro();
            if (debugTiming) Debug.Log($"[MusicTiming] Now following {bestProfile.name}{(introActive ? ", stepping up to baseline" : "")}", this);
        }

        profile = bestProfile;
        UpdateCallback();
    }

    private void StartIntro()
    {
        introActive = profile != null && profile.introSteps != null && profile.introSteps.Count > 0;
        introValue = Mathf.Clamp(lockedIntensity, rangeMin, rangeMax);
        activeStep = introActive ? profile.introSteps[0] : null;
        beatsSinceMarker = 0;
        lastBeatPosition = -1;
        needsBeatSync = true;
        lastMarkerTime = Time.unscaledTime;
    }

    private void UpdateCallback()
    {
        bool wanted = debugTiming || introActive;

        if (wanted && !callbackSet && playingInstance.isValid())
        {
            playingInstance.setCallback(MarkerCallback, EVENT_CALLBACK_TYPE.TIMELINE_MARKER | EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
            callbackSet = true;
        }
        else if (!wanted && callbackSet)
        {
            if (playingInstance.isValid()) playingInstance.setCallback(null, EVENT_CALLBACK_TYPE.ALL);
            callbackSet = false;
        }
    }

    private static bool IsPlaying(EventInstance instance)
    {
        if (!instance.isValid() || instance.getPlaybackState(out PLAYBACK_STATE state) != FMOD.RESULT.OK) return false;
        return state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING;
    }

    private void ProcessMarkers()
    {
        while (true)
        {
            MarkerEvent evt;
            lock (MarkerLock)
            {
                if (PendingMarkers.Count == 0) return;
                evt = PendingMarkers.Dequeue();
            }

            if (!callbackSet || !playingInstance.isValid() || evt.instance != playingInstance.handle) continue;

            if (evt.isMarker) HandleMarker(evt);
            else HandleBeat(evt);
        }
    }

    private void HandleMarker(MarkerEvent evt)
    {
        currentSection = evt.name ?? "";
        beatsSinceMarker = lastBeatPosition == evt.position ? 1 : 0;
        needsBeatSync = false;
        lastMarkerTime = Time.unscaledTime;

        if (debugTiming)
            Debug.Log($"[MusicTiming] Section '{currentSection}' started at {evt.position / 1000f:0.00}s (sending {current:0.00})", this);

        if (!introActive) return;

        activeStep = profile != null ? profile.FindIntroStep(currentSection) : null;
        if (activeStep != null) return;

        if (profile != null && introValue >= profile.baselineIntensity - 0.001f)
        {
            introActive = false;
            if (debugTiming) Debug.Log($"[MusicTiming] Reached baseline at '{currentSection}', handing over to normal intensity", this);
        }
    }

    private void HandleBeat(MarkerEvent evt)
    {
        if (evt.beatsPerBar > 0) beatsPerBar = evt.beatsPerBar;
        lastBeatPosition = evt.position;

        if (needsBeatSync && evt.tempo > 0f)
        {
            float beatLength = 60000f / evt.tempo;
            beatsSinceMarker = Mathf.FloorToInt(evt.position / beatLength + 0.5f) + 1;
            needsBeatSync = false;
        }
        else
        {
            beatsSinceMarker++;
        }

        if (!introActive || activeStep == null || (lockUntilTriggered && !intensityUnlocked)) return;

        int perBar = beatsPerBar > 0 ? beatsPerBar : defaultBeatsPerBar;
        int length = Mathf.Max(1, activeStep.bars * perBar);
        int releaseAt = Mathf.Clamp(length - introLeadBeats + 1, 1, length);
        int beatInSection = (Mathf.Max(1, beatsSinceMarker) - 1) % length + 1;
        if (beatInSection != releaseAt) return;

        introValue = Mathf.Max(introValue, activeStep.nextIntensity);
        if (debugTiming) Debug.Log($"[MusicTiming] '{currentSection}' played {activeStep.bars} bars, stepping to {introValue:0.00}", this);
        activeStep = null;
    }

    private float ComputeTarget(float dt)
    {
        int engaged = EnemyCombatTracker.TargetingCount;
        Camera cam = GetGameCamera();
        int onScreenCount = EnemyCombatTracker.CollectUnengagedOnScreen(cam, onScreenEnemies);

        float nonCombat = profile.baselineIntensity;
        if (onScreenCount > 0) nonCombat = Mathf.Max(nonCombat, profile.onScreenIntensity);

        float enemy = rangeMin;

        if (engaged > 0)
        {
            if (lastEngagedCount == 0) combatBase = Mathf.Max(current, nonCombat);

            enemy = Mathf.Max(profile.engagedIntensity, combatBase + profile.engagedStep)
                    + profile.perExtraEnemyIntensity * (engaged - 1);
            combatPeak = enemy;
            combatFade = enemy;
        }
        else if (combatFade > rangeMin)
        {
            combatFade = deaggroFadeDuration > 0f
                ? Mathf.MoveTowards(combatFade, rangeMin, (combatPeak - rangeMin) / deaggroFadeDuration * dt)
                : rangeMin;
        }

        if (debugEnemies) ReportEnemies(engaged, onScreenCount, cam, enemy);
        lastEngagedCount = engaged;

        enemy = Mathf.Max(enemy, combatFade);
        if (onScreenCount > 0) enemy = Mathf.Max(enemy, profile.onScreenIntensity);

        float value = Mathf.Max(enemy, profile.baselineIntensity);
        value = Mathf.Min(value, profile.maximumIntensity);

        if (IntensityZones.Count > 0)
        {
            float minimum = rangeMin;
            float maximum = rangeMax;

            foreach (Vector2 zone in IntensityZones.Values)
            {
                minimum = Mathf.Max(minimum, zone.x);
                maximum = Mathf.Min(maximum, zone.y);
            }

            value = Mathf.Min(Mathf.Max(value, minimum), maximum);
        }

        return Mathf.Clamp(value, rangeMin, rangeMax);
    }

    private Camera GetGameCamera()
    {
        if (gameCamera != null && gameCamera.isActiveAndEnabled) return gameCamera;

        CameraFollow2D follow = FindFirstObjectByType<CameraFollow2D>();
        gameCamera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
        return gameCamera;
    }

    private void ReportEnemies(int engaged, int onScreenCount, Camera cam, float combatValue)
    {
        System.Text.StringBuilder engagedNames = new System.Text.StringBuilder();
        foreach (Enemy e in EnemyCombatTracker.TargetingEnemies)
        {
            if (e == null) continue;
            if (engagedNames.Length > 0) engagedNames.Append(", ");
            engagedNames.Append(e.name);
        }

        System.Text.StringBuilder screenNames = new System.Text.StringBuilder();
        foreach (Enemy e in onScreenEnemies)
        {
            if (e == null) continue;
            if (screenNames.Length > 0) screenNames.Append(", ");
            screenNames.Append(e.name);
        }

        string report = $"engaged {engaged} [{engagedNames}] | on screen {onScreenCount} [{screenNames}]";
        if (report == lastEnemyReport) return;
        lastEnemyReport = report;

        string camInfo = cam != null ? $"{cam.name} {EnemyCombatTracker.GetCameraRect(cam)}" : "none";
        string combat = engaged > 0 ? $", combat {combatValue:0.00} (from {combatBase:0.00} + step {profile.engagedStep:0.##})" : "";
        Debug.Log($"[MusicEnemies] {report}{combat}, camera {camInfo}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Camera cam = GetGameCamera();
        if (cam == null) return;

        Rect view = EnemyCombatTracker.GetCameraRect(cam);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(view.center, view.size);
    }
}