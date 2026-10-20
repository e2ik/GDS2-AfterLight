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

    private Collider2D zone;
    private int playerColliderCount;

    private void Awake()
    {
        zone = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        playerColliderCount = 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerColliderCount++;
        if (playerColliderCount > 1) return;

        Run(IsRightOfCenter(other) ? onEnterFromRight : onEnterFromLeft);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
        if (playerColliderCount > 0) return;

        Run(IsRightOfCenter(other) ? onExitRight : onExitLeft);
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
}