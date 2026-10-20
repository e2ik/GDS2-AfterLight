using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class LightRim : MonoBehaviour
{
    [SerializeField] private Material rimMaterial;
    [SerializeField] private Color tint = Color.white;
    [SerializeField, Range(0f, 10f)] private float intensity = 1.5f;
    [SerializeField, Range(1, 12)] private int depth = 3;
    [SerializeField, Range(0.25f, 4f)] private float fadePower = 1f;
    [SerializeField] private bool includeDiagonals = true;
    [SerializeField] private int sortingOrderOffset = 2;

    [Header("Light Sampling")]
    [SerializeField] private Vector2 sampleOffset = new Vector2(0f, 0.5f);
    [SerializeField, Range(0.25f, 4f)] private float falloffPower = 1.5f;
    [SerializeField, Range(0f, 1f)] private float minimumLight = 0.02f;
    [SerializeField, Min(0f)] private float maxBrightness = 1f;
    [SerializeField, Min(0f)] private float colorSmoothing = 12f;

    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int DepthId = Shader.PropertyToID("_RimDepth");
    private static readonly int FadePowerId = Shader.PropertyToID("_RimFadePower");
    private static readonly int DiagonalsId = Shader.PropertyToID("_RimDiagonals");

    private const string ChildName = "LightRim";

    private static readonly List<Light2D> lights = new List<Light2D>();
    private static bool lightsDirty = true;
    private static bool sceneHooksAdded;

    private SpriteRenderer source;
    private SpriteRenderer rim;
    private MaterialPropertyBlock block;
    private Color currentLight = Color.black;

    public Color CurrentLight => currentLight;

    public void SetIntensity(float value) => intensity = value;

    public static void MarkLightsDirty() => lightsDirty = true;

    private static void AddSceneHooks()
    {
        if (sceneHooksAdded) return;

        sceneHooksAdded = true;
        SceneManager.sceneLoaded += (scene, mode) => lightsDirty = true;
        SceneManager.sceneUnloaded += scene => lightsDirty = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lights.Clear();
        lightsDirty = true;
    }

    private void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        block ??= new MaterialPropertyBlock();
        if (rim == null) rim = CreateChild();
        AddSceneHooks();
        lightsDirty = true;
        Sync();
    }

    private void OnDisable()
    {
        if (rim == null) return;

        if (Application.isPlaying) Destroy(rim.gameObject);
        else DestroyImmediate(rim.gameObject);

        rim = null;
    }

    private SpriteRenderer CreateChild()
    {
        Transform existing = transform.Find(ChildName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        GameObject child = new GameObject(ChildName);
        child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        return child.AddComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        Color target = SampleLights();
        currentLight = !Application.isPlaying || colorSmoothing <= 0f
            ? target
            : Color.Lerp(currentLight, target, 1f - Mathf.Exp(-colorSmoothing * Time.deltaTime));

        Sync();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && rim != null) Sync();
    }

    private void Sync()
    {
        if (rim == null || source == null) return;

        rim.sharedMaterial = rimMaterial;
        rim.enabled = source.enabled && source.sprite != null && rimMaterial != null && intensity > 0f;
        if (!rim.enabled) return;

        rim.sprite = source.sprite;
        rim.flipX = source.flipX;
        rim.flipY = source.flipY;
        rim.sortingLayerID = source.sortingLayerID;
        rim.sortingOrder = source.sortingOrder + sortingOrderOffset;
        rim.drawMode = source.drawMode;
        if (source.drawMode != SpriteDrawMode.Simple) rim.size = source.size;
        rim.color = new Color(1f, 1f, 1f, source.color.a);

        Color rimColor = currentLight * tint * intensity * tint.a;
        rimColor.a = 1f;

        block ??= new MaterialPropertyBlock();
        rim.GetPropertyBlock(block);
        block.SetColor(RimColorId, rimColor);
        block.SetFloat(DepthId, depth);
        block.SetFloat(FadePowerId, fadePower);
        block.SetFloat(DiagonalsId, includeDiagonals ? 1f : 0f);
        rim.SetPropertyBlock(block);
    }

    private Color SampleLights()
    {
        RefreshLights();

        Vector2 point = (Vector2)transform.position + sampleOffset;
        int sortingLayer = source != null ? source.sortingLayerID : 0;
        Color total = Color.black;

        foreach (Light2D light in lights)
        {
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f) continue;
            if (light.lightType != Light2D.LightType.Point) continue;
            if (!TargetsLayer(light, sortingLayer)) continue;

            float attenuation = PointAttenuation(light, point);
            if (attenuation <= 0f) continue;

            total += light.color * (light.intensity * attenuation);
        }

        if (total.r < minimumLight && total.g < minimumLight && total.b < minimumLight) return Color.black;

        float peak = Mathf.Max(total.r, Mathf.Max(total.g, total.b));
        if (maxBrightness > 0f && peak > maxBrightness) total *= maxBrightness / peak;

        total.a = 1f;
        return total;
    }

    private float PointAttenuation(Light2D light, Vector2 point)
    {
        Vector2 toPoint = point - (Vector2)light.transform.position;
        float distance = toPoint.magnitude;

        float outer = Mathf.Max(light.pointLightOuterRadius, 0.0001f);
        if (distance >= outer) return 0f;

        float inner = Mathf.Clamp(light.pointLightInnerRadius, 0f, outer);
        float radial = distance <= inner ? 1f : 1f - (distance - inner) / Mathf.Max(outer - inner, 0.0001f);
        radial = Mathf.Pow(Mathf.Clamp01(radial), falloffPower);

        float outerAngle = light.pointLightOuterAngle;
        if (outerAngle < 360f && distance > 0.0001f)
        {
            float angle = Vector2.Angle(light.transform.up, toPoint);
            float halfOuter = outerAngle * 0.5f;
            float halfInner = Mathf.Min(light.pointLightInnerAngle * 0.5f, halfOuter);

            if (angle >= halfOuter) return 0f;
            if (angle > halfInner) radial *= 1f - (angle - halfInner) / Mathf.Max(halfOuter - halfInner, 0.0001f);
        }

        return radial;
    }

    private static bool TargetsLayer(Light2D light, int sortingLayerId)
    {
        int[] targets = light.targetSortingLayers;
        if (targets == null) return true;

        foreach (int id in targets)
            if (id == sortingLayerId) return true;

        return false;
    }

    private void RefreshLights()
    {
        if (Application.isPlaying && !lightsDirty) return;

        lightsDirty = false;
        lights.Clear();
        lights.AddRange(FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }
}