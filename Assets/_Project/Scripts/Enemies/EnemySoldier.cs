using System;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using TheLastBeat.Feedback;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Enemies
{
    /// <summary>
    /// Demonio soldado (tarea 13). Máquina de estados sobre un NavMeshAgent:
    ///
    ///   Perseguir ──(en rango y sin recarga)──▶ Preparar ──(windup)──▶ golpe ──▶ Recuperar ──▶ Perseguir
    ///        ▲                                     │ golpe pesado recibido
    ///        └────────── Aturdido / Tambaleo ◀─────┘
    ///   Cualquier estado ──(vida 0)──▶ Muerto ──(se hunde)──▶ vuelve al pool
    ///
    /// Elige objetivo con HeroTarget.FindBest: el héroe más cercano, salvo que un héroe con
    /// prioridad (la bardo, tarea 16) esté cerca. Antes de golpear muestra un aviso en el
    /// suelo (attackIndicator) para que el jugador pueda reaccionar.
    /// Compatible con pooling: EnemyPool llama a ResetForSpawn y escucha Released.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class EnemySoldier : MonoBehaviour
    {
        private enum State { Chase, Windup, Recover, Stagger, Stunned, Dead }

        [Header("Referencias")]
        [Tooltip("Disco rojo en el suelo que se muestra mientras prepara el golpe.")]
        [SerializeField] private GameObject attackIndicator;
        [Tooltip("Opcional hasta la tarea 28. Parámetros Speed, Attack, Hit y Die.")]
        [SerializeField] private Animator animator;

        [Header("Ataque")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [Tooltip("Distancia al objetivo a la que empieza a preparar el golpe.")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.7f;
        [SerializeField, Min(0.1f)] private float hitRadius = 1.2f;
        [SerializeField, Min(0f)] private float hitForward = 1.0f;
        [Tooltip("Tiempo de aviso antes de golpear.")]
        [SerializeField, Min(0f)] private float windupTime = 0.55f;
        [SerializeField, Min(0f)] private float recoverTime = 0.6f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.0f;
        [SerializeField, Min(0f)] private float knockback = 2f;
        [SerializeField] private bool interrupts = true;

        [Header("Reacciones")]
        [Tooltip("Cuánto se tambalea al recibir un golpe pesado (cancela su ataque).")]
        [SerializeField, Min(0f)] private float staggerTime = 0.35f;
        [SerializeField, Min(0f)] private float knockbackDeceleration = 18f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;

        [Header("IA")]
        [Tooltip("Cada cuánto vuelve a elegir objetivo.")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.4f;
        [Tooltip("Cada cuánto actualiza el destino del NavMeshAgent.")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;

        [Header("Muerte")]
        [SerializeField, Min(0f)] private float deathDuration = 1.0f;
        [SerializeField, Min(0f)] private float sinkDistance = 1.2f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private NavMeshAgent agent;
        private Health health;
        private HitFlash hitFlash;
        private Collider[] colliders;
        private HitDetector hits;

        private State state;
        private float stateTimer;
        private float stateDuration;
        private float cooldownTimer;
        private float retargetTimer;
        private float repathTimer;
        private HeroTarget target;
        private Vector3 push;
        private Vector3 deathStart;
        private bool released;

        /// <summary>Murió (para contar las bajas de la horda).</summary>
        public event Action<EnemySoldier> Died;

        /// <summary>Terminó de hundirse y puede volver al pool.</summary>
        public event Action<EnemySoldier> Released;

        public bool IsDead => state == State.Dead;
        public Health Health => health;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            hitFlash = GetComponent<HitFlash>();
            colliders = GetComponentsInChildren<Collider>(true);
            hits = new HitDetector(8);
            if (attackIndicator != null) attackIndicator.SetActive(false);
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        /// <summary>Deja al soldado como nuevo en una posición. Lo llama EnemyPool al sacarlo del pool.</summary>
        public void ResetForSpawn(Vector3 position, Quaternion rotation)
        {
            released = false;
            health.ResetHealth();
            if (hitFlash != null) hitFlash.ResetFlash();
            foreach (Collider c in colliders) c.enabled = true;

            agent.enabled = true;
            agent.Warp(position);
            transform.rotation = rotation;
            agent.isStopped = false;

            push = Vector3.zero;
            target = null;
            retargetTimer = 0f;
            repathTimer = 0f;
            cooldownTimer = UnityEngine.Random.Range(0f, 0.5f); // que no ataquen todos al mismo tiempo
            SetState(State.Chase);
        }

        /// <summary>Lo deja aturdido (lo usará el demonio tanque en la tarea 53).</summary>
        public void Stun(float seconds)
        {
            if (state == State.Dead) return;
            EnterTimedState(State.Stunned, Mathf.Max(seconds, state == State.Stunned ? stateDuration - stateTimer : 0f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (state == State.Dead)
            {
                UpdateDeath(dt);
                return;
            }

            cooldownTimer -= dt;
            ApplyPush(dt);

            retargetTimer -= dt;
            if (retargetTimer <= 0f || target == null || !target.IsAlive)
            {
                target = HeroTarget.FindBest(transform.position);
                retargetTimer = retargetInterval;
            }

            stateTimer += dt;
            switch (state)
            {
                case State.Chase: UpdateChase(dt); break;
                case State.Windup: UpdateWindup(dt); break;
                case State.Recover:
                    if (stateTimer >= recoverTime)
                    {
                        cooldownTimer = attackCooldown;
                        SetState(State.Chase);
                    }
                    break;
                case State.Stagger:
                case State.Stunned:
                    if (stateTimer >= stateDuration) SetState(State.Chase);
                    break;
            }

            if (animator != null)
            {
                float speed = agent.enabled && agent.speed > 0f ? agent.velocity.magnitude / agent.speed : 0f;
                animator.SetFloat(SpeedHash, speed, 0.1f, dt);
            }
        }

        private void UpdateChase(float dt)
        {
            if (target == null)
            {
                agent.isStopped = true;
                return;
            }

            agent.isStopped = false;
            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                agent.SetDestination(target.transform.position);
                repathTimer = repathInterval;
            }

            if (FlatDistanceTo(target.transform.position) <= attackRange && cooldownTimer <= 0f)
            {
                StartWindup();
            }
        }

        private void StartWindup()
        {
            agent.isStopped = true;
            agent.ResetPath();
            SetState(State.Windup);
            if (attackIndicator != null) attackIndicator.SetActive(true);
            if (animator != null) animator.SetTrigger(AttackHash);
        }

        private void UpdateWindup(float dt)
        {
            if (target != null) FaceTowards(target.transform.position, dt);
            if (stateTimer >= windupTime)
            {
                Strike();
                SetState(State.Recover);
            }
        }

        private void Strike()
        {
            hits.BeginAttack();
            Vector3 center = transform.position + Vector3.up + transform.forward * hitForward;
            DamageInfo info = DamageInfo.Create(damage, gameObject, health.Team, center, transform.forward,
                                                knockback, interrupts);
            hits.SphereHit(center, hitRadius, GameLayers.HeroesMask, info);
        }

        private void OnDamaged(Health h, DamageInfo info)
        {
            if (state == State.Dead) return;
            if (info.Knockback > 0f) push += info.Direction * info.Knockback;
            if (animator != null) animator.SetTrigger(HitHash);

            // Un golpe pesado cancela el ataque que estaba preparando.
            if (info.IsHeavy && state != State.Stunned) EnterTimedState(State.Stagger, staggerTime);
        }

        private void OnDied(Health h, DamageInfo info)
        {
            SetState(State.Dead);
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }
            foreach (Collider c in colliders) c.enabled = false;
            deathStart = transform.position;
            if (animator != null) animator.SetTrigger(DieHash);
            Died?.Invoke(this);
        }

        private void UpdateDeath(float dt)
        {
            float k = deathDuration > 0f ? Mathf.Clamp01(stateTimer / deathDuration) : 1f;
            stateTimer += dt;
            transform.position = deathStart + Vector3.down * (sinkDistance * k);

            if (stateTimer >= deathDuration && !released)
            {
                released = true;
                Released?.Invoke(this);
            }
        }

        private void EnterTimedState(State s, float duration)
        {
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            SetState(s);
            stateDuration = duration;
        }

        private void SetState(State s)
        {
            state = s;
            stateTimer = 0f;
            if (s != State.Windup && attackIndicator != null) attackIndicator.SetActive(false);
        }

        private void ApplyPush(float dt)
        {
            if (push.sqrMagnitude < 0.0001f || !agent.enabled) return;
            agent.Move(push * dt);
            push = Vector3.MoveTowards(push, Vector3.zero, knockbackDeceleration * dt);
        }

        private void FaceTowards(Vector3 point, float dt)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
        }

        private float FlatDistanceTo(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * hitForward, hitRadius);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
