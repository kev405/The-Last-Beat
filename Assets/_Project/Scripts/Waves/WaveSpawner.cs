using System;
using System.Collections;
using System.Collections.Generic;
using TheLastBeat.Combat;
using TheLastBeat.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Waves
{
    /// <summary>
    /// Lanza las hordas de un WaveData (tarea 15). Cada horda empieza cuando cayó la anterior.
    /// Elige puntos de aparición lejos de los héroes para que no salgan encima del jugador.
    /// Avisa con eventos para que el flujo del nivel (tarea 20), la música y la interfaz reaccionen.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private WaveData data;
        [SerializeField] private EnemyPool pool;
        [Tooltip("Objeto cuyos hijos son los puntos de aparición (Spawns).")]
        [SerializeField] private Transform spawnPointsRoot;

        [Header("Inicio")]
        [Tooltip("Empieza solo al dar Play. En la tarea 20 lo apagaremos y lo lanzará LevelFlow.")]
        [SerializeField] private bool autoStart = true;
        [SerializeField, Min(0f)] private float startDelay = 1f;

        [Header("Aparición")]
        [Tooltip("No usa puntos a menos de esta distancia de un héroe, si hay alternativa.")]
        [SerializeField, Min(0f)] private float minDistanceFromHeroes = 8f;
        [SerializeField] private bool logToConsole = true;

        private readonly List<Transform> points = new List<Transform>();
        private readonly List<Transform> candidates = new List<Transform>();

        /// <summary>Empezó la horda (índice, datos).</summary>
        public event Action<int, Wave> WaveStarted;

        /// <summary>Cayó el último enemigo de la horda (índice).</summary>
        public event Action<int> WaveCleared;

        /// <summary>Terminaron todas las hordas.</summary>
        public event Action AllWavesCleared;

        public bool IsRunning { get; private set; }
        public int CurrentWave { get; private set; } = -1;
        public int AliveCount { get; private set; }
        public int WaveCount => data != null && data.waves != null ? data.waves.Length : 0;

        private void Awake()
        {
            points.Clear();
            if (spawnPointsRoot != null)
                foreach (Transform child in spawnPointsRoot) points.Add(child);
        }

        private void Start()
        {
            if (autoStart) StartWaves();
        }

        public void StartWaves()
        {
            if (IsRunning) return;
            if (data == null || pool == null || points.Count == 0)
            {
                Debug.LogError($"[{nameof(WaveSpawner)}] Falta WaveData, EnemyPool o puntos de aparición.", this);
                return;
            }
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            IsRunning = true;
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            for (int i = 0; i < data.waves.Length; i++)
            {
                Wave w = data.waves[i];
                CurrentWave = i;
                if (w.delayBefore > 0f) yield return new WaitForSeconds(w.delayBefore);

                if (logToConsole) Debug.Log($"[Hordas] Empieza {w.name}: {w.count} enemigos.");
                WaveStarted?.Invoke(i, w);

                int spawned = 0;
                while (spawned < w.count)
                {
                    while (AliveCount >= w.maxAlive) yield return null;
                    if (SpawnOne()) spawned++;
                    else yield return null;
                    if (w.spawnInterval > 0f) yield return new WaitForSeconds(w.spawnInterval);
                }

                while (AliveCount > 0) yield return null;

                if (logToConsole) Debug.Log($"[Hordas] {w.name} superada.");
                WaveCleared?.Invoke(i);
            }

            IsRunning = false;
            if (logToConsole) Debug.Log("[Hordas] Todas las hordas superadas.");
            AllWavesCleared?.Invoke();
        }

        private bool SpawnOne()
        {
            Transform point = PickPoint();
            Vector3 position = point.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) position = hit.position;

            HeroTarget hero = HeroTarget.FindBest(position);
            Quaternion rotation = point.rotation;
            if (hero != null)
            {
                Vector3 dir = hero.transform.position - position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) rotation = Quaternion.LookRotation(dir);
            }

            EnemySoldier e = pool.Spawn(position, rotation);
            if (e == null) return false;

            AliveCount++;
            e.Died += OnEnemyDied;
            return true;
        }

        private void OnEnemyDied(EnemySoldier e)
        {
            e.Died -= OnEnemyDied;
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }

        private Transform PickPoint()
        {
            candidates.Clear();
            foreach (Transform p in points)
                if (HeroTarget.NearestDistance(p.position) >= minDistanceFromHeroes) candidates.Add(p);

            List<Transform> from = candidates.Count > 0 ? candidates : points;
            return from[UnityEngine.Random.Range(0, from.Count)];
        }

        private void OnDrawGizmos()
        {
            if (spawnPointsRoot == null) return;
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            foreach (Transform p in spawnPointsRoot)
            {
                Gizmos.DrawWireSphere(p.position + Vector3.up * 0.1f, 0.6f);
                Gizmos.DrawLine(p.position, p.position + p.forward * 1.2f);
            }
        }
    }
}
