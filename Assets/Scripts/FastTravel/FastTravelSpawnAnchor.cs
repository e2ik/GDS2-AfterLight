using UnityEngine;

public class FastTravelSpawnAnchor : MonoBehaviour
{
    [SerializeField] private string anchorID;

    public string AnchorID => anchorID;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, Vector3.up * 1f);
    }
}