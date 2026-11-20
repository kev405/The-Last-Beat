using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Embestida del caballero (tarea 11): carga corta en línea recta que golpea a todo lo
    /// que atraviesa. Usa la dirección del stick o, si no se está moviendo, hacia donde mira.
    /// Se mueve con el CharacterController, así que se detiene contra las paredes.
    /// Necesita un asset ChargeAbilityData en el campo Data.
    /// </summary>
    public class ChargeAbility : Ability
    {
        private float timer;
        private Vector3 direction;
        private bool lockHeld;

        private ChargeAbilityData Charge => Data as ChargeAbilityData;

        public override bool CanStart()
        {
            if (Charge == null)
            {
                Debug.LogError($"[{nameof(ChargeAbility)}] El campo Data necesita un asset de tipo Embestida (ChargeAbilityData).", this);
                return false;
            }
            return base.CanStart();
        }

        protected override void OnBegin()
        {
            timer = 0f;

            Vector3 wanted = Caster.Motor.DesiredDirection;
            direction = wanted.sqrMagnitude > 0.01f ? wanted : transform.forward;
            direction.y = 0f;
            direction.Normalize();

            Caster.Motor.FaceDirection(direction);
            Caster.Motor.StopImmediately();
            Caster.Motor.AddMoveLock();
            lockHeld = true;
        }

        private void Update()
        {
            if (!IsRunning) return;

            float dt = Time.deltaTime;
            float previous = timer;
            timer += dt;

            if (previous < Charge.duration)
            {
                float slice = Mathf.Min(timer, Charge.duration) - previous;
                Vector3 before = transform.position;
                Caster.Motor.ExternalMove(direction * (Charge.distance / Charge.duration * slice));
                Vector3 after = transform.position;

                // Golpea el tramo recorrido en este frame, con un poco de margen hacia adelante.
                float length = Vector3.Distance(before, after);
                Vector3 center = (before + after) * 0.5f + Vector3.up + direction * 0.3f;
                Vector3 halfExtents = new Vector3(Charge.width * 0.5f, 1f, length * 0.5f + 0.6f);
                Hits.BoxHit(center, halfExtents, Quaternion.LookRotation(direction), Caster.TargetMask,
                            MakeDamage(Charge.damage, center, direction, Charge.knockback, false));
            }
            else if (timer >= Charge.duration + Charge.recovery)
            {
                End();
            }
        }

        protected override void OnEnd()
        {
            if (lockHeld)
            {
                Caster.Motor.RemoveMoveLock();
                lockHeld = false;
            }
            Caster.Motor.StopImmediately();
        }

        private void OnDrawGizmosSelected()
        {
            ChargeAbilityData d = Data as ChargeAbilityData;
            if (d == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up + transform.forward * (d.distance * 0.5f),
                                          transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(d.width, 2f, d.distance));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
