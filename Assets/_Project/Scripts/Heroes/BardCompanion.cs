using TheLastBeat.Combat;
using TheLastBeat.Core;
using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Heroes
{
    /// <summary>
    /// IA mínima de la bardo (tarea 16). Tres reglas, revisadas cada scanInterval:
    ///  1. Si hay enemigos vivos a menos de dangerRadius, se aleja de ellos, pero sin pasar
    ///     de leashRadius respecto al caballero (no huye al otro lado del mapa).
    ///  2. Si está lejos del caballero, se acerca hasta quedar a followDistance.
    ///  3. Si no, se queda quieta.
    /// Si queda atascada a más de teleportDistance, reaparece detrás del caballero.
    /// En la entrega final se reemplaza por la IA de compañeros (tareas 51 y 52).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class BardCompanion : MonoBehaviour
    {
        private enum Mode { Idle, Follow, Flee, Dead }

        [Header("Referencias")]
        [Tooltip("A quién sigue. Si se deja vacío, busca al objeto con PlayerController.")]
        [SerializeField] private Transform leader;
        [Tooltip("Opcional hasta la tarea 28. Parámetro float 'Speed'.")]
        [SerializeField] private Animator animator;
        [Tooltip("El dibujo de la bardo; al morir se acuesta.")]
        [SerializeField] private Transform visual;

        [Header("Seguir")]
        [SerializeField, Min(0.5f)] private float followDistance = 3f;
        [Tooltip("Distancia a la que empieza a moverse para alcanzar al caballero.")]
        [SerializeField, Min(0.5f)] private float catchUpDistance = 4.5f;
        [SerializeField, Min(5f)] private float teleportDistance = 25f;

        [Header("Peligro")]
        [SerializeField, Min(0.5f)] private float dangerRadius = 4f;
        [SerializeField, Min(0.5f)] private float fleeDistance = 4.5f;
        [Tooltip("Lo más lejos del caballero que puede huir.")]
        [SerializeField, Min(1f)] private float leashRadius = 7f;
        [Tooltip("Capas de los enemigos. Si queda en Nothing, usa la capa Enemy.")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField, Min(0.05f)] private float scanInterval = 0.2f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private readonly Collider[] buffer = new Collider[16];
        private NavMeshAgent agent;
        private Health health;
        private Mode mode = Mode.Idle;
        private float scanTimer;
        private float deathTimer;
        private Quaternion visualStart;

        public bool IsFleeing => mode == Mode.Flee;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            if (enemyMask.value == 0) enemyMask = GameLayers.EnemyMask;
            if (leader == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null) leader = player.transform;
            }
            if (visual != null) visualStart = visual.localRotation;
        }

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void Update()
        {
            float dt = Time.deltaTime;

            if (mode == Mode.Dead)
            {
                if (visual != null && deathTimer < 1f)
                {
                    deathTimer += dt * 2f;
                    visual.localRotation = Quaternion.Slerp(visualStart, visualStart * Quaternion.Euler(-90f, 0f, 0f), deathTimer);
                }
                return;
            }

            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                Think();
            }

            if (animator != null)
            {
                float speed = agent.speed > 0f ? agent.velocity.magnitude / agent.speed : 0f;
                animator.SetFloat(SpeedHash, speed, 0.1f, dt);
            }
        }

        private void Think()
        {
            if (leader == null) return;

            Vector3 pos = transform.position;
            Vector3 leaderPos = leader.position;
            float toLeader = Flat(pos - leaderPos).magnitude;

            if (toLeader > teleportDistance)
            {
                Vector3 behind = leaderPos - leader.forward * followDistance;
                if (NavMesh.SamplePosition(behind, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
                mode = Mode.Idle;
                return;
            }

            // 1. Huir de los enemigos cercanos.
            if (FindThreat(pos, out Vector3 threat))
            {
                Vector3 away = Flat(pos - threat);
                if (away.sqrMagnitude < 0.01f) away = Flat(pos - leaderPos);
                if (away.sqrMagnitude < 0.01f) away = -leader.forward;

                Vector3 candidate = pos + away.normalized * fleeDistance;
                Vector3 fromLeader = Flat(candidate - leaderPos);
                if (fromLeader.magnitude > leashRadius) candidate = leaderPos + fromLeader.normalized * leashRadius;

                MoveTo(candidate);
                mode = Mode.Flee;
                return;
            }

            // 2. Alcanzar al caballero.
            bool tooFar = toLeader > catchUpDistance || (mode == Mode.Follow && toLeader > followDistance + 0.3f);
            if (tooFar)
            {
                Vector3 dir = Flat(pos - leaderPos);
                if (dir.sqrMagnitude < 0.01f) dir = -leader.forward;
                MoveTo(leaderPos + dir.normalized * followDistance);
                mode = Mode.Follow;
                return;
            }

            // 3. Quedarse quieta.
            if (mode != Mode.Idle)
            {
                agent.ResetPath();
                mode = Mode.Idle;
            }
        }

        /// <summary>Promedio de la posición de los enemigos vivos dentro de dangerRadius.</summary>
        private bool FindThreat(Vector3 pos, out Vector3 threat)
        {
            threat = Vector3.zero;
            int count = Physics.OverlapSphereNonAlloc(pos, dangerRadius, buffer, enemyMask, QueryTriggerInteraction.Ignore);
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                Collider c = buffer[i];
                buffer[i] = null;
                if (c == null) continue;
                IDamageable d = c.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead) continue;
                threat += d.Transform.position;
                found++;
            }
            if (found == 0) return false;
            threat /= found;
            return true;
        }

        private void MoveTo(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private void OnDied(Health h, DamageInfo info)
        {
            mode = Mode.Dead;
            deathTimer = 0f;
            if (agent.enabled)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, dangerRadius);
            if (leader != null)
            {
                Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.6f);
                Gizmos.DrawWireSphere(leader.position, leashRadius);
            }
        }
    }
}
