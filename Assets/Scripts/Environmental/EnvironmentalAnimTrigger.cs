using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class EnvironmentalAnimTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool triggerOnce = true;

    [Header("Animator Settings")]
    [SerializeField] private string animatorTriggerName = "Activate";
    [SerializeField] private bool useBoolInstead = false;
    [SerializeField] private string animatorBoolName = "IsActive";

    private Animator animator;
    private bool hasTriggered;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOnPlayerLayer(other)) return;
        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;

        if (useBoolInstead)
            animator.SetBool(animatorBoolName, true);
        else
            animator.SetTrigger(animatorTriggerName);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsOnPlayerLayer(other)) return;
        if (triggerOnce) return;

        if (useBoolInstead)
            animator.SetBool(animatorBoolName, false);
    }

    private bool IsOnPlayerLayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer) != 0;
    }
}