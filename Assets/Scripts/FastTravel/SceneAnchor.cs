using UnityEngine;

public class SceneAnchor : MonoBehaviour
{
    [SerializeField] private string anchorID;
    [SerializeField] private Transform sceneRoot;
    [SerializeField] private Vector2 tileSize = Vector2.one;

    public string AnchorID => anchorID;
    public Transform SceneRoot => sceneRoot;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, new Vector3(tileSize.x, tileSize.y, 0.5f));
    }
}