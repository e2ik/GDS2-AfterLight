using UnityEngine;

namespace Enemies
{
    public class AttackEvents : MonoBehaviour
    {
        [SerializeField] private HitBox hitbox;
        [SerializeField] private HurtBox hurtbox;
 
        public int CurrentDamage { get; set; }
        public ParryDirection CurrentParryDirection { get; set; }
        public bool ParryWindowOpen { get; private set; }
        
        public void EnableHitbox() => hitbox.Activate(CurrentDamage, CurrentParryDirection);
 
        public void EnableIFrames() => hurtbox.Invulnerable = true;
        public void DisableIFrames() => hurtbox.Invulnerable = false;
 
        public void OpenParryWindow() => ParryWindowOpen = true;
        public void CloseParryWindow() => ParryWindowOpen = false;
        
    }
}
