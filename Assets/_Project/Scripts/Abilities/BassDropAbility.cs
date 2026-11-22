using System;
using TheLastBeat.Characters;
using TheLastBeat.Enemies;
using TheLastBeat.Heroes;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Golpe de bajo de la bardo (habilidad defensiva): sube el laúd, pisa y suelta una onda que daña y empuja
    /// hacia fuera a los enemigos que tiene alrededor. El golpe es pesado, así que además les corta el ataque
    /// que estuvieran preparando. Con Stun Seconds mayor que 0 en los datos, también los aturde (nivel 2).
    ///
    /// La onda sale en el instante del pisotón, que avisa BardActionMoments leyendo la animación; si ese aviso no
    /// llegara, sale igual un poco después de Hit Delay. Mientras dura, la bardo no se mueve (BardCompanion.AddMoveLock).
    /// El efecto visual escucha el evento Dropped. Necesita un asset BassDropAbilityData en el campo Data.
    /// </summary>
    public class BassDropAbility : Ability
    {
        // Margen sobre Hit Delay antes de soltar la onda sin esperar más al aviso de la animación.
        private const float MomentGrace = 0.3f;

        [Tooltip("Si se deja vacío, usa el BardActionMoments de este objeto.")]
        [SerializeField] private BardActionMoments moments;

        private readonly Collider[] stunBuffer = new Collider[32];
        private BardCompanion companion;
        private float timer;
        private bool dropped;
        private bool lockHeld;

        private BassDropAbilityData Bass => Data as BassDropAbilityData;

        /// <summary>Salió la onda: punto del suelo bajo la bardo y radio real (para el efecto y el sonido).</summary>
        public event Action<Vector3, float> Dropped;

        private void Awake()
        {
            companion = GetComponent<BardCompanion>();
            if (moments == null) moments = GetComponent<BardActionMoments>();
        }

        private void OnEnable()
        {
            if (moments != null) moments.Hit += OnMoment;
        }

        protected override void OnDisable()
        {
            if (moments != null) moments.Hit -= OnMoment;
            base.OnDisable();
        }

        public override bool CanStart()
        {
            if (Bass == null)
            {
                Debug.LogError($"[{nameof(BassDropAbility)}] El campo Data necesita un asset de tipo Golpe de bajo (BassDropAbilityData).", this);
                return false;
            }
            return base.CanStart();
        }

        protected override void OnBegin()
        {
            timer = 0f;
            dropped = false;
            if (companion != null && !lockHeld)
            {
                companion.AddMoveLock();
                lockHeld = true;
            }
        }

        private void Update()
        {
            if (!IsRunning) return;

            timer += Time.deltaTime;
            if (!dropped && timer >= Bass.hitDelay + MomentGrace) Drop();
            if (timer >= Bass.castTime) End();
        }

        private void OnMoment(BardAction action)
        {
            if (action == BardAction.BassDrop && IsRunning && !dropped) Drop();
        }

        private void Drop()
        {
            dropped = true;

            float radius = Bass.radius * Caster.AreaMultiplier;
            Vector3 feet = transform.position;
            Vector3 center = feet + Vector3.up;

            // HitDetector empuja a cada enemigo en la dirección que va de la bardo hacia él: todos salen hacia fuera.
            Hits.BeginAttack();
            Hits.SphereHit(center, radius, Caster.TargetMask,
                           MakeDamage(Bass.damage, center, transform.forward, Bass.knockback, Bass.staggers));

            if (Bass.stunSeconds > 0f) StunAround(center, radius, Bass.stunSeconds);

            if (Dropped != null) Dropped.Invoke(feet, radius);
        }

        private void StunAround(Vector3 center, float radius, float seconds)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, stunBuffer, Caster.TargetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider col = stunBuffer[i];
                stunBuffer[i] = null;
                if (col == null) continue;

                EnemySoldier soldier = col.GetComponentInParent<EnemySoldier>();
                if (soldier != null && !soldier.IsDead) soldier.Stun(seconds);
            }
        }

        protected override void OnEnd()
        {
            if (lockHeld)
            {
                if (companion != null) companion.RemoveMoveLock();
                lockHeld = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            BassDropAbilityData d = Data as BassDropAbilityData;
            if (d == null) return;
            Gizmos.color = new Color(0.62f, 0.2f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, d.radius);
        }
    }
}
