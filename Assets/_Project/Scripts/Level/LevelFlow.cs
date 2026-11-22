using System;
using System.Collections;
using TheLastBeat.Combat;
using TheLastBeat.Heroes;
using TheLastBeat.Player;
using TheLastBeat.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.Level
{
    public enum GameState
    {
        /// <summary>El jugador recorre el pueblo en silencio, todavía sin enemigos.</summary>
        Exploring,
        /// <summary>Las hordas están activas.</summary>
        Combat,
        Victory,
        Defeat
    }

    /// <summary>
    /// Flujo del nivel (tarea 20). Une las piezas que ya existen:
    ///   inicio en silencio → el caballero entra a la plaza (ZoneTrigger) → hordas (WaveSpawner)
    ///   → victoria al caer la última horda, o derrota si cae el caballero o la bardo.
    /// No sabe nada de la interfaz: la avisa con StateChanged y Message.
    /// En la entrega final crece a los 7 pasos del nivel (tarea 60).
    /// </summary>
    public class LevelFlow : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private WaveSpawner spawner;
        [SerializeField] private Health knight;
        [SerializeField] private Health bard;
        [SerializeField] private PlayerInputReader playerInput;

        [Header("Tiempos")]
        [Tooltip("Pausa entre la caída de la última horda y la pantalla de victoria.")]
        [SerializeField, Min(0f)] private float victoryDelay = 1.5f;
        [Tooltip("Pausa entre la caída de un héroe y la pantalla de derrota.")]
        [SerializeField, Min(0f)] private float defeatDelay = 1.5f;

        [Header("Mensajes")]
        [SerializeField] private string introMessage = "El pueblo está en silencio...";
        [SerializeField] private string combatMessage = "¡Defiende la canción!";
        [SerializeField] private string waveClearedMessage = "¡Horda superada!";
        [SerializeField] private string knightDownMessage = "El caballero cayó";
        [SerializeField] private string bardDownMessage = "La bardo cayó: la música se apagó";

        [Header("Escenas")]
        [SerializeField] private string menuSceneName = "MainMenu";

        private bool ending;

        public GameState State { get; private set; } = GameState.Exploring;

        /// <summary>Cambió el estado del nivel.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Un mensaje corto para el centro de la pantalla.</summary>
        public event Action<string> Message;

        private void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputReader>();
            if (knight == null)
            {
                PlayerController p = FindFirstObjectByType<PlayerController>();
                if (p != null) knight = p.GetComponent<Health>();
            }
            if (bard == null)
            {
                BardCompanion b = FindFirstObjectByType<BardCompanion>();
                if (b != null) bard = b.GetComponent<Health>();
            }
        }

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.WaveStarted += OnWaveStarted;
                spawner.WaveCleared += OnWaveCleared;
                spawner.AllWavesCleared += OnAllWavesCleared;
            }
            if (knight != null) knight.Died += OnKnightDied;
            if (bard != null) bard.Died += OnBardDied;
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.WaveStarted -= OnWaveStarted;
                spawner.WaveCleared -= OnWaveCleared;
                spawner.AllWavesCleared -= OnAllWavesCleared;
            }
            if (knight != null) knight.Died -= OnKnightDied;
            if (bard != null) bard.Died -= OnBardDied;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            State = GameState.Exploring;
            StateChanged?.Invoke(State);
            StartCoroutine(SendLater(introMessage, 0.5f));
        }

        /// <summary>Empieza el combate. Lo llama el ZoneTrigger de la entrada de la plaza.</summary>
        public void StartCombat()
        {
            if (State != GameState.Exploring) return;
            SetState(GameState.Combat);
            Message?.Invoke(combatMessage);
            if (spawner != null) spawner.StartWaves();
        }

        /// <summary>Vuelve a cargar el nivel desde cero.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Vuelve al menú principal; si no está en la build, reinicia el nivel.</summary>
        public void GoToMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(menuSceneName)) SceneManager.LoadScene(menuSceneName);
            else Restart();
        }

        private void OnWaveStarted(int index, Wave wave) => Message?.Invoke(wave.name);

        private void OnWaveCleared(int index)
        {
            if (spawner != null && index < spawner.WaveCount - 1) Message?.Invoke(waveClearedMessage);
        }

        private void OnAllWavesCleared()
        {
            if (ending) return;
            ending = true;
            StartCoroutine(EndAfter(GameState.Victory, victoryDelay));
        }

        private void OnKnightDied(Health h, DamageInfo info) => HeroDown(knightDownMessage);
        private void OnBardDied(Health h, DamageInfo info) => HeroDown(bardDownMessage);

        private void HeroDown(string message)
        {
            if (ending) return;
            ending = true;
            if (playerInput != null) playerInput.SetGameplayEnabled(false);
            Message?.Invoke(message);
            StartCoroutine(EndAfter(GameState.Defeat, defeatDelay));
        }

        private IEnumerator EndAfter(GameState result, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (playerInput != null) playerInput.SetGameplayEnabled(false);
            SetState(result);
            if (result == GameState.Defeat) Time.timeScale = 0f; // congela a los enemigos detrás de la pantalla
        }

        private IEnumerator SendLater(string message, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (State == GameState.Exploring && !string.IsNullOrEmpty(message)) Message?.Invoke(message);
        }

        private void SetState(GameState s)
        {
            if (State == s) return;
            State = s;
            Debug.Log($"[Nivel] {s}");
            StateChanged?.Invoke(s);
        }
    }
}
