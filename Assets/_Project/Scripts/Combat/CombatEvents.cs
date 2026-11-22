using System;
using UnityEngine;

namespace TheLastBeat.Combat
{
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

        // Si se desactiva "Domain Reload" en Play Mode, los eventos estáticos sobreviven entre
        // partidas y se acumulan suscriptores. Esto los limpia al iniciar cada Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Hit = null;
            Healed = null;
        }
    }
}
