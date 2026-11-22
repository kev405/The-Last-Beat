using System.Collections.Generic;
using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using TheLastBeat.Enemies;
using UnityEngine;

namespace TheLastBeat.Heroes
{
    /// <summary>
    /// Decide cuándo la bardo ataca y usa sus habilidades. BardCompanion sigue decidiendo por dónde se mueve.
    /// Reglas, revisadas cada Think Interval y por orden de prioridad:
    ///
    ///  1. Golpe de bajo: si hay Enemies To Drop o más enemigos dentro del radio de peligro (el de BardCompanion,
    ///     4 m), o si uno de ellos empieza a preparar un golpe contra ella (EnemySoldier.IsPreparingAttack).
    ///  2. Pista de baile: si algún héroe a su alcance tiene la vida por debajo de Heal Below, y no hay ningún
    ///     enemigo a menos de Safe Distance ni preparando un golpe contra ella (lanzarla la deja clavada).
    ///  3. Nota: solo si no hay ningún enemigo dentro del radio de peligro; si lo hay, no dispara y BardCompanion huye.
    ///     Apunta al enemigo vivo más cercano dentro del alcance que no esté tapado por una pared, y deja pasar
    ///     Note Interval entre un disparo y el siguiente.
    ///
    /// Las recargas y el "una habilidad a la vez" los lleva AbilityCaster; BardBasicAttack impide disparar otra
    /// Nota antes de que termine la animación de la anterior.
    /// Auto Cast en false la deja sin decidir nada, para probar a mano con el probador de teclado.
    /// En la entrega final esto pasa a la IA de compañeros (tareas 51 y 52).
    /// </summary>
    [RequireComponent(typeof(BardCompanion))]
    public class BardCombatAI : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private BardCompanion companion;
        [SerializeField] private AbilityCaster caster;
        [SerializeField] private BardBasicAttack attack;
        [SerializeField] private Health health;
        [SerializeField] private HeroTarget self;

        [Header("Decisión")]
        [Tooltip("Apágalo para que no use nada por su cuenta y probar con las teclas 7, 8 y 9.")]
        [SerializeField] private bool autoCast = true;
        [Tooltip("Cada cuánto decide. Corto, para alcanzar a responder cuando un enemigo prepara un golpe.")]
        [SerializeField, Min(0.02f)] private float thinkInterval = 0.05f;

        [Header("Golpe de bajo")]
        [SerializeField, Min(0)] private int bassDropSlot = 0;
        [Tooltip("Enemigos dentro del radio de peligro a partir de los cuales lo lanza.")]
        [SerializeField, Min(1)] private int enemiesToDrop = 2;
        [Tooltip("También lo lanza si un enemigo cercano empieza a preparar un golpe contra ella.")]
        [SerializeField] private bool dropWhenTargeted = true;

        [Header("Pista de baile")]
        [SerializeField, Min(0)] private int danceFloorSlot = 1;
        [Tooltip("La lanza si un héroe a su alcance tiene la vida por debajo de esta fracción.")]
        [SerializeField, Range(0f, 1f)] private float healBelow = 0.7f;
        [Tooltip("No la lanza con un enemigo más cerca que esto.")]
        [SerializeField, Min(0f)] private float safeDistance = 2.5f;

        [Header("Nota")]
        [SerializeField] private bool useNote = true;
        [Tooltip("Segundos entre el inicio de un disparo y el siguiente. Nunca dispara antes de que termine la animación.")]
        [SerializeField, Min(0f)] private float noteInterval = 0.9f;

        private readonly Collider[] buffer = new Collider[48];
        private readonly HashSet<IDamageable> seen = new HashSet<IDamageable>();
        private LayerMask enemyMask;
        private LayerMask obstacleMask;
        private float timer;
        private float noteTimer;

        // Resultado de la última revisión.
        private int nearCount;
        private bool targeted;
        private float nearestDistance;
        private Transform noteTarget;

        public bool AutoCast
        {
            get { return autoCast; }
            set { autoCast = value; }
        }

