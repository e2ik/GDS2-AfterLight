using UnityEngine;

namespace Enemies
{
    public class AttackEvents : MonoBehaviour
    {
        [SerializeField] private HitBox hitbox;
        [SerializeField] private HurtBox hurtbox;
 
        public int CurrentDamage { get; set; }
        
        public void EnableHitbox() => hitbox.Activate(CurrentDamage);
        public void DisableHitbox() => hitbox.Deactivate();
 
        public void EnableIFrames() => hurtbox.Invulnerable = true;
        public void DisableIFrames() => hurtbox.Invulnerable = false;
 
        public void OpenParryWindow() => ParryWindowOpen = true;
        public void CloseParryWindow() => ParryWindowOpen = false;
        
        public bool ParryWindowOpen { get; private set; }
    }
}
