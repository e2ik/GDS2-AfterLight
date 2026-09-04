using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class VfxBox : MonoBehaviour
    {
        private Collider2D col;

        private static readonly Color ZeroColor = Color.white;
        private static readonly Color LightColor = Color.cyan;
        private static readonly Color MediumColor = Color.yellow;
        private static readonly Color HeavyColor = Color.red;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
        }

        public void PlayVFX(AttackForce attackForce)
        {
            ParticleSystem ps = PSpawner.Spawn("anticipation", col.bounds.center, Quaternion.identity);
            if (ps == null) return;

            Color color = GetColorForForce(attackForce);
            var main = ps.main;
            main.startColor = color;
        }

        private static Color GetColorForForce(AttackForce force)
        {
            switch (force)
            {
                case AttackForce.Zero:   return ZeroColor;
                case AttackForce.Light:  return LightColor;
                case AttackForce.Medium: return MediumColor;
                case AttackForce.Heavy:  return HeavyColor;
                default:                 return ZeroColor;
            }
        }
    }
}