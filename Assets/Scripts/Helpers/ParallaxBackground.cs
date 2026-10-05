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
    }

    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    [Header("Area Tint")]
    [SerializeField] private Color exteriorTint = Color.white;
    [SerializeField] private Color interiorTint = new Color(0.4f, 0.4f, 0.4f);

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

    private void Awake()
    {
        content = new GameObject("Layers").transform;
        content.SetParent(transform, false);

        foreach (ParallaxLayer layer in layers)
        {
            GameObject layerObject = new GameObject(string.IsNullOrEmpty(layer.name) ? "Layer" : layer.name);
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
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void ResetParallax()
    {
    }

    public void SetTint(Color tint)
    {
        currentTint = tint;

        foreach (ParallaxLayer layer in layers)
        {
            if (layer.renderer == null) continue;
            layer.renderer.color = layer.useAreaTint ? layer.color * tint : layer.color;
        }
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

        bool visible = !onlyVisibleInGame || (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Game);
        if (content.gameObject.activeSelf != visible)
        {
            content.gameObject.SetActive(visible);
            if (visible) hasAppliedSide = false;
        }
        if (!visible) return;

        SyncAreaTint();

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