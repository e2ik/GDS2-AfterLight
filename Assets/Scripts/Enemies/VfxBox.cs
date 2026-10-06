using FMODUnity;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class VfxBox : MonoBehaviour
    {
        [SerializeField] private EventReference warningEvent;

        [Header("Anticipation Flash")]
        [SerializeField] private bool flashOnAnticipation = true;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField] private HitFlash.FlashMode flashMode = HitFlash.FlashMode.Edge;
        [SerializeField, Min(0f)] private float flashIntensity = 2f;
        [SerializeField, ColorUsage(false, true)] private Color zeroColor = Color.white;
        [SerializeField, ColorUsage(false, true)] private Color lightColor = Color.cyan;
        [SerializeField, ColorUsage(false, true)] private Color mediumColor = Color.yellow;
        [SerializeField, ColorUsage(false, true)] private Color heavyColor = Color.red;

        private Collider2D col;
        private ParticleSystem activeParticleSystem; // Track the current effect

        private void Awake()
        {
            col = GetComponent<Collider2D>();

            if (hitFlash == null)
            {
                Enemy enemy = GetComponentInParent<Enemy>();
                hitFlash = enemy != null
                    ? enemy.GetComponentInChildren<HitFlash>(true)
                    : GetComponentInParent<HitFlash>();
            }
        }

        private void OnDisable()
        {
            if (activeParticleSystem != null)
            {
                PSpawner.Kill(activeParticleSystem);
                activeParticleSystem = null;
            }
        }

        public void PlayVFX(AttackForce attackForce)
        {
            if (!gameObject.activeInHierarchy || col == null || !col.enabled) return;
            if (activeParticleSystem != null && activeParticleSystem.isPlaying)
            {
                PSpawner.Kill(activeParticleSystem);
            }

            AudioManager.PlaySFXAttached(warningEvent, gameObject);

            Color color = GetColorForForce(attackForce);

            if (flashOnAnticipation && hitFlash != null)
                hitFlash.Flash(color, flashIntensity, flashMode);

            activeParticleSystem = PSpawner.Spawn("anticipation", col.bounds.center, Quaternion.identity);
            if (activeParticleSystem == null) return;

            var main = activeParticleSystem.main;
            main.startColor = color;
        }

        private Color GetColorForForce(AttackForce force)
        {
            switch (force)
            {
                case AttackForce.Zero:   return zeroColor;
                case AttackForce.Light:  return lightColor;
                case AttackForce.Medium: return mediumColor;
                case AttackForce.Heavy:  return heavyColor;
                default:                 return zeroColor;
            }
        }
    }
}