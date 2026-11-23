using System;
using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>Lo que hace un enemigo y se oye (tarea 32).</summary>
    public enum EnemyCue
    {
        Spawned,
        Windup,
        Swing,
        Died
    }

    /// <summary>
    /// Avisos globales de combate. Cada vez que un golpe se aplica, HitDetector lo anuncia aquí.
    /// Lo escucharán la pausa de impacto, la sacudida de cámara y los números de daño (tarea 12),
    /// y el audio y los efectos (tareas 32 y 33), sin que el código de ataque los conozca.
    /// </summary>
    public static class CombatEvents
    {
        /// <summary>Un golpe se aplicó a un objetivo (después de filtros y de la regla de bandos).</summary>
        public static event Action<IDamageable, DamageInfo> Hit;

        public static void RaiseHit(IDamageable target, DamageInfo info) => Hit?.Invoke(target, info);

        /// <summary>Alguien recuperó vida: a quién y cuánto (lo que devolvió Health.Heal). Lo escuchan los números verdes.</summary>
        public static event Action<Health, float> Healed;

        public static void RaiseHeal(Health target, float amount) => Healed?.Invoke(target, amount);

        /// <summary>
        /// Un enemigo apareció, empezó a preparar un golpe, lo lanzó o murió: qué hizo y dónde. Lo escucha el audio
        /// (GameSfx, tarea 32), sin que el enemigo sepa nada de sonido.
        /// </summary>
        public static event Action<EnemyCue, Vector3> EnemyActed;

        public static void RaiseEnemy(EnemyCue cue, Vector3 position) => EnemyActed?.Invoke(cue, position);

        // Si se desactiva "Domain Reload" en Play Mode, los eventos estáticos sobreviven entre
        // partidas y se acumulan suscriptores. Esto los limpia al iniciar cada Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Hit = null;
            Healed = null;
            EnemyActed = null;
        }
    }
}
