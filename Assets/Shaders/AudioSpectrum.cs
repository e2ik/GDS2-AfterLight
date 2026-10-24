using System;
using System.Runtime.InteropServices;
using FMODUnity;
using UnityEngine;

public class AudioSpectrum : MonoBehaviour
{
    public enum FFTSize { Size512 = 512, Size1024 = 1024, Size2048 = 2048, Size4096 = 4096 }

    [Header("Source")]
    [SerializeField] private string busPath = "bus:/";

    [Header("Analysis")]
    [SerializeField, Range(8, 128)] private int bandCount = 64;
    [SerializeField] private FFTSize fftSize = FFTSize.Size2048;
    [SerializeField, Min(1f)] private float minFrequency = 40f;
    [SerializeField, Min(1f)] private float maxFrequency = 12000f;
    [SerializeField] private float minDecibels = -70f;
    [SerializeField] private float maxDecibels = -12f;
    [SerializeField, Min(0f)] private float gain = 1f;
    [SerializeField, Min(0f)] private float attackSpeed = 30f;
    [SerializeField, Min(0f)] private float releaseSpeed = 6f;

    [Header("Beat")]
    [SerializeField, Min(1f)] private float bassMinFrequency = 40f;
    [SerializeField, Min(1f)] private float bassMaxFrequency = 150f;
    [SerializeField, Min(1f)] private float beatThreshold = 1.4f;
    [SerializeField, Range(0f, 1f)] private float minBeatLevel = 0.15f;
    [SerializeField, Min(0f)] private float beatCooldown = 0.15f;
    [SerializeField, Min(0f)] private float beatDecay = 4f;
    [SerializeField, Min(0f)] private float averageSpeed = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugBeats = false;

    private static readonly int SpectrumTexId = Shader.PropertyToID("_AudioSpectrumTex");
    private static readonly int BeatId = Shader.PropertyToID("_AudioBeat");
    private static readonly int LevelId = Shader.PropertyToID("_AudioLevel");
    private static readonly int ActiveId = Shader.PropertyToID("_AudioActive");

    private FMOD.DSP fft;
    private FMOD.ChannelGroup channelGroup;
    private FMOD.Studio.Bus bus;
    private bool busLocked;
    private bool attached;
    private bool warnedBus;
    private float sampleRate = 48000f;

    private float[] channelBuffer = Array.Empty<float>();
    private float[] mixBuffer = Array.Empty<float>();
    private float[] bands;
    private Texture2D spectrumTexture;
    private Color32[] pixels;

    private float beat;
    private float level;
    private float bassAverage;
    private float cooldownTimer;

    public float Beat => beat;
    public float Level => level;
    public float GetBand(int index) => bands != null && index >= 0 && index < bands.Length ? bands[index] : 0f;

    private void OnEnable()
    {
        bands = new float[bandCount];
        pixels = new Color32[bandCount];

        spectrumTexture = new Texture2D(bandCount, 1, TextureFormat.RGBA32, false, true)
        {
            name = "AudioSpectrum",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        WriteTexture();
        Shader.SetGlobalTexture(SpectrumTexId, spectrumTexture);
        Shader.SetGlobalFloat(ActiveId, 1f);
    }

    private void OnDisable()
    {
        Detach();
        Shader.SetGlobalFloat(ActiveId, 0f);
        Shader.SetGlobalFloat(BeatId, 0f);
        Shader.SetGlobalFloat(LevelId, 0f);

        if (spectrumTexture != null) Destroy(spectrumTexture);
        spectrumTexture = null;
    }

    private void Update()
    {
        if (!attached) TryAttach();

        float dt = Time.unscaledDeltaTime;
        bool analysed = attached && Analyse(dt);
        if (!analysed) DecayAll(dt);

        beat = Mathf.Max(0f, beat - beatDecay * dt);
        cooldownTimer -= dt;

        WriteTexture();
        Shader.SetGlobalFloat(BeatId, beat);
        Shader.SetGlobalFloat(LevelId, level);
    }

    private void TryAttach()
    {
        if (!fft.hasHandle())
        {
            if (RuntimeManager.CoreSystem.createDSPByType(FMOD.DSP_TYPE.FFT, out fft) != FMOD.RESULT.OK) return;
            fft.setParameterInt((int)FMOD.DSP_FFT.WINDOWSIZE, (int)fftSize);
        }

        if (!busLocked)
        {
            if (RuntimeManager.StudioSystem.getBus(busPath, out bus) != FMOD.RESULT.OK)
            {
                if (!warnedBus) Debug.LogWarning($"[AudioSpectrum] Bus '{busPath}' not found yet.", this);
                warnedBus = true;
                return;
            }

            bus.lockChannelGroup();
            busLocked = true;
        }

        if (bus.getChannelGroup(out channelGroup) != FMOD.RESULT.OK) return;
        if (channelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, fft) != FMOD.RESULT.OK) return;

        RuntimeManager.CoreSystem.getSoftwareFormat(out int rate, out FMOD.SPEAKERMODE _, out int _);
        if (rate > 0) sampleRate = rate;

        attached = true;
    }

