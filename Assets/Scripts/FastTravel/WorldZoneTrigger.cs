using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WorldZoneStreamer : MonoBehaviour
{
    [Header("Scene Stitching Setup")]
    [SerializeField] private string sceneToLoad;
    [SerializeField] private string sourceAnchorID;
    [SerializeField] private string targetAnchorID;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (WorldStreamer.Instance != null && !WorldStreamer.Instance.IsFastTraveling)
        {
            WorldStreamer.Instance.StreamAndStitchScene(sceneToLoad, sourceAnchorID, targetAnchorID);
        }
    }
}