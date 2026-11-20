using System.Collections.Generic;
using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Busca objetivos en un área y les aplica daño. No es un MonoBehaviour: cada ataque o
    /// habilidad tiene su propia instancia.
    ///
    /// Dos garantías:
    ///  - Sin basura en memoria por golpe: usa OverlapSphereNonAlloc / OverlapBoxNonAlloc
    ///    con un arreglo reservado una sola vez.
    ///  - Un objetivo recibe un solo golpe por ataque, aunque tenga varios colliders o el área
    ///    se revise en varios frames. BeginAttack() abre un ataque nuevo.
    /// </summary>
    public sealed class HitDetector
    {
        private readonly Collider[] buffer;
        private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

        public HitDetector(int capacity = 32)
        {
            buffer = new Collider[Mathf.Max(1, capacity)];
        }

        /// <summary>Empieza un ataque nuevo: los objetivos ya golpeados pueden recibir daño otra vez.</summary>
        public void BeginAttack() => alreadyHit.Clear();

        /// <summary>Golpea todo lo que haya dentro de una esfera. Devuelve cuántos golpes se aplicaron.</summary>
        public int SphereHit(Vector3 center, float radius, LayerMask mask, DamageInfo template)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, mask, QueryTriggerInteraction.Collide);
            return Apply(count, center, template);
        }

        /// <summary>Golpea todo lo que haya dentro de una caja orientada. Devuelve cuántos golpes se aplicaron.</summary>
        public int BoxHit(Vector3 center, Vector3 halfExtents, Quaternion rotation, LayerMask mask, DamageInfo template)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, rotation, mask, QueryTriggerInteraction.Collide);
            return Apply(count, center, template);
        }

        private int Apply(int count, Vector3 origin, DamageInfo template)
        {
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                buffer[i] = null;
                if (col == null) continue;

                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;
                if (!alreadyHit.Add(target)) continue;

                DamageInfo info = template;
                info.HitPoint = col.bounds.center;

                // El empuje sale desde quien ataca, no desde el centro del área: el área del combo
                // está 1 m por delante del caballero y un enemigo pegado a él queda "detrás" de ese
                // centro, lo que lo empujaría hacia el caballero.
                Vector3 from = template.Source != null ? template.Source.transform.position : origin;
                Vector3 dir = target.Transform.position - from;
                dir.y = 0f;
                info.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : template.Direction;

                if (target.TakeDamage(info))
                {
                    landed++;
                    CombatEvents.RaiseHit(target, info);
                }
            }
            return landed;
        }
    }
}
