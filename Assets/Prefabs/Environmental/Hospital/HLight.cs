using System.Collections;
using UnityEngine;

public class HLight : MonoBehaviour, IOnOff
{
    public enum LightMode { On, Off, Flicker }

    [System.Serializable]
    public struct LightState
    {
        public Color bulbColor;
        [Range(0f, 1f)] public float coverAlpha;
        [Range(0f, 1f)] public float coneAlpha;
        [Range(0f, 1f)] public float darkenAlpha;
    }

    [Header("Sprites")]
    [SerializeField] private SpriteRenderer bulb;
    [SerializeField] private SpriteRenderer cover;
    [SerializeField] private SpriteRenderer cone;
    [SerializeField] private SpriteRenderer darken;

    [Header("Mode")]
    [SerializeField] private LightMode mode = LightMode.On;
    [Tooltip("Which mode SetOn(true) switches to. Set to Flicker to have a switch turn this into a flickering light rather than a steady one.")]
    [SerializeField] private LightMode onSwitchMode = LightMode.On;

    [Header("States")]
    [SerializeField] private LightState onState = new()
    {
        bulbColor = Color.white,
        coverAlpha = 0.5f,
        coneAlpha = 0.5f,
        darkenAlpha = 0f
    };

    [SerializeField] private LightState offState = new()
    {
        bulbColor = new Color(0.5f, 0.5f, 0.5f, 1f),
        coverAlpha = 0.2f,
        coneAlpha = 0f,
        darkenAlpha = 0.6f
    };

    [Header("Flicker")]
    [SerializeField] private Vector2 steadyTimeRange = new(0.5f, 3f);
    [SerializeField] private Vector2Int flickersPerBurst = new(2, 6);
    [SerializeField] private Vector2 flickerOffTimeRange = new(0.02f, 0.08f);
    [SerializeField] private Vector2 flickerOnTimeRange = new(0.03f, 0.12f);

    private Coroutine flickerRoutine;

    public LightMode Mode => mode;
    public bool IsOn => mode != LightMode.Off;

    private void OnEnable()
    {
        SetMode(mode);
    }

    public void SetMode(LightMode newMode)
    {
        mode = newMode;

        if (flickerRoutine != null)
        {
            StopCoroutine(flickerRoutine);
            flickerRoutine = null;
        }

        if (!isActiveAndEnabled) return;

        switch (mode)
        {
            case LightMode.On:
                ApplyState(onState);
                break;
            case LightMode.Off:
                ApplyState(offState);
                break;
            case LightMode.Flicker:
                flickerRoutine = StartCoroutine(FlickerRoutine());
                break;
        }
    }

    public void SetOn(bool on)
    {
        SetMode(on ? onSwitchMode : LightMode.Off);
    }

    private IEnumerator FlickerRoutine()
    {
        ApplyState(onState);

        while (true)
        {
            yield return new WaitForSeconds(Random.Range(steadyTimeRange.x, steadyTimeRange.y));

            int flickers = Random.Range(flickersPerBurst.x, flickersPerBurst.y + 1);
            for (int i = 0; i < flickers; i++)
            {
                ApplyState(offState);
                yield return new WaitForSeconds(Random.Range(flickerOffTimeRange.x, flickerOffTimeRange.y));

                ApplyState(onState);
                yield return new WaitForSeconds(Random.Range(flickerOnTimeRange.x, flickerOnTimeRange.y));
            }
        }
    }

    private void ApplyState(LightState state)
    {
        if (bulb != null) bulb.color = state.bulbColor;
        SetAlpha(cover, state.coverAlpha);
        SetAlpha(cone, state.coneAlpha);
        SetAlpha(darken, state.darkenAlpha);
    }

    private static void SetAlpha(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;

        Color color = sr.color;
        color.a = alpha;
        sr.color = color;
    }

#if UNITY_EDITOR
    private bool dirty;

    private void OnValidate()
    {
        if (Application.isPlaying)
            dirty = true;
        else
            ApplyState(mode == LightMode.Off ? offState : onState);
    }

    private void Update()
    {
        if (!dirty) return;
        dirty = false;
        SetMode(mode);
    }
#endif
}