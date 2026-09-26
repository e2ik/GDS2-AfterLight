using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TestReturnCam : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayer;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerLayer.value) == 0) return;

        Debug.Log("[TestReturnCameraTrigger] Returning camera to normal follow.");
        GameManager.Instance?.ReturnCameraToNormal();

        Destroy(gameObject);
    }
}