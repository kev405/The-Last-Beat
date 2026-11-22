using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Datos del Golpe de bajo de la bardo. En los valores base: radius es el radio de la onda, damage el daño
    /// a cada enemigo dentro y knockback la fuerza con la que los empuja hacia fuera.
    /// Nivel 1: empuje y daño bajo. Nivel 2: pon Stun Seconds en 1.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Golpe de bajo", fileName = "Ability_GolpeDeBajo")]
    public class BassDropAbilityData : AbilityData
    {
        [Header("Golpe de bajo")]
        [Tooltip("Segundos que deja aturdidos a los enemigos (EnemySoldier.Stun). 0 = no aturde.")]
        [Min(0f)] public float stunSeconds = 0f;
        [Tooltip("El golpe cuenta como pesado: el enemigo se tambalea y pierde el ataque que estaba preparando.")]
        public bool staggers = true;

        [Header("Tiempos (los pone el menú según la animación)")]
        [Tooltip("Segundos desde que empieza hasta el pisotón. Solo se usa si BardActionMoments no avisa.")]
        [Min(0f)] public float hitDelay = 0.47f;
        [Tooltip("Segundos que la bardo queda clavada, lo que dura la animación.")]
        [Min(0.1f)] public float castTime = 1.2f;
    }
}
