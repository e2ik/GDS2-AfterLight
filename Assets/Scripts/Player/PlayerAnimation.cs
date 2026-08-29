using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private Player player;
    private Rigidbody2D rb;
    private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsWallSlidingHash = Animator.StringToHash("isWallSliding");
    private static readonly int IsDashingHash = Animator.StringToHash("isDashing");
    private static readonly int IsDirectionalDashHash = Animator.StringToHash("isDirectionalDash");
    private static readonly int IsChargingSkillHash = Animator.StringToHash("isChargingSkill");
    private static readonly int IsParryingHash = Animator.StringToHash("isParrying");
    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
    private static readonly int IsSkillingHash = Animator.StringToHash("isSkilling");

    private string lastPlayedSkill = string.Empty;

    [Header("Parry Settings")]
    [SerializeField] private float minParryDisplayDuration = 0.25f; 
    private bool isParryAnimationLocked = false;
    private Coroutine parryLockCoroutine;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
    }

    private void Update()
    {
        if (player == null || rb == null) return;
        UpdateAnimationParameters();
        HandleSkillAnimation();
    }

    private void UpdateAnimationParameters()
    {
        bool isSkilling = player.CombatController.IsSkilling;
        animator.SetBool(IsSkillingHash, isSkilling);

        if (isSkilling)
        {
            isParryAnimationLocked = false; // Reset lock on skill
            return;
        }

        bool actualIsParrying = player.CombatController.IsParrying;

        if (actualIsParrying && !isParryAnimationLocked)
        {
            if (parryLockCoroutine != null) StopCoroutine(parryLockCoroutine);
            parryLockCoroutine = StartCoroutine(LockParryAnimation());
        }

        if (ShouldInterruptParry())
        {
            isParryAnimationLocked = false;
            if (parryLockCoroutine != null) StopCoroutine(parryLockCoroutine);
        }

        bool visualIsParrying = actualIsParrying || isParryAnimationLocked;
        animator.SetBool(IsParryingHash, visualIsParrying);

        animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocityX));
        animator.SetFloat(YVelocityHash, rb.linearVelocityY);
        animator.SetBool(IsGroundedHash, player.Controller.IsGrounded);
        animator.SetBool(IsWallSlidingHash, player.Controller.IsWallSliding);
        animator.SetBool(IsDashingHash, player.Controller.IsDashing);
        animator.SetBool(IsDirectionalDashHash, player.Controller.IsDirectionalDash);
        animator.SetBool(IsChargingSkillHash, player.Controller.IsChargingSkill);
        animator.SetBool(IsAttackingHash, player.CombatController.IsAttacking);
    }

    private bool ShouldInterruptParry()
    {
        // Add any action here that SHOULD break out of the parry animation early
        return player.Controller.IsDashing || 
               player.Controller.IsDirectionalDash || 
               player.CombatController.IsAttacking || 
               !player.Controller.IsGrounded;
    }

    private IEnumerator LockParryAnimation()
    {
        isParryAnimationLocked = true;
        float timer = 0f;

        while (timer < minParryDisplayDuration)
        {
            // If interrupted mid-frame, exit early
            if (ShouldInterruptParry())
            {
                isParryAnimationLocked = false;
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        isParryAnimationLocked = false;
    }

    private void HandleSkillAnimation()
    {
        if (player.CombatController.IsSkilling)
        {
            string currentGem = player.CombatController.CurrentSkillGemName;

            if (!string.IsNullOrEmpty(currentGem) && lastPlayedSkill != currentGem)
            {
                animator.Play(currentGem);
                lastPlayedSkill = currentGem;
            }
        }
        else
        {
            lastPlayedSkill = string.Empty;
        }
    }

    public void EndSkillAnimation()
    {
        player.CombatController.EndSkill();
    }
}