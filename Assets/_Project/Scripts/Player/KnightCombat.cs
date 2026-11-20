using System;
using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Combo básico del caballero (tarea 8): tres golpes encadenados con el botón de ataque.
    /// Al empezar cada golpe se gira hacia el enemigo más cercano (ayuda de apuntado) y
    /// bloquea el movimiento hasta terminar. El daño lo aplica un HitDetector.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class KnightCombat : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController motor;
        [SerializeField] private Health health;
        [Tooltip("Opcional hasta la tarea 28. Triggers Attack1, Attack2 y Attack3.")]
        [SerializeField] private Animator animator;

        [Header("Combo")]
        [SerializeField] private AttackStep[] combo =
        {
            new AttackStep { name = "Corte 1", damage = 20f, radius = 1.3f, forwardOffset = 1.0f,
                             hitTime = 0.14f, duration = 0.40f, comboWindowStart = 0.12f,
                             lungeDistance = 0.4f, lungeTime = 0.10f },
            new AttackStep { name = "Corte 2", damage = 22f, radius = 1.3f, forwardOffset = 1.0f,
                             hitTime = 0.14f, duration = 0.42f, comboWindowStart = 0.12f,
                             lungeDistance = 0.4f, lungeTime = 0.10f },
            new AttackStep { name = "Golpe final", damage = 40f, radius = 1.6f, forwardOffset = 1.2f,
                             knockback = 6f, isHeavy = true,
                             hitTime = 0.22f, duration = 0.65f, comboWindowStart = 0.65f,
                             lungeDistance = 0.8f, lungeTime = 0.15f }
        };

        [Header("Objetivos")]
        [Tooltip("Capas que golpea. Si queda en Nothing, usa la capa Enemy.")]
        [SerializeField] private LayerMask targetMask;
        [Tooltip("Radio en el que busca un enemigo para girarse hacia él al atacar.")]
        [SerializeField, Min(0f)] private float autoAimRadius = 4f;

        [Header("Depuración")]
        [SerializeField] private bool drawGizmos = true;

        private static readonly int[] AttackTriggers =
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3")
        };

        private readonly Collider[] aimBuffer = new Collider[16];
        private HitDetector hits;
        private AbilityCaster caster;
        private int step = -1;
        private float timer;
        private bool hitDone;
        private bool queued;
        private bool lockHeld;

        /// <summary>Multiplicador de daño (la potenciación de la bardo lo sube en la tarea 19).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        public bool IsAttacking => step >= 0;
        public int CurrentStep => step;

        /// <summary>Empezó un golpe del combo (índice 0, 1 o 2).</summary>
        public event Action<int> AttackStarted;

        /// <summary>Se aplicó el daño de un golpe: índice del golpe y cuántos objetivos recibieron daño.</summary>
        public event Action<int, int> AttackLanded;

        private Team MyTeam => health != null ? health.Team : Team.Heroes;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (motor == null) motor = GetComponent<PlayerController>();
            if (health == null) health = GetComponent<Health>();
            caster = GetComponent<AbilityCaster>();
            if (targetMask.value == 0) targetMask = GameLayers.EnemyMask;
            hits = new HitDetector(32);
        }

        private void OnEnable()
        {
            if (input != null) input.AttackPressed += OnAttackPressed;
        }

        private void OnDisable()
        {
            if (input != null) input.AttackPressed -= OnAttackPressed;
            CancelAttack();
        }

        private void OnAttackPressed()
        {
            if (health != null && health.IsDead) return;
            if (caster != null && caster.IsCasting) return;
            if (combo == null || combo.Length == 0) return;

            if (!IsAttacking)
            {
                StartStep(0);
            }
            else if (step < combo.Length - 1 && timer >= combo[step].comboWindowStart)
            {
                queued = true;
            }
        }

        private void Update()
        {
            if (!IsAttacking) return;

            AttackStep s = combo[step];
            float dt = Time.deltaTime;
            float previous = timer;
            timer += dt;

            // Avance del golpe: reparte lungeDistance a lo largo de lungeTime.
            if (s.lungeTime > 0f && previous < s.lungeTime)
            {
                float slice = Mathf.Min(timer, s.lungeTime) - previous;
                motor.ExternalMove(transform.forward * (s.lungeDistance / s.lungeTime * slice));
            }

            if (!hitDone && timer >= s.hitTime)
            {
                hitDone = true;
                ApplyHit(s);
            }

            if (timer >= s.duration)
            {
                if (queued && step < combo.Length - 1) StartStep(step + 1);
                else EndCombo();
            }
        }

        /// <summary>Corta el combo de inmediato (lo usan las habilidades al lanzarse).</summary>
        public void CancelAttack()
        {
            if (IsAttacking) EndCombo();
        }

        private void StartStep(int index)
        {
            step = index;
            timer = 0f;
            hitDone = false;
            queued = false;

            if (!lockHeld)
            {
                motor.AddMoveLock();
                lockHeld = true;
            }
            motor.StopImmediately();
            AimAtNearestEnemy();
            hits.BeginAttack();

            if (animator != null && index < AttackTriggers.Length) animator.SetTrigger(AttackTriggers[index]);
            AttackStarted?.Invoke(index);
        }

        private void ApplyHit(AttackStep s)
        {
            Vector3 center = HitCenter(s);
            DamageInfo info = DamageInfo.Create(s.damage * DamageMultiplier, gameObject, MyTeam,
                                                center, transform.forward, s.knockback, true, s.isHeavy);
            int landed = hits.SphereHit(center, s.radius, targetMask, info);
            AttackLanded?.Invoke(step, landed);
        }

        private void EndCombo()
        {
            step = -1;
            queued = false;
            if (lockHeld)
            {
                motor.RemoveMoveLock();
                lockHeld = false;
            }
        }

        /// <summary>
        /// Si hay un enemigo vivo cerca, se gira hacia el más cercano. Si no, hacia donde
        /// el jugador está empujando el stick. Si tampoco, mantiene la dirección actual.
        /// </summary>
        private void AimAtNearestEnemy()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, autoAimRadius, aimBuffer, targetMask,
                                                      QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            Vector3 bestDir = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                Collider col = aimBuffer[i];
                aimBuffer[i] = null;
                if (col == null) continue;
                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;

                Vector3 dir = target.Transform.position - transform.position;
                dir.y = 0f;
                float d = dir.sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestDir = dir;
                }
            }

            if (bestDir.sqrMagnitude > 0.0001f) motor.FaceDirection(bestDir);
            else if (motor.DesiredDirection.sqrMagnitude > 0.01f) motor.FaceDirection(motor.DesiredDirection);
        }

        private Vector3 HitCenter(AttackStep s) =>
            transform.position + Vector3.up * 1f + transform.forward * s.forwardOffset;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || combo == null) return;

            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, autoAimRadius);

            for (int i = 0; i < combo.Length; i++)
            {
                bool current = Application.isPlaying && i == step;
                Gizmos.color = current ? Color.red : new Color(1f, 0.6f, 0.1f, 0.8f);
                Gizmos.DrawWireSphere(HitCenter(combo[i]), combo[i].radius);
            }
        }
    }
}
