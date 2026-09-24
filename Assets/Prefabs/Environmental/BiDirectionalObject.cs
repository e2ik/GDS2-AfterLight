using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BiDirectionalObject : MonoBehaviour
{
    private enum Side { None, Left, Right }

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool invertSides = false;

    [Header("Animator State Names")]
    [SerializeField] private string openLeftState = "OpenLeft";
    [SerializeField] private string openRightState = "OpenRight";
    [SerializeField] private string closeLeftState = "CloseLeft";
    [SerializeField] private string closeRightState = "CloseRight";

    [Header("Audio")]
    [SerializeField] private FMODUnity.EventReference openEvent;
    [SerializeField] private FMODUnity.EventReference closeEvent;

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
        if (!IsOnPlayerLayer(other)) return;

        occupants.Add(other);

        if (openedFromSide != Side.None) return;

        openedFromSide = GetSide(other.transform.position);
        Play(openedFromSide == Side.Left ? openLeftState : openRightState);
        AudioManager.PlaySFX(openEvent, transform.position);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!occupants.Remove(other)) return;

        if (occupants.Count > 0) return;
        if (openedFromSide == Side.None) return;

        Play(openedFromSide == Side.Left ? closeLeftState : closeRightState);
        AudioManager.PlaySFX(closeEvent, transform.position);
        openedFromSide = Side.None;
    }

    private bool IsOnPlayerLayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer.value) != 0;
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
            Debug.LogWarning($"{name}: No Animator assigned to BiDirectionalObject.", this);
            return;
        }

        animator.Play(stateName);
    }
}