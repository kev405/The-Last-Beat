using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Pausa de impacto (tarea 12): congela casi por completo el juego unos milisegundos
    /// cada vez que un golpe conecta. Es el truco más barato para que los golpes "pesen".
    /// Las pausas no se acumulan: si llegan varios golpes juntos (el Giro contra un grupo),
    /// se usa la más larga, no la suma.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float lightDuration = 0.045f;
        [SerializeField, Min(0f)] private float heavyDuration = 0.09f;
        [Tooltip("Escala de tiempo durante la pausa. 0 = congelado, 0.05 = casi congelado.")]
        [SerializeField, Range(0f, 1f)] private float slowScale = 0.05f;
        [Tooltip("Solo pausa con los golpes de los héroes, no cuando un enemigo golpea.")]
        [SerializeField] private bool onlyHeroAttacks = true;

        private float remaining;
        private bool active;

        private void OnEnable() => CombatEvents.Hit += OnHit;

        private void OnDisable()
        {
            CombatEvents.Hit -= OnHit;
            Restore();
        }

        private void OnHit(IDamageable target, DamageInfo info)
        {
            if (onlyHeroAttacks && info.SourceTeam != Team.Heroes) return;
            if (!active && Time.timeScale == 0f) return; // el juego está en pausa

            float duration = info.IsHeavy ? heavyDuration : lightDuration;
            remaining = Mathf.Max(remaining, duration);

            if (!active)
            {
                active = true;
                Time.timeScale = slowScale;
            }
        }

        private void Update()
        {
            if (!active) return;
            remaining -= Time.unscaledDeltaTime; // tiempo real: la pausa no se frena a sí misma
            if (remaining <= 0f) Restore();
        }

        private void Restore()
        {
            if (!active) return;
            active = false;
            remaining = 0f;
            Time.timeScale = 1f;
        }
    }
}
