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
        animator.SetBool(IsParryingHash, player.CombatController.IsParrying);
        animator.SetBool(IsAttackingHash, player.CombatController.IsAttacking);
        animator.SetBool(IsSkillingHash, player.CombatController.IsSkilling);

        if (player.CombatController.IsSkilling) return;

        animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocityX));
        animator.SetFloat(YVelocityHash, rb.linearVelocityY);
        animator.SetBool(IsGroundedHash, player.Controller.IsGrounded);
        animator.SetBool(IsWallSlidingHash, player.Controller.IsWallSliding);
        animator.SetBool(IsDashingHash, player.Controller.IsDashing);
        animator.SetBool(IsDirectionalDashHash, player.Controller.IsDirectionalDash);
        animator.SetBool(IsChargingSkillHash, player.Controller.IsChargingSkill);
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

    public  void EndSkillAnimation()
    {
        player.CombatController.EndSkill();
    }
}