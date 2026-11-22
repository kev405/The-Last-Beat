using System;
using TheLastBeat.Abilities;
using TheLastBeat.Characters;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Heroes
{
    /// <summary>
    /// Ataque básico de la bardo, la Nota: con un rasgueo dispara una nota que vuela en línea recta, de corto
    /// alcance y poco daño, y se deshace contra el primer enemigo que toca o contra una pared.
    ///
    ///  - TryAttack arranca la animación (trigger "Note") y la gira hacia el objetivo. Mientras dura la animación
    ///    (Animation Time) no se puede volver a disparar, por mucho que se insista, ni tampoco mientras lanza una
    ///    habilidad. Cada cuánto dispara la IA es cosa de BardCombatAI (Note Interval).
    ///  - La nota sale en el instante que avisa BardActionMoments (la mano cruzando las cuerdas); si ese aviso
    ///    no llegara, sale igual un poco después de Release Time.
    ///  - La nota es un punto que avanza cada fotograma y golpea con HitDetector, así que el daño llega cuando
    ///    la nota llega, igual que se ve. El golpe va marcado como silencioso (DamageInfo.Quiet): muestra su
    ///    número, pero no pausa el juego ni sacude la cámara.
    ///
    /// Disparar no corta la canción: eso solo pasa cuando la golpean (SongBuff).
    /// Quién decide cuándo disparar es BardCombatAI; el efecto visual escucha el evento Fired.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class BardBasicAttack : MonoBehaviour
    {
        // Margen sobre Release Time antes de soltar la nota sin esperar más al aviso de la animación.
        private const float MomentGrace = 0.25f;

        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private Animator animator;
        [SerializeField] private BardActionMoments moments;
        [SerializeField] private BardCompanion companion;
        [SerializeField] private AbilityCaster caster;
        [SerializeField] private Health health;

        [Header("Nota")]
        [SerializeField, Min(0f)] private float damage = 8f;
        [Tooltip("Metros que recorre la nota antes de deshacerse.")]
        [SerializeField, Min(0.5f)] private float range = 7f;
        [Tooltip("Metros por segundo.")]
        [SerializeField, Min(1f)] private float speed = 14f;
        [Tooltip("Radio de la nota para acertar a un enemigo.")]
        [SerializeField, Min(0.05f)] private float hitRadius = 0.5f;
        [SerializeField, Min(0f)] private float knockback = 0f;
        [Tooltip("Altura a la que vuela y distancia por delante de la bardo desde la que sale.")]
        [SerializeField] private Vector2 muzzle = new Vector2(1.15f, 0.45f);

        [Header("Tiempos (los pone el menú según la animación)")]
        [Tooltip("Segundos que dura la animación de la Nota. Hasta que termina no se puede volver a disparar.")]
        [SerializeField, Min(0.1f)] private float animationTime = 0.47f;
        [Tooltip("Segundos desde que empieza la animación hasta que sale la nota. Solo se usa si BardActionMoments no avisa.")]
        [SerializeField, Min(0f)] private float releaseTime = 0.2f;

        [Header("Capas")]
        [Tooltip("Lo que golpea la nota. Si queda en Nothing, usa la capa Enemy.")]
        [SerializeField] private LayerMask targetMask;
        [Tooltip("Lo que detiene la nota. Si queda en Nothing, usa la capa Ground.")]
        [SerializeField] private LayerMask obstacleMask;

        private static readonly int NoteHash = Animator.StringToHash("Note");

        private readonly Collider[] aimBuffer = new Collider[24];
        private HitDetector hits;

        private float cooldown;        // segundos que le quedan a la animación del disparo en curso
        private float elapsed;         // segundos desde que empezó el disparo en curso
        private bool releasing;        // la animación empezó y la nota aún no sale
        private Transform aimTarget;
        private Vector3 aimDirection;

        private bool flying;
        private Vector3 notePosition;
        private Vector3 noteDirection;
        private float noteLeft;

        /// <summary>Multiplicador de daño (mejoras o potenciaciones futuras).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        public float Range => range;

        /// <summary>true desde que empieza la animación del disparo hasta que termina.</summary>
        public bool IsBusy => releasing || cooldown > 0f;

        public bool CanAttack =>
            isActiveAndEnabled && !IsBusy && health != null && !health.IsDead && (caster == null || !caster.IsCasting);

        /// <summary>Empezó la animación del disparo.</summary>
        public event Action Started;

        /// <summary>Salió la nota: origen, dirección plana, alcance y velocidad. Lo escucha BardNoteVfx.</summary>
        public event Action<Vector3, Vector3, float, float> Fired;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (moments == null) moments = GetComponent<BardActionMoments>();
            if (companion == null) companion = GetComponent<BardCompanion>();
            if (caster == null) caster = GetComponent<AbilityCaster>();
            if (health == null) health = GetComponent<Health>();
            if (targetMask.value == 0) targetMask = GameLayers.EnemyMask;
            if (obstacleMask.value == 0) obstacleMask = GameLayers.ObstacleMask;
            hits = new HitDetector(8);
        }

        private void OnEnable()
        {
            if (moments != null) moments.Hit += OnMoment;
        }

        private void OnDisable()
        {
            if (moments != null) moments.Hit -= OnMoment;
            releasing = false;
            flying = false;
        }

        /// <summary>Dispara hacia ese objetivo; lo sigue con la mirada hasta que la nota sale.</summary>
        public bool TryAttack(Transform target)
        {
            if (!CanAttack) return false;
            aimTarget = target;
            Begin(target != null ? target.position - transform.position : transform.forward);
            return true;
        }

        /// <summary>Dispara en esa dirección.</summary>
        public bool TryAttack(Vector3 direction)
        {
            if (!CanAttack) return false;
            aimTarget = null;
            Begin(direction);
            return true;
        }

        /// <summary>Dispara al enemigo vivo más cercano dentro del alcance o, si no hay, hacia donde mira.</summary>
        public bool TryAttackNearest()
        {
            if (!CanAttack) return false;
            Transform nearest = FindNearestEnemy();
            return nearest != null ? TryAttack(nearest) : TryAttack(transform.forward);
        }

        private void Begin(Vector3 direction)
        {
            aimDirection = Flatten(direction, transform.forward);
            elapsed = 0f;
            releasing = true;
            cooldown = animationTime;

            if (companion != null) companion.AimAt(transform.position + aimDirection * 5f, animationTime);
            if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(NoteHash);
            if (Started != null) Started.Invoke();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (cooldown > 0f) cooldown -= dt;

            if (health != null && health.IsDead)
            {
                releasing = false;
                flying = false;
                return;
            }

            if (releasing)
            {
                elapsed += dt;
                FollowTarget();
                if (elapsed >= releaseTime + MomentGrace) Release();
            }

            if (flying) StepNote(dt);
        }

        private void OnMoment(BardAction action)
        {
            if (action == BardAction.Note && releasing) Release();
        }

        /// <summary>Mientras prepara el disparo, sigue al objetivo por si se mueve.</summary>
        private void FollowTarget()
        {
            if (aimTarget == null || !aimTarget.gameObject.activeInHierarchy) return;
            aimDirection = Flatten(aimTarget.position - transform.position, aimDirection);
            if (companion != null) companion.AimAt(aimTarget.position, 0.15f);
        }

        private void Release()
        {
            releasing = false;
            FollowTarget();

            Vector3 origin = transform.position + Vector3.up * muzzle.x + aimDirection * muzzle.y;
            hits.BeginAttack();
            flying = true;
            notePosition = origin;
            noteDirection = aimDirection;
            noteLeft = range;

            if (Fired != null) Fired.Invoke(origin, aimDirection, range, speed);
        }

        /// <summary>Avanza la nota un paso y golpea lo que haya en su sitio. Se deshace al acertar, al tocar una pared o al acabar el alcance.</summary>
        private void StepNote(float dt)
        {
            if (noteLeft <= 0f)
            {
                flying = false;
                return;
            }

            float move = Mathf.Min(speed * dt, noteLeft);
            if (move <= 0f) return; // el juego está en pausa: la nota espera donde está

            // Avanza en tramos no mayores que su radio, para que un fotograma lento no la haga saltarse a un enemigo.
            float maxStep = Mathf.Max(0.05f, hitRadius);
            while (move > 0f)
            {
                float step = Mathf.Min(move, maxStep);
                move -= step;

                if (Physics.Raycast(notePosition, noteDirection, step, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    flying = false;
                    return;
                }

                notePosition += noteDirection * step;
                noteLeft -= step;

                DamageInfo info = DamageInfo.Create(damage * DamageMultiplier, gameObject, health.Team,
                                                    notePosition, noteDirection, knockback, true, false);
                info.Quiet = true;
                int landed = hits.SphereHit(notePosition, hitRadius, targetMask, info);

                if (landed > 0 || noteLeft <= 0f)
                {
                    flying = false;
                    return;
                }
            }
        }

        private Transform FindNearestEnemy()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, aimBuffer, targetMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            Transform nearest = null;

            for (int i = 0; i < count; i++)
            {
                Collider col = aimBuffer[i];
                aimBuffer[i] = null;
                if (col == null) continue;

                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;

                Vector3 d = target.Transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    nearest = target.Transform;
                }
            }
            return nearest;
        }

        private static Vector3 Flatten(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) return direction.normalized;

            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.85f, 0.7f);
            Vector3 origin = transform.position + Vector3.up * muzzle.x;
            Gizmos.DrawLine(origin, origin + transform.forward * range);
            Gizmos.DrawWireSphere(origin + transform.forward * range, hitRadius);
        }
    }
}
