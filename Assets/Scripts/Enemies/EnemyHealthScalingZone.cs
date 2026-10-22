using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Enemies
{
    public class EnemyHealthScalingZone : MonoBehaviour
    {
        [SerializeField] private float minHitDamage = 20f;
        [SerializeField] private float maxHitDamage = 30f;
        [SerializeField, Min(1f)] private float maxHealthMultiplier = 2f;

        [Header("Attack Shift")]
        [SerializeField] private float minAttack = 35f;
        [SerializeField] private float maxAttack = 50f;
        [SerializeField, Min(1f)] private float highAttackMinMultiplier = 1.1f;
        [SerializeField, Min(1f)] private float highAttackMaxMultiplier = 2.5f;
        [SerializeField] private bool debugScaling = false;

        private static readonly Dictionary<int, EnemyHealthScalingZone> zones = new Dictionary<int, EnemyHealthScalingZone>();

        private bool evaluated;
        private float multiplier = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetZones() => zones.Clear();

        private void OnEnable()
        {
            zones[gameObject.scene.handle] = this;
            evaluated = false;
        }

        private void OnDisable()
        {
            if (zones.TryGetValue(gameObject.scene.handle, out EnemyHealthScalingZone zone) && zone == this)
                zones.Remove(gameObject.scene.handle);
        }

        private float Evaluate()
        {
            if (evaluated) return multiplier;

            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || player.Stats == null) return 1f;

            float hitDamage = player.Stats.GetPotentialHitDamage(out float bestAttack, out WeaponInstance bestWeapon);
            float t = Range01(minHitDamage, maxHitDamage, hitDamage);
            float potentialAttack = player.Stats.GetPotentialAttack();
            float attackT = Range01(minAttack, maxAttack, potentialAttack);

            float lowEnd = Mathf.Lerp(1f, highAttackMinMultiplier, attackT);
            float highEnd = Mathf.Lerp(maxHealthMultiplier, highAttackMaxMultiplier, attackT);
            multiplier = Mathf.Lerp(lowEnd, highEnd, t);
            evaluated = true;

            if (debugScaling)
            {
                float currentHit = player.CombatController != null ? player.CombatController.GetScaledAttackDamage() : 0f;
                string weaponText = bestWeapon != null ? $"weapon {bestWeapon.InstTemplateID} dmg {bestWeapon.InstRolledDamage:F1}" : "no weapon";
                Debug.Log($"[EnemyHealthScalingZone] {gameObject.scene.name}: best hit {hitDamage:F1} = {weaponText} + attack {bestAttack:F1}^exp | current equipped base hit {currentHit:F1}, current attack {player.Stats.TotalAttack:F1} | range {minHitDamage}-{maxHitDamage} | potential attack {potentialAttack:F1} shifts range to x{lowEnd:F2}-x{highEnd:F2} -> x{multiplier:F2}", this);
            }

            return multiplier;
        }

        private static float Range01(float min, float max, float value)
        {
            if (max > min) return Mathf.InverseLerp(min, max, value);
            return value >= min ? 1f : 0f;
        }

        public static float GetMultiplier(Scene scene)
        {
            if (!zones.TryGetValue(scene.handle, out EnemyHealthScalingZone zone) || zone == null) return 1f;
            return zone.Evaluate();
        }
    }
}