    private void Detach()
    {
        if (attached && channelGroup.hasHandle() && fft.hasHandle()) channelGroup.removeDSP(fft);
        if (fft.hasHandle()) fft.release();
        if (busLocked && bus.isValid()) bus.unlockChannelGroup();

        fft.clearHandle();
        channelGroup.clearHandle();
        attached = false;
        busLocked = false;
    }

    private bool Analyse(float dt)
    {
        if (fft.getParameterData((int)FMOD.DSP_FFT.SPECTRUMDATA, out IntPtr pointer, out uint _) != FMOD.RESULT.OK) return false;

        FMOD.DSP_PARAMETER_FFT data = (FMOD.DSP_PARAMETER_FFT)Marshal.PtrToStructure(pointer, typeof(FMOD.DSP_PARAMETER_FFT));
        int bins = data.length;
        int channels = data.numchannels;
        if (bins <= 0 || channels <= 0) return false;

        if (channelBuffer.Length != bins) channelBuffer = new float[bins];
        if (mixBuffer.Length != bins) mixBuffer = new float[bins];
        Array.Clear(mixBuffer, 0, bins);

        for (int c = 0; c < channels; c++)
        {
            data.getSpectrum(c, ref channelBuffer);
            for (int i = 0; i < bins; i++) mixBuffer[i] += channelBuffer[i];
        }

        float inverseChannels = 1f / channels;
        for (int i = 0; i < bins; i++) mixBuffer[i] *= inverseChannels;

        float binHz = sampleRate / (bins * 2f);
        float low = Mathf.Max(1f, minFrequency);
        float high = Mathf.Max(low + 1f, maxFrequency);
        float total = 0f;

        for (int b = 0; b < bandCount; b++)
        {
            float f0 = low * Mathf.Pow(high / low, b / (float)bandCount);
            float f1 = low * Mathf.Pow(high / low, (b + 1) / (float)bandCount);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(f0 / binHz), 0, bins - 1);
            int i1 = Mathf.Clamp(Mathf.CeilToInt(f1 / binHz), i0, bins - 1);

            float peak = 0f;
            for (int i = i0; i <= i1; i++) peak = Mathf.Max(peak, mixBuffer[i]);

            float target = Mathf.Clamp01(Mathf.InverseLerp(minDecibels, maxDecibels, ToDecibels(peak)) * gain);
            float speed = target > bands[b] ? attackSpeed : releaseSpeed;
            bands[b] = Mathf.Lerp(bands[b], target, 1f - Mathf.Exp(-speed * dt));
            total += bands[b];
        }

        level = total / bandCount;

        int b0 = Mathf.Clamp(Mathf.FloorToInt(bassMinFrequency / binHz), 0, bins - 1);
        int b1 = Mathf.Clamp(Mathf.CeilToInt(bassMaxFrequency / binHz), b0, bins - 1);
        float bass = 0f;
        for (int i = b0; i <= b1; i++) bass += mixBuffer[i];
        bass /= (b1 - b0 + 1);

        float bassLevel = Mathf.Clamp01(Mathf.InverseLerp(minDecibels, maxDecibels, ToDecibels(bass)));
        if (cooldownTimer <= 0f && bassLevel >= minBeatLevel && bass > bassAverage * beatThreshold)
        {
            beat = 1f;
            cooldownTimer = beatCooldown;
            if (debugBeats) Debug.Log($"[AudioSpectrum] Beat {Time.time:F2} bass {bassLevel:F2}", this);
        }

        bassAverage = Mathf.Lerp(bassAverage, bass, 1f - Mathf.Exp(-averageSpeed * dt));
        return true;
    }

    private void DecayAll(float dt)
    {
        float total = 0f;
        float t = 1f - Mathf.Exp(-releaseSpeed * dt);
        for (int b = 0; b < bandCount; b++)
        {
            bands[b] = Mathf.Lerp(bands[b], 0f, t);
            total += bands[b];
        }
        level = total / bandCount;
    }

    private static float ToDecibels(float amplitude) => 20f * Mathf.Log10(Mathf.Max(amplitude, 1e-7f));

    private void WriteTexture()
    {
        if (spectrumTexture == null) return;

        for (int b = 0; b < bandCount; b++)
        {
            byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(bands[b]) * 255f);
            pixels[b] = new Color32(value, value, value, 255);
        }

        spectrumTexture.SetPixels32(pixels);
        spectrumTexture.Apply(false);
    }
}
