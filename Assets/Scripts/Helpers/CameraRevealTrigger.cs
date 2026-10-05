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

    [Tooltip("The area the camera should zoom out to fully show. Its own collider's bounds are used — it doesn't need to be a trigger, or even on this object.")]
    [SerializeField] private Collider2D revealArea;

    [Tooltip("Only used in Temporary mode.")]
    [SerializeField, Min(0f)] private float holdDuration = 2f;

    [Tooltip("If off, this trigger only ever fires once — and stays fired even across a scene reload, for the rest of this play session. If on, it can fire again on a later re-entry.")]
    [SerializeField] private bool canRetrigger = false;

    [Tooltip("Only used in Temporary mode, since it has a natural release point. In LockCamera mode this is intentionally not applied here — freezing the player indefinitely with no guaranteed unlock would be a footgun, so if you need the player held during a permanent lock, manage that from whatever system calls ReturnCameraToNormal().")]
    [SerializeField] private bool shouldLockPlayer = false;

    [SerializeField] private bool releaseLockOnReenter = false;

    [SerializeField] private Transform walkTarget;
    [SerializeField] private bool walkOnlyIfOutsideArea = true;
    [SerializeField, Range(0.1f, 1f)] private float walkSpeed = 0.6f;

    private static readonly HashSet<string> triggeredThisSession = new();

    public static void ClearSessionTriggers()
    {
        triggeredThisSession.Clear();
    }

    private string triggerID;
    private static CameraRevealTrigger activeLockOwner;
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();

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
        if (!IsOnPlayerLayer(other)) return;

        bool alreadyInside = playerColliders.Count > 0;
        playerColliders.Add(other);
        if (alreadyInside) return;

        bool toggles = mode == RevealTriggerMode.LockCamera && releaseLockOnReenter;

        if (toggles && activeLockOwner == this)
        {
            activeLockOwner = null;
            Player leavingPlayer = ResolvePlayer(other);
            if (leavingPlayer != null && leavingPlayer.Controller != null) leavingPlayer.Controller.CancelScriptedWalk();

            CameraFollow2D releaseCam = FindFirstObjectByType<CameraFollow2D>();
            if (releaseCam != null) releaseCam.ReturnToNormalFollow();
            return;
        }

        if (!canRetrigger && triggeredThisSession.Contains(triggerID)) return;
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
            activeLockOwner = this;
            TryWalkPlayerIn(ResolvePlayer(other));
        }

        triggeredThisSession.Add(triggerID);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
    }

    private void TryWalkPlayerIn(Player player)
    {
        if (walkTarget == null || player == null || player.Controller == null) return;

        if (walkOnlyIfOutsideArea)
        {
            Bounds area = revealArea.bounds;
            Vector2 pos = player.transform.position;
            bool inside = pos.x >= area.min.x && pos.x <= area.max.x && pos.y >= area.min.y && pos.y <= area.max.y;
            if (inside) return;
        }

        player.Controller.StartScriptedWalk(walkTarget.position.x, null, walkSpeed);
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