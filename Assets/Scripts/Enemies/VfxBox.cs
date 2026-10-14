using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Color Override")]
        [SerializeField] private bool overrideColors = false;
        [SerializeField, ColorUsage(false, true)] private Color zeroColor = Color.white;
        [SerializeField, ColorUsage(false, true)] private Color lightColor = Color.cyan;
        [SerializeField, ColorUsage(false, true)] private Color mediumColor = Color.yellow;
        [SerializeField, ColorUsage(false, true)] private Color heavyColor = Color.red;

        [Header("Charge Effect")]
        [SerializeField] private ParticleSystem chargeParticles;
        [SerializeField] private bool tintChargeByForce = true;
        [SerializeField, Min(0f)] private float chargeEmissionRate = 15f;
        [FormerlySerializedAs("chargeScale")]
        [SerializeField, Min(0.01f)] private float chargeShapeScale = 1f;
        [SerializeField, Min(0.01f)] private float chargeParticleSize = 1f;
        [FormerlySerializedAs("chargeParticleSpeed")]
        [SerializeField, Min(0.01f)] private float chargeSimulationSpeed = 1f;

        [Header("Anticipation Effect")]
        [SerializeField] private bool overrideAnticipationScale = false;
        [SerializeField, Min(0.01f)] private float anticipationScale = 1f;

        private static readonly Dictionary<ParticleSystem, Vector3> anticipationBaseScales = new();
        [SerializeField] private string chargeGlowProperty = "_Intensity";
        [SerializeField, Min(0f)] private float chargeGlowIntensity = 2f;

        private const float ChargeDuration = 0.2f;

        private Collider2D col;
        private ParticleSystem activeParticleSystem; // Track the current effect
        private Enemy ownerEnemy;
        private bool charging;
        private ParticleSystemRenderer chargeRenderer;
        private MaterialPropertyBlock chargeBlock;
        private float stopEmitAt = -1f;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            ownerEnemy = GetComponentInParent<Enemy>();

            if (chargeParticles != null && !chargeParticles.gameObject.scene.IsValid())
            {
                chargeParticles = Instantiate(chargeParticles, transform);
                chargeParticles.transform.localPosition = Vector3.zero;
            }

            if (chargeParticles != null)
            {
                chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                chargeRenderer = chargeParticles.GetComponent<ParticleSystemRenderer>();

                foreach (ParticleSystem system in chargeParticles.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var systemMain = system.main;

                    if (!Mathf.Approximately(chargeParticleSize, 1f))
                        systemMain.startSizeMultiplier *= chargeParticleSize;

                    if (!Mathf.Approximately(chargeShapeScale, 1f))
                    {
                        var shape = system.shape;
                        shape.radius *= chargeShapeScale;
                        shape.scale *= chargeShapeScale;
                        shape.position *= chargeShapeScale;
                    }
                }
            }

            if (hitFlash == null)
            {
                Enemy enemy = GetComponentInParent<Enemy>();
                hitFlash = enemy != null
                    ? enemy.GetComponentInChildren<HitFlash>(true)
                    : GetComponentInParent<HitFlash>();
            }
        }

        private void Update()
        {
            if (charging && ownerEnemy != null && !ownerEnemy.IsAttacking)
                StopCharge();

            if (charging && chargeParticles != null && col != null)
                chargeParticles.transform.position = col.bounds.center;

            if (charging && stopEmitAt >= 0f && Time.time >= stopEmitAt)
            {
                stopEmitAt = -1f;
                if (chargeParticles != null) chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void StopCharge()
        {
            charging = false;
            stopEmitAt = -1f;
            if (chargeParticles == null) return;

            chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void PlayCharge(Color color)
        {
            if (chargeParticles == null) return;

            if (col != null) chargeParticles.transform.position = col.bounds.center;

            var chargeMain = chargeParticles.main;
            if (tintChargeByForce)
            {
                Color tint = color;
                tint.a = chargeMain.startColor.color.a;
                chargeMain.startColor = tint;
            }

            if (chargeEmissionRate > 0f)
            {
                var chargeEmission = chargeParticles.emission;
                chargeEmission.enabled = true;
                chargeEmission.rateOverTime = chargeEmissionRate;
            }

            if (chargeRenderer != null && !string.IsNullOrEmpty(chargeGlowProperty))
            {
                chargeBlock ??= new MaterialPropertyBlock();
                chargeRenderer.GetPropertyBlock(chargeBlock);
                chargeBlock.SetFloat(chargeGlowProperty, chargeGlowIntensity);
                chargeRenderer.SetPropertyBlock(chargeBlock);
            }

            foreach (ParticleSystem system in chargeParticles.GetComponentsInChildren<ParticleSystem>(true))
            {
                var systemMain = system.main;
                systemMain.simulationSpeed = chargeSimulationSpeed;
            }

            chargeParticles.Play(true);
            charging = true;

            stopEmitAt = Time.time + ChargeDuration;
        }

        private void OnDisable()
        {
            charging = false;
            if (chargeParticles != null)
                chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

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

            if (flashOnAnticipation && hitFlash != null && !hitFlash.IsFlashing)
                hitFlash.Flash(color, flashIntensity, flashMode);

            PlayCharge(color);

            activeParticleSystem = PSpawner.Spawn("anticipation", col.bounds.center, Quaternion.identity);
            if (activeParticleSystem == null) return;

            if (!anticipationBaseScales.TryGetValue(activeParticleSystem, out Vector3 baseScale))
            {
                baseScale = activeParticleSystem.transform.localScale;
                anticipationBaseScales[activeParticleSystem] = baseScale;
            }
            activeParticleSystem.transform.localScale = overrideAnticipationScale ? baseScale * anticipationScale : baseScale;

            var main = activeParticleSystem.main;
            main.startColor = color;
        }

        public Color GetColorForForce(AttackForce force)
        {
            if (!overrideColors && GameManager.Instance != null)
                return GameManager.Instance.GetAttackForceColor(force);

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