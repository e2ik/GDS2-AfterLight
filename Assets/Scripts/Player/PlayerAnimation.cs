using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private Player player;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sr;
    private Color ogColor;

    // Animator Hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsWallSlidingHash = Animator.StringToHash("isWallSliding");
    private static readonly int IsDashingHash = Animator.StringToHash("isDashing");
    private static readonly int IsDirectionalDashHash = Animator.StringToHash("isDirectionalDash");
    private static readonly int IsChargingSkillHash = Animator.StringToHash("isChargingSkill");
    private static readonly int IsParryingHash = Animator.StringToHash("isParrying");
    private static readonly int IsParrySuccessHash = Animator.StringToHash("isParrySuccess");
    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
    private static readonly int IsSkillingHash = Animator.StringToHash("isSkilling");
    private static readonly int IsHurtHash = Animator.StringToHash("Hurt");

    private string lastPlayedSkill = string.Empty;
    private Coroutine flashColorCoroutine;
    private bool wasInvulnerable;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        if (sr == null)
        {
            sr = GetComponentInParent<SpriteRenderer>();
            if (sr != null) ogColor = sr.color;
        }
    }

    private void Update()
    {
        if (player == null || rb == null) return;
        UpdateAnimationParameters();
        HandleSkillAnimation();
        HandleInvulnerabilityVisuals();
    }

    private void UpdateAnimationParameters()
    {
        animator.SetBool(IsParryingHash, player.CombatController.IsParrying);
        animator.SetBool(IsParrySuccessHash, player.CombatController.IsParrySuccess);
        animator.SetBool(IsAttackingHash, player.CombatController.IsAttacking);
        animator.SetBool(IsSkillingHash, player.CombatController.IsSkilling);

        if (player.CombatController.IsSkilling) return;

        animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocityX));
        animator.SetFloat(YVelocityHash, rb.linearVelocityY);
        animator.SetBool(IsGroundedHash, player.Controller.IsGrounded);
        animator.SetBool(IsWallSlidingHash, player.Controller.IsWallSliding);
        animator.SetBool(IsDashingHash, player.Controller.IsDashing);
        animator.SetBool(IsDirectionalDashHash, player.Controller.IsDirectionalDash);
        animator.SetBool(IsChargingSkillHash, player.CombatController.IsChargingSkill);
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

    #region Hit & Knockback Animation

    public void PlayHurtAnimation()
    {
        animator.SetTrigger(IsHurtHash);
        FlashRedOnHit();
        CamControls.Shake(0.1f, 0.5f);
    }

    public void FlashRedOnHit()
    {
        StartFlashColor(Color.red, 0.1f);
    }

    #endregion

    #region Parry Visuals

    public void FlashGreenOnParrySuccess()
    {
        StartFlashColor(Color.green, 0.15f);

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Vector2 spawnPosition = transform.position;

        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            Vector2 center = bounds.center;
            float randomX = UnityEngine.Random.Range(0.4f, 0.7f);
            float facingDir = player.Controller != null ? player.Controller.FacingDirection : 1f;
            float horizontalOffset = (bounds.extents.x + randomX) * facingDir;
            float randomY = UnityEngine.Random.Range(-bounds.extents.y + 0.7f, bounds.extents.y - 0.7f);

            spawnPosition = new Vector2(center.x + horizontalOffset, center.y + randomY);
        }

        PSpawner.Spawn("spark", spawnPosition);
        CamControls.Shake(0.15f, 0.1f);
    }

    #endregion

    #region Helper Methods

    private void StartFlashColor(Color flashColor, float duration)
    {
        if (sr == null) return;
        if (flashColorCoroutine != null) StopCoroutine(flashColorCoroutine);
        flashColorCoroutine = StartCoroutine(FlashColorRoutine(flashColor, duration));
    }

    private IEnumerator FlashColorRoutine(Color flashColor, float duration)
    {
        sr.color = flashColor;
        yield return new WaitForSeconds(duration);
        sr.color = ogColor;
        flashColorCoroutine = null;
    }

    private void HandleInvulnerabilityVisuals()
    {
        if (sr == null || player == null || player.Controller == null) return;

        bool isInvuln = player.Controller.IsInvulnerable;

        if (flashColorCoroutine != null) return;

        if (isInvuln)
        {
            Color invulnColor = ogColor;
            invulnColor.a = 0.5f; 
            sr.color = invulnColor;
            wasInvulnerable = true;
        }
        else if (wasInvulnerable)
        {
            sr.color = ogColor;
            wasInvulnerable = false;
        }
    }

    #endregion
}