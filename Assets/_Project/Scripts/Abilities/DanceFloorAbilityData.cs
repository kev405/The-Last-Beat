using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Datos de la Pista de baile de la bardo. En los valores base: radius es el radio de la pista; damage y
    /// knockback no se usan (no hace daño).
    /// Nivel 1: cura 4 por segundo, radio 3,5 m. Nivel 2: cura 7 por segundo, radio 4,5 m.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Pista de baile", fileName = "Ability_PistaDeBaile")]
    public class DanceFloorAbilityData : AbilityData
    {
        [Header("Pista de baile")]
        [Tooltip("Vida que recupera por segundo cada héroe que esté dentro.")]
        [Min(0f)] public float healPerSecond = 4f;
        [Tooltip("Segundos que la pista se queda en el suelo.")]
        [Min(0.5f)] public float floorDuration = 6f;
        [Tooltip("Cada cuánto cura. Con 1 s sale un número verde por segundo con lo curado en ese segundo.")]
        [Min(0.1f)] public float tickInterval = 1f;
        [Tooltip("Si la bardo también se cura mientras esté dentro de su pista.")]
        public bool healsCaster = true;

        [Header("Tiempos (los pone el menú según la animación)")]
        [Tooltip("Segundos desde que empieza hasta que la pista se abre. Solo se usa si BardActionMoments no avisa.")]
        [Min(0f)] public float hitDelay = 0.7f;
        [Tooltip("Segundos que la bardo queda clavada, lo que dura la animación.")]
        [Min(0.1f)] public float castTime = 1.33f;
    }
}
