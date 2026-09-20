using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BiDirectionalObject : MonoBehaviour
{
    private enum Side { None, Left, Right }

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Detection")]
    [Tooltip("Only colliders with this tag will open the door.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Swap left and right if they come out the wrong way round.")]
    [SerializeField] private bool invertSides = false;

    [Header("Animator State Names")]
    [SerializeField] private string openLeftState = "OpenLeft";
    [SerializeField] private string openRightState = "OpenRight";
    [SerializeField] private string closeLeftState = "CloseLeft";
    [SerializeField] private string closeRightState = "CloseRight";

    private BoxCollider2D box;
    private Side openedFromSide = Side.None;
    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        occupants.Add(other);

        if (openedFromSide != Side.None) return;

        openedFromSide = GetSide(other.transform.position);
        Play(openedFromSide == Side.Left ? openLeftState : openRightState);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!occupants.Remove(other)) return;

        if (occupants.Count > 0) return;
        if (openedFromSide == Side.None) return;

        Play(openedFromSide == Side.Left ? closeLeftState : closeRightState);
        openedFromSide = Side.None;
    }

    private Side GetSide(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float x = local.x - box.offset.x;

        bool isLeft = x < 0f;
        if (invertSides) isLeft = !isLeft;

        return isLeft ? Side.Left : Side.Right;
    }

    private void Play(string stateName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name}: No Animator assigned to DoorTrigger.", this);
            return;
        }

        animator.Play(stateName);
    }
}