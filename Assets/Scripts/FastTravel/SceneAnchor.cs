using UnityEngine;

public class SceneAnchor : MonoBehaviour
{
    [SerializeField] private string anchorID;
    [SerializeField] private Transform sceneRoot;

    public string AnchorID => anchorID;
    public Transform SceneRoot => sceneRoot;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 2f, 0.5f));
    }
}