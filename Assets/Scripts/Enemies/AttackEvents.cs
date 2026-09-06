using UnityEngine;

namespace Enemies
{
    public class AttackEvents : MonoBehaviour
    {
        [SerializeField] private HitBox hitbox;
        [SerializeField] private HurtBox hurtbox;
        [SerializeField] private VfxBox vfxBox;

        public int CurrentDamage { get; set; }
        public ParryDirection CurrentParryDirection { get; set; }
        public AttackForce CurrentAttackForce { get; set; }
        public bool ParryWindowOpen { get; private set; }
        
        public void EnableHitbox() => hitbox.Enable(CurrentDamage, CurrentParryDirection, CurrentAttackForce);
        public void DisableHitbox() => hitbox.Disable();

        public void EnableIFrames() => hurtbox.Invulnerable = true;
        public void DisableIFrames() => hurtbox.Invulnerable = false;

        public void OpenParryWindow() => ParryWindowOpen = true;
        public void CloseParryWindow() => ParryWindowOpen = false;

        public void PlayAnticipation() => vfxBox.PlayVFX(CurrentAttackForce);
        
    }
}
