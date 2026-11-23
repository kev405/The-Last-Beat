using System;
using TheLastBeat.Combat;
using TheLastBeat.Level;
using UnityEngine;

namespace TheLastBeat.Heroes
{
    public enum SongState
    {
        /// <summary>No canta: todavía no empieza o la bardo cayó.</summary>
        Silent,
        /// <summary>Canta: los héroes dentro del radio están potenciados.</summary>
        Singing,
        /// <summary>Un golpe cortó la canción; vuelve a cantar al terminar la recuperación.</summary>
        Interrupted
    }

    /// <summary>
    /// La canción de la bardo (tareas 17 y 18). Mientras canta, potencia a los héroes dentro
    /// de su radio (BuffReceiver). Si la golpean con un golpe que interrumpe, la canción se
    /// corta, el grupo pierde la potenciación y tarda recoveryTime en volver; otro golpe
    /// durante la recuperación reinicia el contador.
    /// Con waitForCombat no empieza a cantar hasta que LevelFlow pasa a Combat: el pueblo llega en silencio.
    /// El audio (tarea 32), los efectos (33) y la interfaz (34) escuchan SongStateChanged.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class SongBuff : MonoBehaviour
    {
        [Header("Canción")]
        [Tooltip("Radio en metros en el que potencia a los héroes.")]
        [SerializeField, Min(0.5f)] private float radius = 8f;
        [Tooltip("Segundos que tarda en volver a cantar después de un golpe.")]
        [SerializeField, Min(0f)] private float recoveryTime = 3f;
        [Tooltip("Segundos desde que puede cantar (el inicio del combate o, sin waitForCombat, del nivel) hasta que empieza.")]
        [SerializeField, Min(0f)] private float startDelay = 1f;
        [Tooltip("Espera a que empiece el combate (LevelFlow en Combat) para cantar. Apagado, canta desde que carga el nivel.")]
        [SerializeField] private bool waitForCombat = true;
        [Tooltip("Cada cuánto revisa quién está dentro del radio.")]
        [SerializeField, Min(0.05f)] private float scanInterval = 0.2f;

        [Header("Visual")]
        [Tooltip("Disco del suelo que muestra el área. Se enciende solo mientras canta.")]
        [SerializeField] private GameObject aura;

        [Header("Consola")]
        [SerializeField] private bool logToConsole = true;

        private Health health;
        private LevelFlow level;
        private SongState state = SongState.Silent;
        private float recoveryRemaining;
        private float startTimer;
        private float scanTimer;

        public SongState State => state;
        public float Radius => radius;
        public float RecoveryTime => recoveryTime;

        /// <summary>Segundos que faltan para volver a cantar (0 si está cantando).</summary>
        public float RecoveryRemaining => state == SongState.Interrupted ? recoveryRemaining : 0f;

        public event Action<SongState> SongStateChanged;

        private void Awake()
        {
            health = GetComponent<Health>();
            level = FindFirstObjectByType<LevelFlow>();
            if (aura != null) aura.SetActive(false);
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            startTimer = startDelay;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            RemoveAllBuffs();
        }

        private void Update()
        {
            if (health.IsDead) return;
            float dt = Time.deltaTime;

            switch (state)
            {
                case SongState.Silent:
                    // En la llegada al pueblo no canta: empieza con el combate.
                    if (waitForCombat && level != null && level.State == GameState.Exploring) break;
                    startTimer -= dt;
                    if (startTimer <= 0f) SetState(SongState.Singing);
                    break;

                case SongState.Interrupted:
                    recoveryRemaining -= dt;
                    if (recoveryRemaining <= 0f) SetState(SongState.Singing);
                    break;

                case SongState.Singing:
                    scanTimer -= dt;
                    if (scanTimer <= 0f)
                    {
                        scanTimer = scanInterval;
                        RefreshBuffs();
                    }
                    break;
            }
        }

        /// <summary>Corta la canción. Si ya estaba cortada, reinicia el tiempo de recuperación.</summary>
        public void Interrupt()
        {
            if (health.IsDead) return;
            recoveryRemaining = recoveryTime;
            SetState(SongState.Interrupted);
        }

        private void OnDamaged(Health h, DamageInfo info)
        {
            if (info.Interrupts) Interrupt();
        }

        private void OnDied(Health h, DamageInfo info) => SetState(SongState.Silent);

        private void RefreshBuffs()
        {
            float r2 = radius * radius;
            Vector3 center = transform.position;
            var receivers = BuffReceiver.All;
            for (int i = 0; i < receivers.Count; i++)
            {
                BuffReceiver r = receivers[i];
                if (r == null) continue;
                Vector3 d = r.transform.position - center;
                d.y = 0f;
                bool inside = r.IsAlive && d.sqrMagnitude <= r2;
                r.SetBuff(this, inside);
            }
        }

        private void RemoveAllBuffs()
        {
            var receivers = BuffReceiver.All;
            for (int i = 0; i < receivers.Count; i++)
                if (receivers[i] != null) receivers[i].SetBuff(this, false);
        }

        private void SetState(SongState s)
        {
            if (state == s) return;
            state = s;

            if (s == SongState.Singing)
            {
                scanTimer = 0f; // revisa el área en el siguiente frame
            }
            else
            {
                RemoveAllBuffs();
            }

            if (aura != null) aura.SetActive(s == SongState.Singing);
            if (logToConsole)
            {
                string msg = s switch
                {
                    SongState.Singing => "la canción suena",
                    SongState.Interrupted => $"¡canción interrumpida! vuelve en {recoveryTime:0.#} s",
                    _ => "silencio"
                };
                Debug.Log($"[Bardo] {msg}");
            }
            SongStateChanged?.Invoke(s);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.5f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
