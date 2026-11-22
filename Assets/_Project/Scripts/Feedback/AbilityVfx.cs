using TheLastBeat.Abilities;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Enciende y apaga efectos visuales con una habilidad (tarea 33). Escucha Ability.Started y
    /// Ability.Finished, así que las habilidades no conocen los efectos. Un componente por habilidad:
    ///  - trails: estelas que solo dejan rastro mientras la habilidad está activa (arco del giro, rayo de la estocada).
    ///  - whileActive: partículas que emiten mientras dura (polvo de la embestida).
    ///  - onStart / onFinish: ráfagas de una sola vez al empezar o al terminar.
    ///  - objects: objetos que solo están encendidos mientras dura (la media luna del giro).
    /// Las casillas vacías o destruidas se ignoran.
    /// </summary>
    public class AbilityVfx : MonoBehaviour
    {
        [SerializeField] private Ability ability;
        [SerializeField] private TrailRenderer[] trails = new TrailRenderer[0];
        [SerializeField] private ParticleSystem[] whileActive = new ParticleSystem[0];
        [SerializeField] private ParticleSystem[] onStart = new ParticleSystem[0];
        [SerializeField] private ParticleSystem[] onFinish = new ParticleSystem[0];
        [SerializeField] private GameObject[] objects = new GameObject[0];

        private void Awake()
        {
            SetTrails(false, true);
            SetObjects(false);
        }

        private void OnEnable()
        {
            if (ability == null) return;
            ability.Started += OnStarted;
            ability.Finished += OnFinished;
        }

        private void OnDisable()
        {
            if (ability != null)
            {
                ability.Started -= OnStarted;
                ability.Finished -= OnFinished;
            }
            SetTrails(false, false);
            SetObjects(false);
            Stop(whileActive);
        }

        private void OnStarted(Ability _)
        {
            SetTrails(true, true);
            SetObjects(true);
            Play(whileActive);
            Play(onStart);
        }

        private void OnFinished(Ability _)
        {
            SetTrails(false, false);
            SetObjects(false);
            Stop(whileActive);
            Play(onFinish);
        }

        private void SetTrails(bool emitting, bool clear)
        {
            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer trail = trails[i];
                if (trail == null) continue;
                if (clear) trail.Clear();
                trail.emitting = emitting;
            }
        }

        private void SetObjects(bool active)
        {
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null) objects[i].SetActive(active);
        }

        private static void Play(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
                if (systems[i] != null) systems[i].Play(true);
        }

        private static void Stop(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
                if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
