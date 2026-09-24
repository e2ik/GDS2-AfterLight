using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CameraRevealTrigger : MonoBehaviour
{
    public enum RevealTriggerMode
    {
        Temporary,
        LockCamera
    }

    [SerializeField] private RevealTriggerMode mode = RevealTriggerMode.Temporary;

    [SerializeField] private LayerMask playerLayer;

    [Tooltip("Attach this to a Collider2D to define the area to be revealed.")]
    [SerializeField] private Collider2D revealArea;
    [SerializeField, Min(0f)] private float holdDuration = 2f;
    private bool canRetrigger = false; // broken atm leave on false
    [SerializeField] private bool shouldLockPlayer = false;

    private static readonly HashSet<string> triggeredThisSession = new(); // session based but new game will clear this
    
    public static void ClearSessionTriggers()
    {
        triggeredThisSession.Clear();
    }

    private string triggerID;

    private void Awake()
    {
        triggerID = GetHierarchyPath(transform);
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canRetrigger && triggeredThisSession.Contains(triggerID)) return;
        if (!IsOnPlayerLayer(other)) return;
        if (revealArea == null)
        {
            Debug.LogWarning($"{name}: no Reveal Area assigned — nothing to show.", this);
            return;
        }

        CameraFollow2D cam = FindFirstObjectByType<CameraFollow2D>();
        if (cam == null) return;

        if (mode == RevealTriggerMode.Temporary)
        {
            Player player = shouldLockPlayer ? ResolvePlayer(other) : null;

            if (player != null && player.Controller != null)
            {
                player.Controller.InputEnabled = false;
                player.Controller.FreezeMovement(true);
            }

            cam.RevealBounds(revealArea.bounds, holdDuration, () =>
            {
                if (player != null && player.Controller != null)
                {
                    player.Controller.InputEnabled = true;
                    player.Controller.FreezeMovement(false);
                }
            });
        }
        else
        {
            cam.LockToBounds(revealArea.bounds);
        }

        triggeredThisSession.Add(triggerID);
    }

    private string GetHierarchyPath(Transform current)
    {
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path + $"[{current.GetSiblingIndex()}]";
        }
        return $"{gameObject.scene.name}:{path}[{transform.GetSiblingIndex()}]";
    }

    private Player ResolvePlayer(Collider2D other)
    {
        return other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<Player>()
            : other.GetComponentInParent<Player>();
    }

    private bool IsOnPlayerLayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer.value) != 0;
    }
}