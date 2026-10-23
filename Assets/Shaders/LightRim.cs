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

    [Header("Direction")]
    [SerializeField, Range(0f, 1f)] private float directional = 1f;
    [SerializeField] private bool secondLight = true;

    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int DepthId = Shader.PropertyToID("_RimDepth");
    private static readonly int FadePowerId = Shader.PropertyToID("_RimFadePower");
    private static readonly int DiagonalsId = Shader.PropertyToID("_RimDiagonals");
    private static readonly int DirId = Shader.PropertyToID("_RimDir");
    private static readonly int Color2Id = Shader.PropertyToID("_RimColor2");
    private static readonly int Dir2Id = Shader.PropertyToID("_RimDir2");

    private struct LightSlot
    {
        public Color Color;
        public Vector2 Direction;
        public float Weight;
    }

    private readonly List<LightSlot> contributions = new List<LightSlot>();

    private const string ChildName = "LightRim";

    private static readonly List<Light2D> lights = new List<Light2D>();
    private static bool lightsDirty = true;
    private static bool sceneHooksAdded;

    private SpriteRenderer source;
    private SpriteRenderer rim;
    private MaterialPropertyBlock block;
    private Color currentLight = Color.black;
    private Vector2 currentDir = Vector2.up;
    private Color currentLight2 = Color.black;
    private Vector2 currentDir2 = Vector2.up;

    public Color CurrentLight => currentLight + currentLight2;

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
        SampleLights(out LightSlot first, out LightSlot second);

        bool instant = !Application.isPlaying || colorSmoothing <= 0f;
        float blend = instant ? 1f : 1f - Mathf.Exp(-colorSmoothing * Time.deltaTime);

        currentLight = Color.Lerp(currentLight, first.Color, blend);
        currentLight2 = Color.Lerp(currentLight2, second.Color, blend);
        currentDir = BlendDirection(currentDir, first.Direction, first.Weight, blend);
        currentDir2 = BlendDirection(currentDir2, second.Direction, second.Weight, blend);

        Sync();
    }

    private static Vector2 BlendDirection(Vector2 current, Vector2 target, float weight, float blend)
    {
        if (weight <= 0f) return current;
        Vector2 result = Vector2.Lerp(current, target, blend);
        return result.sqrMagnitude > 0.0001f ? result.normalized : target;
    }

    private Vector2 ToTextureDirection(Vector2 worldDir)
    {
        Vector2 local = transform.InverseTransformVector(worldDir);
        if (source != null && source.flipX) local.x = -local.x;
        if (source != null && source.flipY) local.y = -local.y;
        Vector2 dir = Vector2.Lerp(Vector2.up, local.sqrMagnitude > 0.0001f ? local.normalized : Vector2.up, directional);
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
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
        Color rimColor2 = secondLight ? currentLight2 * tint * intensity * tint.a : Color.black;
        rimColor2.a = 1f;
        Vector2 dir1 = ToTextureDirection(currentDir);
        Vector2 dir2 = ToTextureDirection(currentDir2);

        block ??= new MaterialPropertyBlock();
        rim.GetPropertyBlock(block);
        block.SetColor(RimColorId, rimColor);
        block.SetFloat(DepthId, depth);
        block.SetFloat(FadePowerId, fadePower);
        block.SetFloat(DiagonalsId, includeDiagonals ? 1f : 0f);
        block.SetVector(DirId, new Vector4(dir1.x, dir1.y, 0f, 0f));
        block.SetColor(Color2Id, rimColor2);
        block.SetVector(Dir2Id, new Vector4(dir2.x, dir2.y, 0f, 0f));
        rim.SetPropertyBlock(block);
    }

    private void SampleLights(out LightSlot first, out LightSlot second)
    {
        RefreshLights();

        Vector2 point = (Vector2)transform.position + sampleOffset;
        int sortingLayer = source != null ? source.sortingLayerID : 0;
        contributions.Clear();

        foreach (Light2D light in lights)
        {
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f) continue;
            if (light.lightType != Light2D.LightType.Point) continue;
            if (!TargetsLayer(light, sortingLayer)) continue;

            float attenuation = PointAttenuation(light, point);
            if (attenuation <= 0f) continue;

            Color color = light.color * (light.intensity * attenuation);
            Vector2 toLight = (Vector2)light.transform.position - point;

            contributions.Add(new LightSlot
            {
                Color = color,
                Direction = toLight.sqrMagnitude > 0.0001f ? toLight.normalized : Vector2.up,
                Weight = Mathf.Max(color.r, Mathf.Max(color.g, color.b))
            });
        }

        first = default;
        second = default;
        int firstIndex = -1;
        int secondIndex = -1;

        for (int i = 0; i < contributions.Count; i++)
        {
            if (firstIndex < 0 || contributions[i].Weight > contributions[firstIndex].Weight)
            {
                secondIndex = firstIndex;
                firstIndex = i;
            }
            else if (secondIndex < 0 || contributions[i].Weight > contributions[secondIndex].Weight)
            {
                secondIndex = i;
            }
        }

        if (firstIndex < 0) return;

        first = contributions[firstIndex];
        if (secondLight && secondIndex >= 0) second = contributions[secondIndex];

        for (int i = 0; i < contributions.Count; i++)
        {
            if (i == firstIndex || (secondLight && i == secondIndex)) continue;

            LightSlot extra = contributions[i];
            bool toSecond = secondLight && secondIndex >= 0
                            && Vector2.Dot(extra.Direction, second.Direction) > Vector2.Dot(extra.Direction, first.Direction);

            if (toSecond) Merge(ref second, extra);
            else Merge(ref first, extra);
        }

        Finish(ref first);
        Finish(ref second);
    }

    private static void Merge(ref LightSlot slot, LightSlot extra)
    {
        Vector2 dir = slot.Direction * slot.Weight + extra.Direction * extra.Weight;
        slot.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : slot.Direction;
        slot.Color += extra.Color;
        slot.Weight += extra.Weight;
    }

    private void Finish(ref LightSlot slot)
    {
        Color total = slot.Color;

        if (total.r < minimumLight && total.g < minimumLight && total.b < minimumLight)
        {
            slot.Color = Color.black;
            slot.Weight = 0f;
            return;
        }

        float peak = Mathf.Max(total.r, Mathf.Max(total.g, total.b));
        if (maxBrightness > 0f && peak > maxBrightness) total *= maxBrightness / peak;

        total.a = 1f;
        slot.Color = total;
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