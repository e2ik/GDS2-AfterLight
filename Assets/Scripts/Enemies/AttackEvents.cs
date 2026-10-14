using UnityEngine;

namespace Enemies
{
    public class AttackEvents : MonoBehaviour
    {
        [SerializeField] private HitBox hitbox;
        [SerializeField] private HurtBox hurtbox;
        [SerializeField] private VfxBox vfxBox;
        [SerializeField] private GameObject groundObj;

        public int CurrentDamage { get; set; }
        public ParryDirection CurrentParryDirection { get; set; }
        public AttackForce CurrentAttackForce { get; set; }
        public bool ParryWindowOpen { get; private set; }
        public bool LastHitConfirmed { get; private set; }

        private Enemy owner;
        
        public void EnableHitbox()
        {
            StopCharge();

            if (owner == null) owner = GetComponentInParent<Enemy>();

            if (owner != null && owner.TryGetMoveHitValues(out int moveDamage, out AttackForce moveForce, out ParryDirection moveDirection))
                hitbox.Enable(moveDamage, moveDirection, moveForce, true);
            else
                hitbox.Enable(CurrentDamage, CurrentParryDirection, CurrentAttackForce);
        }

        public void DisableHitbox() => hitbox.Disable();

        public void EnableGroundObj()
        {
            if (groundObj != null) groundObj.SetActive(true);
        }

        public void DisableGroundObj()
        {
            if (groundObj != null) groundObj.SetActive(false);
        }

        public void EnableIFrames() => hurtbox.Invulnerable = true;
        public void DisableIFrames() => hurtbox.Invulnerable = false;

        public void OpenParryWindow() => ParryWindowOpen = true;
        public void CloseParryWindow() => ParryWindowOpen = false;

        public void PlayAnticipation()
        {
            vfxBox.PlayVFX(CurrentAttackForce);

            if (CurrentAttackForce == AttackForce.Heavy)
                Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.EnemyHeavyAttack);
        }
        public void StopCharge() => vfxBox?.StopCharge();

        public void ResetHitConfirmation() => LastHitConfirmed = false;
        public void ConfirmHit() => LastHitConfirmed = true;

    }
}