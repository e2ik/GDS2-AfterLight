using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CloudLightFlicker : MonoBehaviour
{
    [SerializeField] private Light2D[] lights;
    [SerializeField] private float minIntensity = 0.75f;
    [SerializeField] private float maxIntensity = 1.5f;
    [SerializeField] private Vector2 transitionTimeRange = new Vector2(2f, 6f);
    [SerializeField] private Vector2 holdTimeRange = new Vector2(0.5f, 3f);
    [SerializeField] private bool randomStart = true;

    private float fromIntensity;
    private float toIntensity;
    private float transitionDuration;
    private float transitionTime;
    private float holdTimer;

    private void Awake()
    {
        if (lights == null || lights.Length == 0) lights = GetComponentsInChildren<Light2D>(true);
    }

    private void OnEnable()
    {
        float start = randomStart ? Random.Range(minIntensity, maxIntensity) : minIntensity;
        fromIntensity = start;
        toIntensity = start;
        transitionDuration = 0f;
        transitionTime = 0f;
        holdTimer = 0f;
        Apply(start);
    }

    private void Update()
    {
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            return;
        }

        if (transitionTime >= transitionDuration)
        {
            PickNextTarget();
            return;
        }

        transitionTime += Time.deltaTime;
        float t = Mathf.Clamp01(transitionTime / transitionDuration);
        float eased = t * t * (3f - 2f * t);
        Apply(Mathf.Lerp(fromIntensity, toIntensity, eased));

        if (t >= 1f)
            holdTimer = Random.Range(holdTimeRange.x, holdTimeRange.y);
    }

    private void PickNextTarget()
    {
        fromIntensity = toIntensity;
        toIntensity = Random.Range(minIntensity, maxIntensity);
        transitionDuration = Mathf.Max(0.01f, Random.Range(transitionTimeRange.x, transitionTimeRange.y));
        transitionTime = 0f;
    }

    private void Apply(float intensity)
    {
        foreach (Light2D light in lights)
        {
            if (light != null) light.intensity = intensity;
        }
    }
}