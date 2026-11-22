using TheLastBeat.Waves;
using TMPro;
using UnityEngine;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Horda actual y enemigos vivos, arriba al centro (tarea 34). Se oculta mientras el
    /// jugador explora y muestra "PUEBLO LIBERADO" al final.
    /// </summary>
    public class WaveStatusUI : MonoBehaviour
    {
        [SerializeField] private WaveSpawner spawner;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text enemiesText;

        private int lastAlive = -1;
        private bool finished;

        private void OnEnable()
        {
            if (group != null) group.alpha = 0f;
            if (spawner == null) return;
            spawner.WaveStarted += OnWaveStarted;
            spawner.AllWavesCleared += OnAllCleared;
        }

        private void OnDisable()
        {
            if (spawner == null) return;
            spawner.WaveStarted -= OnWaveStarted;
            spawner.AllWavesCleared -= OnAllCleared;
        }

        private void OnWaveStarted(int index, Wave wave)
        {
            if (group != null) group.alpha = 1f;
            if (waveText != null) waveText.SetText("HORDA {0} / {1}", index + 1, spawner.WaveCount);
        }

        private void OnAllCleared()
        {
            finished = true;
            if (waveText != null) waveText.SetText("PUEBLO LIBERADO");
            if (enemiesText != null) enemiesText.SetText(string.Empty);
        }

        private void Update()
        {
            if (spawner == null || finished || enemiesText == null || !spawner.IsRunning) return;
            int alive = spawner.AliveCount;
            if (alive == lastAlive) return;
            lastAlive = alive;
            enemiesText.SetText("Enemigos: {0}", alive);
        }
    }
}
