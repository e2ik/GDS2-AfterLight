using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Enemies
{
    public class EnemyHealthScalingZone : MonoBehaviour
    {
        [SerializeField] private float minPower = 40f;
        [SerializeField] private float maxPower = 50f;
        [SerializeField, Min(1f)] private float maxHealthMultiplier = 2f;
        [SerializeField, Min(0f)] private float weaponDamageWeight = 1f;
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

            float power = player.Stats.GetPotentialPower(weaponDamageWeight);
            float t = maxPower > minPower ? Mathf.InverseLerp(minPower, maxPower, power) : (power >= minPower ? 1f : 0f);
            multiplier = Mathf.Lerp(1f, maxHealthMultiplier, t);
            evaluated = true;

            if (debugScaling)
                Debug.Log($"[EnemyHealthScalingZone] {gameObject.scene.name}: power {power:F1} (range {minPower}-{maxPower}) -> x{multiplier:F2}", this);

            return multiplier;
        }

        public static float GetMultiplier(Scene scene)
        {
            if (!zones.TryGetValue(scene.handle, out EnemyHealthScalingZone zone) || zone == null) return 1f;
            return zone.Evaluate();
        }
    }
}