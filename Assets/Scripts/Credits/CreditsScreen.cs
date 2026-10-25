using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class CreditsScreen : MonoBehaviour
{
    [SerializeField] private CreditsData data;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool returnToTitleOnFinish = true;
    [SerializeField] private bool unloadSceneOnFinish = true;
    [SerializeField] private bool freezePlayer = true;
    [SerializeField] private UnityEvent onFinished;

    [Header("Root")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField, Min(0.1f)] private float scale = 1f;
    [SerializeField] private bool snapScaleToWhole = false;
    [SerializeField, Min(0f)] private float fadeInDuration = 1.5f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 1.5f;

    [Header("Background")]
    [FormerlySerializedAs("creditsBackgrounds")]
    [SerializeField] private ParallaxBackground[] backgrounds;
    [SerializeField, Min(0f)] private float screenFadeDuration = 1f;

    [Header("Terminal")]
    [SerializeField] private CanvasGroup terminalGroup;
    [SerializeField] private RectTransform terminalContent;
    [SerializeField, Min(1)] private int maxTerminalLines = 14;
    [SerializeField] private string headingPrefix = "> ";
    [SerializeField] private string linePrefix = "  ";
    [SerializeField] private char leaderChar = '.';
    [SerializeField] private bool autoLineWidth = true;
    [SerializeField, Min(10)] private int lineWidth = 36;
    [SerializeField, Min(1)] private int minLeaderDots = 3;
    [SerializeField] private string statusSuffix = " <color=#7CFFB2>[OK]</color>";
    [SerializeField, Min(1f)] private float charsPerSecond = 40f;
    [SerializeField, Min(0f)] private float linePause = 0.25f;
    [SerializeField, Min(0f)] private float sectionPause = 1f;

    [Header("Cursor")]
    [SerializeField] private bool showCursor = true;
    [SerializeField] private Sprite cursorSprite;
    [SerializeField] private bool useTextColorForCursor = true;
    [SerializeField] private Color cursorColor = Color.white;
    [SerializeField, Min(0f)] private float cursorWidth = 0f;
    [SerializeField, Min(0f)] private float cursorGap = 1f;
    [SerializeField, Min(0.05f)] private float cursorBlinkInterval = 0.5f;

    [Header("Card")]
    [SerializeField] private CanvasGroup cardGroup;
    [SerializeField] private RectTransform cardContent;
    [SerializeField, Min(0f)] private float cardFadeInDuration = 0.5f;
    [SerializeField, Min(0f)] private float cardFadeOutDuration = 0.5f;
    [SerializeField] private bool holdStatsUntilSkip = true;

    [Header("Line Prefabs")]
    [SerializeField] private TMP_Text headingPrefab;
    [SerializeField] private TMP_Text linePrefab;
    [SerializeField] private TMP_Text smallPrintPrefab;
    [SerializeField, Min(0f)] private float spacerHeight = 16f;

    [Header("Text Colors")]
    [SerializeField] private bool overrideTextColors = true;
    [SerializeField] private Color headingColor = new Color(0.49f, 1f, 0.7f, 1f);
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private Color smallPrintColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private bool colorRoleAndName = true;
    [SerializeField] private Color roleColor = new Color(0.65f, 0.8f, 0.85f, 1f);
    [SerializeField] private Color nameColor = Color.white;

    [Header("Line Pulse")]
    [SerializeField] private bool pulseOnLineComplete = true;
    [SerializeField, Min(1f)] private float pulseBrightness = 1.6f;
    [SerializeField, Range(0f, 1f)] private float pulseWhiten = 0.35f;
    [SerializeField, Min(0.01f)] private float pulseDuration = 0.5f;
    [SerializeField, Range(0.01f, 0.9f)] private float pulseRise = 0.15f;

    [Header("Input")]
    [SerializeField] private InputActionReference skipAction;
    [SerializeField, Min(0f)] private float skipHoldDuration = 1f;
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private Image skipFill;
    [SerializeField, Min(0f)] private float promptFadeIn = 0.2f;
    [SerializeField, Min(0f)] private float promptVisibleTime = 1.5f;
    [SerializeField, Min(0f)] private float promptFadeOut = 1f;

    [Header("Audio")]
    [SerializeField] private EventReference creditsMusic;
    [SerializeField] private EventReference musicAfterFinish;
    [SerializeField] private EventReference typeBlip;
    [SerializeField, Range(0f, 1f)] private float typeBlipVolume = 0.4f;
    [SerializeField, Min(1)] private int blipEveryChars = 2;

    [Header("Beat Sync")]
    [SerializeField] private bool syncToBeat = true;
    [SerializeField] private BeatUnit syncUnit = BeatUnit.Beat;
    [SerializeField] private bool typeToBeat = true;
    [SerializeField] private LineBeats lineBeats = LineBeats.FromLength;
    [SerializeField, Min(1)] private int beatsPerLine = 2;
    [SerializeField, Min(1)] private int maxBeatsPerLine = 8;
    [SerializeField, Min(0)] private int lineGapBeats = 1;
    [SerializeField] private bool compensateAudioLatency = true;
    [SerializeField] private float beatOffset = 0f;
    [SerializeField, Min(0.1f)] private float maxBeatWait = 2f;
    [SerializeField] private bool debugBeats = false;

    public enum BeatUnit { Beat, Bar }
    public enum LineBeats { Fixed, FromLength }

    private static readonly System.Diagnostics.Stopwatch BeatClock = System.Diagnostics.Stopwatch.StartNew();

    private struct BeatEvent
    {
        public System.IntPtr instance;
        public int beat;
        public int beatsPerBar;
        public float tempo;
        public double stamp;
    }

    private static readonly Queue<BeatEvent> PendingBeats = new Queue<BeatEvent>();
    private static readonly object BeatLock = new object();
    private static readonly FMOD.Studio.EVENT_CALLBACK BeatCallback = OnBeatCallback;

    private FMOD.Studio.EventInstance musicInstance;
    private bool beatHooked;
    private int beatCount;
    private int beatInBar;
    private float lastBeatTime = float.NegativeInfinity;
    private float musicTempo;
    private int musicBeatsPerBar = 4;
    private float audioLatency;
    private float chainedLineStart = -1f;

    private float BeatLength => musicTempo > 0f ? 60f / musicTempo : 0f;
    private bool BeatTiming => syncToBeat && typeToBeat && BeatActive && musicTempo > 0f;
    private float nextHookAttempt;
    [SerializeField] private EventReference cardSound;

    private readonly List<GameObject> terminalLines = new List<GameObject>();
    private readonly List<GameObject> cardLines = new List<GameObject>();

    private Coroutine playRoutine;
    private float skipHeld;
    private bool ignoreSkipUntilRelease;
    private bool tapRequested;
    private bool revealSection;
    private bool skipping;
    private bool enabledSkip;
    private bool playerFrozen;
    private CanvasGroup promptGroup;
    private float lastPromptInput = float.NegativeInfinity;
    private Image cursor;
    private bool cursorTyping;
    private TMP_Text measurer;
    private float terminalTargetWidth;

    public bool IsPlaying => playRoutine != null;


    private void Awake()
    {
        ApplyScale();
        if (rootGroup != null) rootGroup.alpha = 0f;
        SetBackgroundsActive(false);
        if (terminalGroup != null) terminalGroup.alpha = 0f;
        if (cardGroup != null) cardGroup.alpha = 0f;
        if (promptRoot != null)
        {
            promptGroup = promptRoot.GetComponent<CanvasGroup>();
            if (promptGroup == null) promptGroup = promptRoot.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            promptRoot.SetActive(false);
        }

        if (skipFill != null)
        {
            if (skipFill.type != Image.Type.Filled)
            {
                skipFill.type = Image.Type.Filled;
                skipFill.fillMethod = Image.FillMethod.Radial360;
                skipFill.fillOrigin = (int)Image.Origin360.Top;
                skipFill.fillClockwise = true;
            }

            skipFill.fillAmount = 0f;
        }
    }

    private void Start()
    {
        if (playOnStart) Play();
    }

    private void OnDisable()
    {
        ReleaseInput();
        ReleasePlayer();
        UnhookMusic();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) ApplyScale();
    }

    private void ApplyScale()
    {
        if (rootGroup == null) return;

        float value = snapScaleToWhole ? Mathf.Max(1f, Mathf.Round(scale)) : scale;
        rootGroup.transform.localScale = new Vector3(value, value, 1f);
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT OnBeatCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, System.IntPtr instancePtr, System.IntPtr parameterPtr)
    {
        try
        {
            if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            {
                var props = (FMOD.Studio.TIMELINE_BEAT_PROPERTIES)System.Runtime.InteropServices.Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_BEAT_PROPERTIES));
                BeatEvent evt = new BeatEvent { instance = instancePtr, beat = props.beat, beatsPerBar = props.timesignatureupper, tempo = props.tempo, stamp = BeatClock.Elapsed.TotalSeconds };
                lock (BeatLock) PendingBeats.Enqueue(evt);
            }
        }
        catch (System.Exception)
        {
        }

        return FMOD.RESULT.OK;
    }

    private bool BeatActive => beatHooked && Time.unscaledTime - lastBeatTime < 3f;

    private void TryHookMusic()
    {
        if (!syncToBeat || beatHooked || creditsMusic.IsNull || Time.unscaledTime < nextHookAttempt) return;
        nextHookAttempt = Time.unscaledTime + 0.2f;

        FMOD.Studio.EventDescription description;
        try
        {
            description = RuntimeManager.GetEventDescription(creditsMusic);
        }
        catch (System.Exception)
        {
            return;
        }

        if (!description.isValid() || description.getInstanceList(out FMOD.Studio.EventInstance[] instances) != FMOD.RESULT.OK) return;

        for (int i = instances.Length - 1; i >= 0; i--)
        {
            if (!instances[i].isValid() || instances[i].getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state) != FMOD.RESULT.OK) continue;
            if (state != FMOD.Studio.PLAYBACK_STATE.PLAYING && state != FMOD.Studio.PLAYBACK_STATE.STARTING) continue;

            musicInstance = instances[i];
            musicInstance.setCallback(BeatCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
            beatHooked = true;
            beatCount = 0;
            beatInBar = 0;
            audioLatency = compensateAudioLatency ? MeasureAudioLatency() : 0f;
            if (debugBeats) Debug.Log("[Credits] Listening to credits music beats", this);
            return;
        }
    }

    private static float MeasureAudioLatency()
    {
        if (RuntimeManager.CoreSystem.getDSPBufferSize(out uint bufferLength, out int bufferCount) != FMOD.RESULT.OK) return 0f;
        if (RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out FMOD.SPEAKERMODE _, out int _) != FMOD.RESULT.OK || sampleRate <= 0) return 0f;
        return (float)bufferLength * bufferCount / sampleRate;
    }

    private float NextBeatTime(float after, BeatUnit unit)
    {
        float length = BeatLength;
        if (length <= 0f) return after;

        int steps = Mathf.Max(0, Mathf.CeilToInt((after - lastBeatTime) / length - 0.001f));
        if (unit == BeatUnit.Bar && musicBeatsPerBar > 0)
        {
            int position = (beatInBar - 1 + steps) % musicBeatsPerBar;
            if (position != 0) steps += musicBeatsPerBar - position;
        }

        return lastBeatTime + steps * length;
    }

    private void UnhookMusic()
    {
        if (beatHooked && musicInstance.isValid()) musicInstance.setCallback(null, FMOD.Studio.EVENT_CALLBACK_TYPE.ALL);
        musicInstance.clearHandle();
        beatHooked = false;
        lastBeatTime = float.NegativeInfinity;
        lock (BeatLock) PendingBeats.Clear();
    }

    private void ProcessBeats()
    {
        while (true)
        {
            BeatEvent evt;
            lock (BeatLock)
            {
                if (PendingBeats.Count == 0) return;
                evt = PendingBeats.Dequeue();
            }

            if (!beatHooked || evt.instance != musicInstance.handle) continue;

            beatCount++;
            beatInBar = evt.beat;
            if (evt.tempo > 0f) musicTempo = evt.tempo;
            if (evt.beatsPerBar > 0) musicBeatsPerBar = evt.beatsPerBar;
            float age = (float)(BeatClock.Elapsed.TotalSeconds - evt.stamp);
            lastBeatTime = Time.unscaledTime - age + audioLatency + beatOffset;
            if (debugBeats) Debug.Log($"[Credits] Beat {evt.beat}/{evt.beatsPerBar}", this);
        }
    }

    private IEnumerator WaitForBeat()
    {
        chainedLineStart = -1f;
        if (!syncToBeat || skipping || !BeatActive || musicTempo <= 0f) yield break;

        float target = NextBeatTime(Time.unscaledTime, syncUnit);
        if (target - Time.unscaledTime > maxBeatWait) yield break;

        while (!skipping && Time.unscaledTime < target) yield return null;
        chainedLineStart = target;
    }

    private IEnumerator WaitUntil(float time)
    {
        while (!skipping && Time.unscaledTime < time)
        {
            if (revealSection) yield break;
            if (ConsumeTap())
            {
                revealSection = true;
                yield break;
            }

            yield return null;
        }
    }

    public void Play()
    {
        if (data == null)
        {
            Debug.LogWarning("[Credits] No CreditsData assigned", this);
            return;
        }

        if (playRoutine != null) return;
        playRoutine = StartCoroutine(PlayRoutine());
    }

    private bool IsBlipCharacter(TMP_Text text, int index)
    {
        if (index < 0 || index >= text.textInfo.characterCount) return false;
        char character = text.textInfo.characterInfo[index].character;
        return character != leaderChar && !char.IsWhiteSpace(character);
    }

    private void PlayTypeBlip()
    {
        var blip = RuntimeManager.CreateInstance(typeBlip);
        blip.setVolume(typeBlipVolume);
        blip.start();
        blip.release();
    }

    private IEnumerator PlayRoutine()
    {
        skipping = false;
        skipHeld = 0f;
        ignoreSkipUntilRelease = false;
        tapRequested = false;
        revealSection = false;
        UnhookMusic();
        ClearAll();
        AcquireInput();
        FreezePlayer();

        if (!creditsMusic.IsNull) MusicManager.Instance?.SwitchMusic(creditsMusic);
        lastPromptInput = float.NegativeInfinity;
        if (promptGroup != null) promptGroup.alpha = 0f;
        if (promptRoot != null) promptRoot.SetActive(true);
        terminalTargetWidth = autoLineWidth ? ComputeTerminalWidth() : 0f;

        FadeCanvasController fader = FadeCanvasController.Instance;
        if (fader != null && fader.Group != null && fader.Group.alpha < 0.99f)
            yield return fader.FadeOut(screenFadeDuration);

        SetBackgroundsActive(true);
        yield return null;
        yield return null;

        if (fader != null) yield return fader.FadeIn(screenFadeDuration);

        yield return WaitForBeat();
        yield return FadeGroup(rootGroup, 1f, fadeInDuration);

        bool firstSection = true;
        foreach (CreditsData.Section section in data.sections)
        {
            if (skipping) break;
            if (section == null) continue;

            if (!firstSection) yield return WaitForBeat();
            firstSection = false;

            if (section.layout == CreditsData.Layout.Terminal)
                yield return PlayTerminalSection(section);
            else
                yield return PlayCardSection(section.heading, section.lines ?? new List<CreditsData.Line>(), section.holdTime);

            revealSection = false;
            if (!skipping) yield return WaitOrTap(sectionPause);
        }

        bool fromTitle = GameManager.Instance != null && GameManager.Instance.CreditsFromTitle;
        if (!skipping && data.showStats && !fromTitle)
        {
            yield return HideTerminal();
            yield return WaitForBeat();
            yield return PlayCardSection(data.statsHeading, BuildStatsLines(), data.statsHoldTime, holdStatsUntilSkip);
        }

        if (promptRoot != null) promptRoot.SetActive(false);
        yield return WaitForBeat();
        yield return FadeGroup(rootGroup, 0f, fadeOutDuration);
        UnhookMusic();

        bool inGame = GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Game;
        bool standalone = SceneManager.sceneCount <= 1 && GameManager.Instance != null;
        bool goingToTitle = returnToTitleOnFinish && (inGame || standalone);

        if (!musicAfterFinish.IsNull) MusicManager.Instance?.SwitchMusic(musicAfterFinish);

        fader = FadeCanvasController.Instance;
        if (fader != null) yield return fader.FadeOut(screenFadeDuration);

        if (!goingToTitle) SetBackgroundsActive(false);

        ClearAll();
        ReleaseInput();
        ReleasePlayer();
        playRoutine = null;

        onFinished?.Invoke();

        if (goingToTitle)
        {
            GameManager.Instance.ReturnToTitle();
            yield break;
        }

        bool managerRevealsScreen = GameManager.Instance != null && GameManager.Instance.CreditsOpen;
        if (!managerRevealsScreen && fader != null) fader.FadeIn(screenFadeDuration);

        if (unloadSceneOnFinish && gameObject.scene.isLoaded && SceneManager.sceneCount > 1) SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    private void SetBackgroundsActive(bool active)
    {
        if (backgrounds == null) return;

        foreach (ParallaxBackground background in backgrounds)
        {
            if (background == null) continue;
            if (active) background.SetForceVisible(true);
            background.gameObject.SetActive(active);
        }
    }

    private IEnumerator PlayTerminalSection(CreditsData.Section section)
    {
        if (terminalGroup != null && terminalGroup.alpha < 1f) yield return FadeGroup(terminalGroup, 1f, 0.3f);
        if (section.clearTerminal) ClearTerminal();

        TMP_Text heading = AddTerminalLine(headingPrefab, headingPrefix + section.heading);
        yield return TypeLine(heading);
        if (!revealSection && !BeatTiming) yield return WaitOrTap(linePause, true);

        foreach (CreditsData.Line line in section.lines)
        {
            if (skipping) yield break;
            if (line == null) continue;

            if (line.style == CreditsData.LineStyle.Spacer)
            {
                AddSpacer(terminalContent, terminalLines);
                TrimTerminal();
                continue;
            }

            if (line.style == CreditsData.LineStyle.Images) continue;

            TMP_Text prefab = line.style == CreditsData.LineStyle.SmallPrint && smallPrintPrefab != null ? smallPrintPrefab : linePrefab;
            if (BeatTiming && !revealSection && chainedLineStart > 0f) yield return WaitUntil(chainedLineStart);
            TMP_Text text = AddTerminalLine(prefab, linePrefix + FormatTerminalLine(line));
            yield return TypeLine(text);
            if (!revealSection && !BeatTiming) yield return WaitOrTap(linePause, true);
        }

        if (revealSection) tapRequested = false;
        chainedLineStart = -1f;
        if (section.holdTime > 0f) yield return WaitOrTap(section.holdTime);
    }

    private IEnumerator PlayCardSection(string heading, List<CreditsData.Line> lines, float holdTime, bool waitForSkipPress = false)
    {
        yield return HideTerminal();
        ClearCard();

        if (!string.IsNullOrEmpty(heading)) AddCardLine(headingPrefab, heading);

        foreach (CreditsData.Line line in lines)
        {
            if (line == null) continue;

            if (line.style == CreditsData.LineStyle.Spacer)
            {
                AddSpacer(cardContent, cardLines);
                continue;
            }

            if (line.style == CreditsData.LineStyle.Images)
            {
                AddImageRow(cardContent, cardLines, line);
                continue;
            }

            TMP_Text prefab = line.style == CreditsData.LineStyle.SmallPrint && smallPrintPrefab != null ? smallPrintPrefab : linePrefab;
            AddCardLine(prefab, FormatCardLine(line));
        }

        if (!cardSound.IsNull) AudioManager.PlaySFX(cardSound);
        yield return FadeGroup(cardGroup, 1f, cardFadeInDuration, true);

        if (waitForSkipPress) yield return WaitForSkipPress();
        else yield return WaitOrTap(holdTime);
        yield return WaitForBeat();
        yield return FadeGroup(cardGroup, 0f, cardFadeOutDuration);
        ClearCard();
    }

    private IEnumerator TypeLine(TMP_Text text)
    {
        if (text == null) yield break;

        RebuildLayout(text);
        int total = text.textInfo.characterCount;
        float shown = 0f;
        int lastBlip = 0;
        text.maxVisibleCharacters = 0;
        cursorTyping = true;

        PlaceCursor(text, 0);

        bool onBeat = BeatTiming && total > 0 && !revealSection;
        float startTime = Time.unscaledTime;
        float duration = 0f;

        if (onBeat)
        {
            startTime = chainedLineStart > 0f && chainedLineStart >= Time.unscaledTime - BeatLength * 0.5f
                ? chainedLineStart
                : NextBeatTime(Time.unscaledTime, BeatUnit.Beat);
            duration = LineBeatCount(total) * BeatLength;

            yield return WaitUntil(startTime);
            chainedLineStart = startTime + duration + lineGapBeats * BeatLength;
        }

        while (shown < total)
        {
            if (skipping) break;

            if (revealSection || ConsumeTap())
            {
                revealSection = true;
                break;
            }

            shown = onBeat
                ? total * Mathf.Clamp01((Time.unscaledTime - startTime) / duration)
                : shown + charsPerSecond * Time.unscaledDeltaTime;
            int visible = Mathf.Min(total, Mathf.FloorToInt(shown));
            text.maxVisibleCharacters = visible;
            PlaceCursor(text, visible);

            if (!typeBlip.IsNull && visible - lastBlip >= blipEveryChars && IsBlipCharacter(text, visible - 1))
            {
                lastBlip = visible;
                PlayTypeBlip();
            }

            yield return null;
        }

        text.maxVisibleCharacters = total;
        RebuildLayout(text);
        PlaceCursor(text, total);
        cursorTyping = false;

        if (pulseOnLineComplete && !skipping && !revealSection)
        {
            if (onBeat) StartCoroutine(PulseLine(text));
            else yield return PulseLine(text);
        }

        if (revealSection) chainedLineStart = -1f;
    }

    private int LineBeatCount(int characters)
    {
        if (lineBeats == LineBeats.Fixed) return beatsPerLine;

        float naturalSeconds = characters / Mathf.Max(1f, charsPerSecond);
        int beats = Mathf.RoundToInt(naturalSeconds / Mathf.Max(0.01f, BeatLength));
        return Mathf.Clamp(beats, 1, maxBeatsPerLine);
    }

    private IEnumerator PulseLine(TMP_Text text)
    {
        float elapsed = 0f;

        while (elapsed < pulseDuration && !skipping)
        {
            if (text == null) yield break;

            if (ConsumeTap())
            {
                revealSection = true;
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / pulseDuration);
            float strength = t < pulseRise ? t / pulseRise : 1f - (t - pulseRise) / (1f - pulseRise);
            ApplyBrightness(text, Mathf.SmoothStep(0f, 1f, strength));
            yield return null;
        }

        if (text != null) ApplyBrightness(text, 0f);
    }

    private void ApplyBrightness(TMP_Text text, float strength)
    {
        text.ForceMeshUpdate();
        if (strength <= 0f) return;

        TMP_TextInfo info = text.textInfo;
        float multiplier = Mathf.Lerp(1f, pulseBrightness, strength);
        float whiten = pulseWhiten * strength;

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo character = info.characterInfo[i];
            if (!character.isVisible) continue;

            Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
            int v = character.vertexIndex;
            for (int k = 0; k < 4; k++) colors[v + k] = Brighten(colors[v + k], multiplier, whiten);
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private static Color32 Brighten(Color32 color, float multiplier, float whiten)
    {
        Color c = color;
        Color bright = new Color(Mathf.Clamp01(c.r * multiplier), Mathf.Clamp01(c.g * multiplier), Mathf.Clamp01(c.b * multiplier), c.a);
        bright = Color.Lerp(bright, new Color(1f, 1f, 1f, c.a), whiten);
        return bright;
    }

    private void RebuildLayout(TMP_Text text)
    {
        RectTransform parent = text.rectTransform.parent as RectTransform;
        if (parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        text.ForceMeshUpdate();
    }

    private void PlaceCursor(TMP_Text text, int visible)
    {
        if (!showCursor || text == null) return;

        TMP_TextInfo info = text.textInfo;
        if (info == null || info.characterCount == 0)
        {
            HideCursor();
            return;
        }

        if (cursor == null) cursor = CreateCursor();

        RectTransform rect = cursor.rectTransform;
        if (rect.parent != text.rectTransform)
        {
            rect.SetParent(text.rectTransform, false);
            rect.anchorMin = rect.anchorMax = text.rectTransform.pivot;
            rect.pivot = Vector2.zero;
        }

        int index = Mathf.Clamp(visible - 1, 0, info.characterCount - 1);
        TMP_CharacterInfo reference = info.characterInfo[index];

        float width = cursorWidth > 0f ? cursorWidth : reference.xAdvance - reference.origin;
        if (width <= 0f) width = reference.pointSize * 0.5f;

        float height = reference.ascender - reference.descender;
        float x = visible <= 0 ? info.characterInfo[0].origin : reference.xAdvance + cursorGap;

        rect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(reference.descender));
        rect.sizeDelta = new Vector2(Mathf.Round(width), Mathf.Round(height));
        cursor.color = useTextColorForCursor ? text.color : cursorColor;
        cursor.gameObject.SetActive(true);
        cursor.enabled = true;
    }

    private Image CreateCursor()
    {
        GameObject go = new GameObject("Cursor", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        Image image = go.GetComponent<Image>();
        image.sprite = cursorSprite;
        image.raycastTarget = false;
        return image;
    }

    private void HideCursor()
    {
        if (cursor != null) cursor.gameObject.SetActive(false);
    }

    private void UpdateCursorBlink()
    {
        if (cursor == null || !cursor.gameObject.activeSelf) return;
        cursor.enabled = cursorTyping || Mathf.Repeat(Time.unscaledTime, cursorBlinkInterval * 2f) < cursorBlinkInterval;
    }

    private bool ConsumeTap()
    {
        if (!tapRequested) return false;
        tapRequested = false;
        return true;
    }

    private IEnumerator WaitForSkipPress()
    {
        if (skipAction == null || skipAction.action == null) yield break;

        yield return null;
        tapRequested = false;

        while (!skipping && !ConsumeTap())
        {
            lastPromptInput = Time.unscaledTime;
            yield return null;
        }

        skipHeld = 0f;
        ignoreSkipUntilRelease = true;
    }

    private IEnumerator WaitOrTap(float seconds, bool tapRevealsSection = false)
    {
        float t = 0f;
        while (t < seconds && !skipping)
        {
            if (ConsumeTap())
            {
                if (tapRevealsSection) revealSection = true;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeGroup(CanvasGroup group, float target, float duration, bool tapCompletes = false)
    {
        if (group == null) yield break;

        float start = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (tapCompletes && ConsumeTap()) break;
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        group.alpha = target;
    }

    private IEnumerator HideTerminal()
    {
        if (terminalGroup == null || terminalGroup.alpha <= 0f) yield break;

        yield return FadeGroup(terminalGroup, 0f, 0.3f);
        ClearTerminal();
    }

    private void Update()
    {
        if (playRoutine == null) return;

        TryHookMusic();
        ProcessBeats();

        bool holding = skipAction != null && skipAction.action != null && skipAction.action.IsPressed();
        if (!skipping && skipAction != null && skipAction.action != null && skipAction.action.WasPressedThisFrame()) tapRequested = true;

        if (ignoreSkipUntilRelease && !holding) ignoreSkipUntilRelease = false;
        skipHeld = holding && !ignoreSkipUntilRelease ? skipHeld + Time.unscaledDeltaTime : 0f;
        if (skipFill != null) skipFill.fillAmount = skipHoldDuration > 0f ? Mathf.Clamp01(skipHeld / skipHoldDuration) : 0f;

        if (!skipping && holding && skipHeld >= skipHoldDuration) skipping = true;

        UpdatePromptFade(holding);
        UpdateCursorBlink();
    }

    private void UpdatePromptFade(bool pressed)
    {
        if (promptGroup == null) return;

        if (pressed) lastPromptInput = Time.unscaledTime;

        bool show = Time.unscaledTime - lastPromptInput <= promptVisibleTime;
        float duration = show ? promptFadeIn : promptFadeOut;
        float target = show ? 1f : 0f;

        promptGroup.alpha = duration > 0f
            ? Mathf.MoveTowards(promptGroup.alpha, target, Time.unscaledDeltaTime / duration)
            : target;
    }

    private TMP_Text AddTerminalLine(TMP_Text prefab, string content)
    {
        TMP_Text text = CreateLine(prefab, terminalContent, content, terminalLines);
        if (text != null) text.maxVisibleCharacters = 0;
        TrimTerminal();
        return text;
    }

    private void AddCardLine(TMP_Text prefab, string content)
    {
        CreateLine(prefab, cardContent, content, cardLines);
    }

    private TMP_Text CreateLine(TMP_Text prefab, RectTransform parent, string content, List<GameObject> owner)
    {
        if (prefab == null || parent == null) return null;

        TMP_Text text = Instantiate(prefab, parent);
        text.gameObject.SetActive(true);
        text.text = content;
        if (overrideTextColors) text.color = ColorFor(prefab);
        owner.Add(text.gameObject);
        return text;
    }

    private Color ColorFor(TMP_Text prefab)
    {
        if (prefab == headingPrefab) return headingColor;
        if (prefab == smallPrintPrefab) return smallPrintColor;
        return lineColor;
    }

    private void AddImageRow(RectTransform parent, List<GameObject> owner, CreditsData.Line line)
    {
        if (parent == null || line.images == null || line.images.Length == 0) return;

        GameObject row = new GameObject("Images", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = line.imageSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        row.GetComponent<LayoutElement>().minHeight = line.imageHeight;
        row.GetComponent<LayoutElement>().preferredHeight = line.imageHeight;

        foreach (Sprite sprite in line.images)
        {
            if (sprite == null) continue;

            GameObject logo = new GameObject(sprite.name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            logo.transform.SetParent(row.transform, false);

            Image image = logo.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            LayoutElement element = logo.GetComponent<LayoutElement>();
            element.preferredHeight = line.imageHeight;
            element.preferredWidth = line.imageHeight * aspect;
        }

        owner.Add(row);
    }

    private void AddSpacer(RectTransform parent, List<GameObject> owner)
    {
        if (parent == null) return;

        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().minHeight = spacerHeight;
        owner.Add(spacer);
    }

    private void TrimTerminal()
    {
        while (terminalLines.Count > maxTerminalLines)
        {
            if (cursor != null && terminalLines[0] != null && cursor.transform.IsChildOf(terminalLines[0].transform))
            {
                HideCursor();
                cursor.transform.SetParent(transform, false);
            }

            if (terminalLines[0] != null) Destroy(terminalLines[0]);
            terminalLines.RemoveAt(0);
        }
    }

    private void ClearTerminal()
    {
        HideCursor();
        if (cursor != null && cursor.transform.parent != null) cursor.transform.SetParent(transform, false);
        ClearList(terminalLines);
    }
    private void ClearCard() => ClearList(cardLines);

    private void ClearList(List<GameObject> list)
    {
        foreach (GameObject go in list)
            if (go != null) Destroy(go);
        list.Clear();
    }

    private void ClearAll()
    {
        ClearTerminal();
        ClearCard();
        if (terminalGroup != null) terminalGroup.alpha = 0f;
        if (cardGroup != null) cardGroup.alpha = 0f;
        if (skipFill != null) skipFill.fillAmount = 0f;
    }

    private string FormatTerminalLine(CreditsData.Line line)
    {
        switch (line.style)
        {
            case CreditsData.LineStyle.RoleAndName:
                int dots = terminalTargetWidth > 0f
                    ? LeaderDotsFor(line.role, line.name)
                    : Mathf.Max(minLeaderDots, lineWidth - VisibleLength(line.role) - VisibleLength(line.name) - 2);
                return $"{Role(line.role)} {new string(leaderChar, dots)} {Name(line.name)}{statusSuffix}";
            case CreditsData.LineStyle.Name:
                return Name(line.name) + statusSuffix;
            default:
                return string.IsNullOrEmpty(line.name) ? line.role : line.name;
        }
    }

    private string FormatCardLine(CreditsData.Line line)
    {
        switch (line.style)
        {
            case CreditsData.LineStyle.RoleAndName:
                return string.IsNullOrEmpty(line.role) ? Name(line.name) : $"<size=70%>{Role(line.role)}</size>\n{Name(line.name)}";
            case CreditsData.LineStyle.Name:
                return Name(line.name);
            default:
                return string.IsNullOrEmpty(line.name) ? line.role : line.name;
        }
    }

    private string Role(string text) => colorRoleAndName ? Tint(text, roleColor) : text;
    private string Name(string text) => colorRoleAndName ? Tint(text, nameColor) : text;

    private static string Tint(string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
    }

    private float ComputeTerminalWidth()
    {
        float widest = 0f;

        foreach (CreditsData.Section section in data.sections)
        {
            if (section == null || section.layout != CreditsData.Layout.Terminal || section.lines == null) continue;

            foreach (CreditsData.Line line in section.lines)
            {
                if (line == null || line.style != CreditsData.LineStyle.RoleAndName) continue;
                widest = Mathf.Max(widest, Measure(LeaderLine(line.role, line.name, minLeaderDots)));
            }
        }

        return widest;
    }

    private int LeaderDotsFor(string role, string name)
    {
        float dotWidth = Mathf.Max(0.01f, Measure(new string(leaderChar, 10)) / 10f);
        int dots = minLeaderDots;

        while (dots < 300 && Measure(LeaderLine(role, name, dots + 1)) <= terminalTargetWidth + dotWidth * 0.5f)
            dots++;

        return dots;
    }

    private string LeaderLine(string role, string name, int dots) => $"{role} {new string(leaderChar, dots)} {name}";

    private float Measure(string content)
    {
        if (measurer == null)
        {
            if (linePrefab == null || terminalContent == null) return VisibleLength(content);

            measurer = Instantiate(linePrefab, terminalContent);
            measurer.name = "Measurer";
            LayoutElement layout = measurer.GetComponent<LayoutElement>();
            if (layout == null) layout = measurer.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            measurer.color = Color.clear;
            measurer.raycastTarget = false;
            measurer.gameObject.SetActive(true);
        }

        return measurer.GetPreferredValues(content).x;
    }

    private static int VisibleLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int count = 0;
        bool inTag = false;
        foreach (char c in text)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) count++;
        }

        return count;
    }

    private List<CreditsData.Line> BuildStatsLines()
    {
        return new List<CreditsData.Line>
        {
            Stat(data.timeLabel, RunStats.FormatTime(RunStats.TimePlayed)),
            Stat(data.enemiesLabel, RunStats.EnemiesDefeated.ToString()),
            Stat(data.deathsLabel, RunStats.Deaths.ToString()),
            Stat(data.epicsLabel, RunStats.EpicsFound.ToString()),
            Stat(data.legendariesLabel, RunStats.LegendariesFound.ToString())
        };
    }

    private static CreditsData.Line Stat(string label, string value)
    {
        return new CreditsData.Line { style = CreditsData.LineStyle.RoleAndName, role = label, name = value };
    }

    private void AcquireInput()
    {
        enabledSkip = EnableIfNeeded(skipAction);
    }

    private void ReleaseInput()
    {
        if (enabledSkip && skipAction != null && skipAction.action != null) skipAction.action.Disable();
        enabledSkip = false;
    }

    private static bool EnableIfNeeded(InputActionReference reference)
    {
        if (reference == null || reference.action == null || reference.action.enabled) return false;
        reference.action.Enable();
        return true;
    }

    private void FreezePlayer()
    {
        if (!freezePlayer || playerFrozen) return;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null || player.Controller == null) return;

        player.Controller.FreezeMovement(true);
        playerFrozen = true;
    }

    private void ReleasePlayer()
    {
        if (!playerFrozen) return;
        playerFrozen = false;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player != null && player.Controller != null) player.Controller.FreezeMovement(false);
    }
}