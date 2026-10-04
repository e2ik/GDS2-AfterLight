using System;
using UnityEngine;

namespace Enemies.ProjectileScripts
{
    [RequireComponent(typeof(ParticleSystem))]
    public class LaserBeamVFX : MonoBehaviour
    {
        private ParticleSystem ps;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
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
    }
}