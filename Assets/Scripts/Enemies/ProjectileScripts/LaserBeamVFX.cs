using System;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    [RequireComponent(typeof(ParticleSystem))]
    [RequireComponent(typeof(LineRenderer))]
    public class LaserBeamVFX : MonoBehaviour
    {
        [Header("Warning Pulse")]
        [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.1f, 1f);
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float pulseMinAlpha = 0.15f;
        [SerializeField] private float pulseMaxAlpha = 0.9f;
        
        private ParticleSystem ps;
        private LineRenderer warningLine;
        private bool warningActive;

        public event Action<Collider2D> BeamTouched;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            
            warningLine = GetComponent<LineRenderer>();
            warningLine.useWorldSpace = false;
            warningLine.positionCount = 2;
            warningLine.enabled = false;
        }

        public void SetLength(float length, float width)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(length, width, 0.01f);
            shape.position = new Vector3(length / 2f, 0f, 0f);
        }

        public void Play() => ps.Play();
        public void Stop() => ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        public void SetWarningColor(Color color) => warningColor = color;

        public void SetWarningWidth(float width)
        {
            warningLine.startWidth = width;
            warningLine.endWidth = width;
        }

        public void ShowWarning(float length, float width)
        {
            warningActive = true;
            warningLine.enabled = true;
            warningLine.SetPosition(0, Vector3.zero);
            warningLine.SetPosition(1, new Vector3(length, 0f, 0f));
            warningLine.startWidth = width;
            warningLine.endWidth = width;
        }

        public void HideWarning()
        {
            warningActive = false;
            warningLine.enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => BeamTouched?.Invoke(other);
        private void OnTriggerStay2D(Collider2D other) => BeamTouched?.Invoke(other);

        private void Update()
        {
            if (!warningActive) return;

            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);

            Color c = warningColor;
            c.a = alpha;
            warningLine.startColor = c;
            warningLine.endColor = c;
        }
    }
}