using UnityEngine;

namespace Enemies
{
    public class AttackEvents : MonoBehaviour
    {
        [SerializeField] private HitBox hitbox;
        [SerializeField] private HurtBox hurtbox;
        [SerializeField] private VfxBox vfxBox;
        [SerializeField] private GameObject[] groundObjs;
        [SerializeField] private Collider2D[] bonusZones;
        [SerializeField, Min(1f)] private float bonusDamageMultiplier = 2f;

        public int CurrentDamage { get; set; }
        public ParryDirection CurrentParryDirection { get; set; }
        public AttackForce CurrentAttackForce { get; set; }
        public bool ParryWindowOpen { get; private set; }
        public bool LastHitConfirmed { get; private set; }
        public bool BonusWindowOpen { get; private set; }

        private Enemy owner;
        private Collider2D[] playerColliders;
        private Player cachedPlayer;
        
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

        public void OpenBonusWindow() => BonusWindowOpen = true;
        public void CloseBonusWindow() => BonusWindowOpen = false;

        public float BonusDamageMultiplier => bonusDamageMultiplier;

        public float GetPlayerDamageMultiplier() => IsPlayerBonusActive() ? bonusDamageMultiplier : 1f;

        public bool IsPlayerBonusActive()
        {
            if (!BonusWindowOpen || bonusZones == null) return false;

            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return false;

            if (player != cachedPlayer)
            {
                cachedPlayer = player;
                playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            }

            foreach (Collider2D zone in bonusZones)
            {
                if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy) continue;

                foreach (Collider2D col in playerColliders)
                {
                    if (col == null || !col.enabled || col.isTrigger) continue;
                    if (zone.Distance(col).isOverlapped) return true;
                }
            }

            return false;
        }

        public void EnableGroundObj() => SetGroundObjs(true);

        public void DisableGroundObj() => SetGroundObjs(false);

        public void EnableGroundObjAt(int index) => SetGroundObj(index, true);

        public void DisableGroundObjAt(int index) => SetGroundObj(index, false);

        private void SetGroundObjs(bool active)
        {
            if (groundObjs == null) return;

            foreach (GameObject obj in groundObjs)
                if (obj != null) obj.SetActive(active);
        }

        private void SetGroundObj(int index, bool active)
        {
            if (groundObjs == null || index < 0 || index >= groundObjs.Length || groundObjs[index] == null) return;

            groundObjs[index].SetActive(active);
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