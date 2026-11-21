using System;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Giro del caballero (tarea 10): gira golpeando a todo lo que tiene alrededor cada
    /// tickInterval, avanza lento mientras gira y termina con una explosión en área.
    /// Necesita un asset SpinAbilityData en el campo Data.
    /// </summary>
    public class SpinAbility : Ability
    {
        [Tooltip("Objeto que gira en pantalla (el hijo Visual del caballero). Opcional.")]
        [SerializeField] private Transform spinVisual;

        private float timer;
        private float nextTick;
        private Quaternion visualStart;

        private SpinAbilityData Spin => Data as SpinAbilityData;

        /// <summary>Se produjo la explosión final: centro y radio (para VFX y sonido en las tareas 32 y 33).</summary>
        public event Action<Vector3, float> Exploded;

        public override bool CanStart()
        {
            if (Spin == null)
            {
                Debug.LogError($"[{nameof(SpinAbility)}] El campo Data necesita un asset de tipo Giro (SpinAbilityData).", this);
                return false;
            }
            return base.CanStart();
        }

        protected override void OnBegin()
        {
            timer = 0f;
            nextTick = 0f;
            Caster.Motor.SpeedMultiplier = Spin.moveSpeedMultiplier;
            if (spinVisual != null) visualStart = spinVisual.localRotation;
        }

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;
            timer += dt;

            if (spinVisual != null) spinVisual.Rotate(0f, Spin.visualSpinSpeed * dt, 0f, Space.Self);

            while (timer >= nextTick && nextTick < Spin.duration)
            {
                nextTick += Spin.tickInterval;
                Hits.BeginAttack(); // cada vuelta puede golpear otra vez a los mismos
                Vector3 center = transform.position + Vector3.up;
                Hits.SphereHit(center, Spin.radius * Caster.AreaMultiplier, Caster.TargetMask,
                               MakeDamage(Spin.damage, center, transform.forward, Spin.knockback, false));
            }

            if (timer >= Spin.duration)
            {
                Explode();
                End();
            }
        }

        private void Explode()
        {
            Hits.BeginAttack();
            Vector3 center = transform.position + Vector3.up * 0.5f;
            float radius = Spin.explosionRadius * Caster.AreaMultiplier;
            Hits.SphereHit(center, radius, Caster.TargetMask,
                           MakeDamage(Spin.explosionDamage, center, transform.forward, Spin.explosionKnockback, true));
            Exploded?.Invoke(center, radius);
        }

        protected override void OnEnd()
        {
            Caster.Motor.SpeedMultiplier = 1f;
            if (spinVisual != null) spinVisual.localRotation = visualStart;
        }

        private void OnDrawGizmosSelected()
        {
            SpinAbilityData d = Data as SpinAbilityData;
            if (d == null) return;
            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, d.radius);
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, d.explosionRadius);
        }
    }
}
