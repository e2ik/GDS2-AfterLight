using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ParallaxBackground : MonoBehaviour
{
    public enum ParallaxSource { Camera, Player }

    [System.Serializable]
    public class ParallaxLayer
    {
        public string name = "Layer";
        public Sprite sprite;
        public Material material;
        public Color color = Color.white;

        [Header("Movement")]
        public ParallaxSource source = ParallaxSource.Player;
        public Vector2 followCamera = new Vector2(0.5f, 0.5f);
        public Vector2 autoScroll;

        [Header("Placement")]
        public Vector2 offset;
        public float scale = 1f;
        public bool fitToCameraHeight;
        public bool tileX = true;
        public bool tileY;

        [Header("Sorting")]
        public string sortingLayer = "Default";
        public int orderInLayer;

        [Header("Tint")]
        public bool useAreaTint = true;

        [System.NonSerialized] public SpriteRenderer renderer;
        [System.NonSerialized] public Vector2 scroll;
        [System.NonSerialized] public Color baseColor;
        [System.NonSerialized] public Color targetColor;
    }

    [System.Serializable]
    public class LayerColor
    {
        public string layerName = "Layer";
        public Color color = Color.white;
    }

    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    [Header("Area Tint")]
    [SerializeField] private Color exteriorTint = Color.white;
    [SerializeField] private Color interiorTint = new Color(0.4f, 0.4f, 0.4f);

    [Header("Layer Colors")]
    [SerializeField, Min(0f)] private float colorBlendTime = 1f;

    [Header("World Anchor")]
    [SerializeField] private Vector2 worldOrigin = Vector2.zero;

    [Header("Behaviour")]
    [SerializeField] private bool onlyVisibleInGame = true;

    private Transform content;
    private Camera cam;
    private Color currentTint = Color.white;
    private GameManager subscribedManager;
    private bool hasAppliedSide;
    private AreaSide appliedSide;
    private IList<LayerColor> activeLayerColors;
    private bool forceVisible;

    public void SetForceVisible(bool visible) => forceVisible = visible;

    private void Awake()
    {
        content = new GameObject("Layers").transform;
        content.SetParent(transform, false);

        foreach (ParallaxLayer layer in layers)
        {
            GameObject layerObject = new GameObject(string.IsNullOrEmpty(layer.name) ? "Layer" : layer.name);
            layerObject.SetActive(false);
            layerObject.transform.SetParent(content, false);

            SpriteRenderer sr = layerObject.AddComponent<SpriteRenderer>();
            sr.sprite = layer.sprite;
            if (layer.material != null) sr.sharedMaterial = layer.material;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingLayerName = string.IsNullOrEmpty(layer.sortingLayer) ? "Default" : layer.sortingLayer;
            sr.sortingOrder = layer.orderInLayer;
            sr.color = layer.color;

            layer.renderer = sr;
            layer.baseColor = layer.color;
            layer.targetColor = layer.color;

            layerObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private float activeBlendTime = -1f;
    private float BlendTime => activeBlendTime >= 0f ? activeBlendTime : colorBlendTime;

    public void SetLayerColors(IList<LayerColor> colors, bool instant = false)
    {
        activeBlendTime = -1f;
        activeLayerColors = colors;
        RefreshLayerColors(instant);
    }

    public void ResetLayerColors(bool instant = false)
    {
        activeBlendTime = -1f;
        activeLayerColors = null;
        RefreshLayerColors(instant);
    }

    private IList<LayerColor> overrideLayerColors;

    public void SetOverrideColors(IList<LayerColor> colors, float blendTime = -1f)
    {
        activeBlendTime = blendTime;
        overrideLayerColors = colors;
        RefreshLayerColors(blendTime == 0f);
    }

    public void ClearOverrideColors(float blendTime = -1f)
    {
        if (overrideLayerColors == null) return;
        activeBlendTime = blendTime;
        overrideLayerColors = null;
        RefreshLayerColors(blendTime == 0f);
    }

    private void RefreshLayerColors(bool instant)
    {
        IList<LayerColor> colors = overrideLayerColors ?? activeLayerColors;

        foreach (ParallaxLayer layer in layers)
        {
            Color target = layer.color;

            if (colors != null)
            {
                foreach (LayerColor entry in colors)
                {
                    if (entry != null && entry.layerName == layer.name)
                    {
                        target = new Color(entry.color.r, entry.color.g, entry.color.b, layer.color.a);
                        break;
                    }
                }
            }

            layer.targetColor = target;
            if (instant || BlendTime <= 0f || content == null || !content.gameObject.activeSelf) layer.baseColor = target;
        }

        ApplyColors();
    }

    private void BlendLayerColors()
    {
        if (BlendTime <= 0f) return;

        float step = Time.unscaledDeltaTime / BlendTime;
        bool changed = false;

        foreach (ParallaxLayer layer in layers)
        {
            if (layer.baseColor == layer.targetColor) continue;
            layer.baseColor = Vector4.MoveTowards(layer.baseColor, layer.targetColor, step);
            changed = true;
        }

        if (changed) ApplyColors();
    }

    private void ApplyColors()
    {
        foreach (ParallaxLayer layer in layers)
        {
            if (layer.renderer == null) continue;
            layer.renderer.color = layer.useAreaTint ? layer.baseColor * currentTint : layer.baseColor;
        }
    }

    public void ResetParallax()
    {
    }

    public void SetTint(Color tint)
    {
        currentTint = tint;
        ApplyColors();
    }

    private void TrySubscribe()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager == subscribedManager) return;

        Unsubscribe();
        subscribedManager = manager;
        subscribedManager.OnAreaSideChanged += HandleAreaSideChanged;
        HandleAreaSideChanged(subscribedManager.CurrentAreaSide);
    }

    private void Unsubscribe()
    {
        if (subscribedManager != null) subscribedManager.OnAreaSideChanged -= HandleAreaSideChanged;
        subscribedManager = null;
    }

    private void HandleAreaSideChanged(AreaSide side)
    {
        appliedSide = side;
        hasAppliedSide = true;
        SetTint(side == AreaSide.Interior ? interiorTint : exteriorTint);
    }

    private void SyncAreaTint()
    {
        if (GameManager.Instance == null) return;

        AreaSide side = GameManager.Instance.CurrentAreaSide;
        if (!hasAppliedSide || side != appliedSide) HandleAreaSideChanged(side);
    }

    private void LateUpdate()
    {
        TrySubscribe();

        bool visible = forceVisible || !onlyVisibleInGame || (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Game);
        if (content.gameObject.activeSelf != visible)
        {
            content.gameObject.SetActive(visible);
            if (visible)
            {
                hasAppliedSide = false;
                RefreshLayerColors(true);
            }
        }
        if (!visible) return;

        SyncAreaTint();
        BlendLayerColors();

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return;

        Vector2 cameraPosition = cam.transform.position;
        Vector2 playerPosition = GetPlayerPosition(cameraPosition);

        float viewHeight = cam.orthographicSize * 2f;
        float viewWidth = viewHeight * cam.aspect;

        foreach (ParallaxLayer layer in layers)
        {
            bool followPlayer = layer.source == ParallaxSource.Player;
            Vector2 source = followPlayer ? new Vector2(playerPosition.x, cameraPosition.y) : cameraPosition;
            UpdateLayer(layer, cameraPosition, source, viewWidth, viewHeight);
        }
    }

    private Vector2 GetPlayerPosition(Vector2 cameraPosition)
    {
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
            return GameManager.Instance.Player.transform.position;

        return cameraPosition;
    }

    private void UpdateLayer(ParallaxLayer layer, Vector2 cameraPosition, Vector2 source, float viewWidth, float viewHeight)
    {
        SpriteRenderer sr = layer.renderer;
        if (sr == null || layer.sprite == null) return;

        if (sr.sprite != layer.sprite) sr.sprite = layer.sprite;

        Vector2 spriteSize = layer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        float scale = layer.scale > 0f ? layer.scale : 1f;
        if (layer.fitToCameraHeight) scale *= viewHeight / spriteSize.y;

        Vector2 tileSize = spriteSize * scale;

        int countX = layer.tileX ? MakeOdd(Mathf.CeilToInt(viewWidth / tileSize.x) + 2) : 1;
        int countY = layer.tileY ? MakeOdd(Mathf.CeilToInt(viewHeight / tileSize.y) + 2) : 1;

        Vector3 parentScale = content.lossyScale;
        sr.transform.localScale = new Vector3(
            parentScale.x != 0f ? scale / parentScale.x : scale,
            parentScale.y != 0f ? scale / parentScale.y : scale,
            1f);
        sr.size = new Vector2(spriteSize.x * countX, spriteSize.y * countY);

        layer.scroll += layer.autoScroll * Time.deltaTime;

        Vector2 position = worldOrigin + layer.offset + Vector2.Scale(source - worldOrigin, layer.followCamera) + layer.scroll;

        if (layer.tileX)
        {
            position.x += Mathf.Round((cameraPosition.x - position.x) / tileSize.x) * tileSize.x;
            layer.scroll.x = Mathf.Repeat(layer.scroll.x, tileSize.x);
        }

        if (layer.tileY)
        {
            position.y += Mathf.Round((cameraPosition.y - position.y) / tileSize.y) * tileSize.y;
            layer.scroll.y = Mathf.Repeat(layer.scroll.y, tileSize.y);
        }

        sr.transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private static int MakeOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }
}