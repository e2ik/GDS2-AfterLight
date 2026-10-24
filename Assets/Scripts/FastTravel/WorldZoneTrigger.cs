using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WorldZoneTrigger : MonoBehaviour
{
    public enum ZoneAction { None, Load, Unload }

    [Header("Scene Stitching Setup")]
    [SerializeField] private string sceneToLoad;
    [SerializeField] private string sourceAnchorID;
    [SerializeField] private string targetAnchorID;

    [Header("Actions")]
    [SerializeField] private ZoneAction onEnterFromLeft = ZoneAction.Load;
    [SerializeField] private ZoneAction onEnterFromRight = ZoneAction.Load;
    [SerializeField] private ZoneAction onExitLeft = ZoneAction.None;
    [SerializeField] private ZoneAction onExitRight = ZoneAction.None;
    [SerializeField, Min(0f)] private float unloadPadding = 0f;

    [Header("Music")] 
    [SerializeField] private bool switchAreaMusic = true;
    [SerializeField] private bool loadedSceneIsOnRight = true;

    private Collider2D zone;
    private int playerColliderCount;
    private bool unloadPending;
    private bool pendingRight;
    private Transform pendingPlayer;

    private void Awake()
    {
        zone = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        playerColliderCount = 0;
        unloadPending = false;
    }

    private void Update()
    {
        if (!unloadPending) return;

        if (pendingPlayer == null)
        {
            unloadPending = false;
            return;
        }

        Bounds bounds = zone.bounds;
        float x = pendingPlayer.position.x;
        bool farEnough = pendingRight ? x >= bounds.max.x + unloadPadding : x <= bounds.min.x - unloadPadding;
        if (!farEnough) return;

        unloadPending = false;
        Run(ZoneAction.Unload);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerColliderCount++;
        if (playerColliderCount > 1) return;

        unloadPending = false;

        Run(IsRightOfCenter(other) ? onEnterFromRight : onEnterFromLeft);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
        if (playerColliderCount > 0) return;

        bool exitRight = IsRightOfCenter(other);
        UpdateAreaMusic(exitRight);
        ZoneAction action = exitRight ? onExitRight : onExitLeft;

        if (action == ZoneAction.Unload && unloadPadding > 0f)
        {
            unloadPending = true;
            pendingRight = exitRight;
            pendingPlayer = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            return;
        }

        Run(action);
    }

    private bool IsRightOfCenter(Collider2D other)
    {
        return other.bounds.center.x > zone.bounds.center.x;
    }

    private void Run(ZoneAction action)
    {
        WorldStreamer streamer = WorldStreamer.Instance;
        if (streamer == null || streamer.IsFastTraveling) return;

        switch (action)
        {
            case ZoneAction.Load:
                streamer.StreamAndStitchScene(sceneToLoad, sourceAnchorID, targetAnchorID);
                break;
            case ZoneAction.Unload:
                if (sceneToLoad != gameObject.scene.name) streamer.UnloadStreamedScene(sceneToLoad);
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (unloadPadding <= 0f) return;

        Collider2D col = zone != null ? zone : GetComponent<Collider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Gizmos.color = new Color(1f, 0.4f, 0.2f);

        if (onExitRight == ZoneAction.Unload)
        {
            float x = bounds.max.x + unloadPadding;
            Gizmos.DrawLine(new Vector3(x, bounds.min.y), new Vector3(x, bounds.max.y));
        }

        if (onExitLeft == ZoneAction.Unload)
        {
            float x = bounds.min.x - unloadPadding;
            Gizmos.DrawLine(new Vector3(x, bounds.min.y), new Vector3(x, bounds.max.y));
        }
    }

    private void UpdateAreaMusic(bool exitRight)
    {
        if (!switchAreaMusic || MusicManager.Instance == null) return;
        
        WorldStreamer streamer = WorldStreamer.Instance;

        if (streamer != null && streamer.IsFastTraveling)
            return;

        string enteredScene = exitRight == loadedSceneIsOnRight ? sceneToLoad : gameObject.scene.name;
        MusicManager.Instance.SetAreaMusicForScene(enteredScene, interruptBoss: false);
    }
}