        private void Awake()
        {
            if (companion == null) companion = GetComponent<BardCompanion>();
            if (caster == null) caster = GetComponent<AbilityCaster>();
            if (attack == null) attack = GetComponent<BardBasicAttack>();
            if (health == null) health = GetComponent<Health>();
            if (self == null) self = GetComponent<HeroTarget>();
            enemyMask = GameLayers.EnemyMask;
            obstacleMask = GameLayers.ObstacleMask;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (noteTimer > 0f) noteTimer -= dt;
            if (!autoCast) return;

            timer -= dt;
            if (timer > 0f) return;
            timer = thinkInterval;

            Think();
        }

        private void Think()
        {
            if (health != null && health.IsDead) return;
            if (caster != null && caster.IsCasting) return;

            float danger = companion != null ? companion.DangerRadius : 4f;
            float noteRange = attack != null ? attack.Range : 0f;
            Scan(danger, noteRange);

            if (caster != null)
            {
                // 1. Golpe de bajo: se los quita de encima.
                bool surrounded = nearCount >= enemiesToDrop || (dropWhenTargeted && targeted);
                if (surrounded && IsReady(bassDropSlot) && caster.TryUse(bassDropSlot)) return;

                // 2. Pista de baile: cura al grupo cuando puede quedarse quieta sin que la golpeen.
                bool safe = nearestDistance > safeDistance && !targeted;
                if (safe && IsReady(danceFloorSlot) && SomeoneNeedsHealing(danceFloorSlot) && caster.TryUse(danceFloorSlot)) return;
            }

            // 3. Nota: solo con el radio de peligro despejado.
            if (useNote && attack != null && nearCount == 0 && noteTarget != null && noteTimer <= 0f &&
                attack.TryAttack(noteTarget))
            {
                noteTimer = noteInterval;
            }
        }

        private bool IsReady(int slot)
        {
            return slot < caster.SlotCount && caster.GetAbility(slot) != null && caster.GetCooldownRemaining(slot) <= 0f;
        }

        /// <summary>
        /// Cuenta los enemigos vivos dentro del radio de peligro, mira si alguno prepara un golpe contra ella y
        /// busca el más cercano a tiro para la Nota. Un enemigo con varios colliders cuenta una sola vez.
        /// </summary>
        private void Scan(float danger, float noteRange)
        {
            nearCount = 0;
            targeted = false;
            nearestDistance = float.MaxValue;
            noteTarget = null;
            seen.Clear();

            Vector3 position = transform.position;
            float radius = Mathf.Max(danger, noteRange);
            float bestNote = float.MaxValue;

            int count = Physics.OverlapSphereNonAlloc(position, radius, buffer, enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                buffer[i] = null;
                if (col == null) continue;

                IDamageable enemy = col.GetComponentInParent<IDamageable>();
                if (enemy == null || enemy.IsDead || !seen.Add(enemy)) continue;

                Vector3 offset = enemy.Transform.position - position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < nearestDistance) nearestDistance = distance;

                if (distance <= danger)
                {
                    nearCount++;
                    EnemySoldier soldier = col.GetComponentInParent<EnemySoldier>();
                    if (soldier != null && self != null && soldier.IsPreparingAttack && soldier.Target == self) targeted = true;
                }

                if (distance <= noteRange && distance < bestNote && HasClearShot(position, enemy.Transform.position))
                {
                    bestNote = distance;
                    noteTarget = enemy.Transform;
                }
            }
        }

        private bool HasClearShot(Vector3 from, Vector3 to)
        {
            Vector3 a = from + Vector3.up;
            Vector3 b = to + Vector3.up;
            return !Physics.Linecast(a, b, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>¿Hay algún héroe vivo y herido que quedaría dentro de la pista si la abre aquí?</summary>
        private bool SomeoneNeedsHealing(int slot)
        {
            Ability ability = caster.GetAbility(slot);
            if (ability == null || ability.Data == null) return false;

            DanceFloorAbilityData floor = ability.Data as DanceFloorAbilityData;
            bool healsCaster = floor == null || floor.healsCaster;
            float reach = ability.Data.radius * caster.AreaMultiplier * 0.9f; // un poco de margen para que no quede en el borde
            float reach2 = reach * reach;

            Vector3 position = transform.position;
            var heroes = HeroTarget.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroTarget hero = heroes[i];
                if (hero == null || !hero.IsAlive) continue;
                if (!healsCaster && hero == self) continue;
                if (hero.Health.Normalized > healBelow) continue;

                Vector3 d = hero.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude <= reach2) return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, safeDistance);
        }
    }
}
