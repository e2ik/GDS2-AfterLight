using System.Collections.Generic;
using UnityEngine;

public class ScanlineSweep : MonoBehaviour
{
    public const int MaxLines = 8;

    [System.Serializable]
    public class ScanLine
    {
        [Min(0f)] public float height = 2f;
        public float speed = 20f;
        [Min(0f)] public float brightness = 1.3f;
        public float shiftPixels = 1f;
        [Range(0f, 1f)] public float startOffset;
    }

    [Header("References")]
    [SerializeField] private RectTransform area;

    [Header("Lines")]
    [SerializeField] private List<ScanLine> lines = new List<ScanLine>
    {
        new ScanLine { height = 2f, speed = 18f, brightness = 1.35f, shiftPixels = 1f, startOffset = 0f },
        new ScanLine { height = 4f, speed = 7f, brightness = 0.8f, shiftPixels = 0f, startOffset = 0.35f },
        new ScanLine { height = 1f, speed = 31f, brightness = 1.2f, shiftPixels = -1f, startOffset = 0.7f },
    };
    [SerializeField, Min(1f)] private float overlapBoost = 1.25f;
    [SerializeField, Min(0f)] private float snapStep = 0f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool drawLineGizmos;

    private static readonly int LinesId = Shader.PropertyToID("_MapScanlineLines");
    private static readonly int CountId = Shader.PropertyToID("_MapScanlineCount");
    private static readonly int AreaId = Shader.PropertyToID("_MapScanlineArea");
    private static readonly int OverlapId = Shader.PropertyToID("_MapScanlineOverlapBoost");

    private readonly Vector4[] lineData = new Vector4[MaxLines];
    private readonly Vector3[] corners = new Vector3[4];
    private readonly Vector2[] localBands = new Vector2[MaxLines];
    private float elapsed;

    private void OnEnable()
    {
        elapsed = 0f;
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(CountId, 0f);
    }

    private void Update()
    {
        if (area == null || lines == null || lines.Count == 0)
        {
            Shader.SetGlobalFloat(CountId, 0f);
            return;
        }

        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        Rect rect = area.rect;
        Canvas canvas = area.GetComponentInParent<Canvas>();
        Transform space = canvas != null ? canvas.rootCanvas.transform : null;

        area.GetWorldCorners(corners);
        Vector3 canvasMin = space != null ? space.InverseTransformPoint(corners[0]) : corners[0];
        Vector3 canvasMax = space != null ? space.InverseTransformPoint(corners[2]) : corners[2];

        int count = Mathf.Min(lines.Count, MaxLines);

        for (int i = 0; i < MaxLines; i++)
        {
            if (i >= count)
            {
                lineData[i] = Vector4.zero;
                continue;
            }

            ScanLine line = lines[i];
            float travel = rect.height + line.height;
            float distance = Mathf.Repeat(line.startOffset * travel + elapsed * line.speed, travel);
            if (snapStep > 0f) distance = Mathf.Round(distance / snapStep) * snapStep;

            float lineTop = rect.yMax + line.height - distance;
            float lineBottom = lineTop - line.height;
            localBands[i] = new Vector2(lineBottom, lineTop);

            float bottom01 = (lineBottom - rect.yMin) / rect.height;
            float top01 = (lineTop - rect.yMin) / rect.height;

            lineData[i] = new Vector4(
                Mathf.Lerp(canvasMin.y, canvasMax.y, bottom01),
                Mathf.Lerp(canvasMin.y, canvasMax.y, top01),
                line.brightness,
                line.shiftPixels);
        }

        Shader.SetGlobalVectorArray(LinesId, lineData);
        Shader.SetGlobalFloat(CountId, count);
        Shader.SetGlobalVector(AreaId, new Vector4(Mathf.Min(canvasMin.x, canvasMax.x), Mathf.Min(canvasMin.y, canvasMax.y), Mathf.Max(canvasMin.x, canvasMax.x), Mathf.Max(canvasMin.y, canvasMax.y)));
        Shader.SetGlobalFloat(OverlapId, overlapBoost);
    }

    private void OnDrawGizmos()
    {
        if (!drawLineGizmos || !Application.isPlaying || area == null || lines == null) return;

        Rect rect = area.rect;
        int count = Mathf.Min(lines.Count, MaxLines);

        for (int i = 0; i < count; i++)
        {
            Gizmos.color = lines[i].brightness >= 1f ? Color.green : Color.magenta;

            Vector3 a = area.TransformPoint(new Vector3(rect.xMin, localBands[i].x, 0f));
            Vector3 b = area.TransformPoint(new Vector3(rect.xMax, localBands[i].y, 0f));
            Gizmos.DrawWireCube((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 0f));
        }
    }
}