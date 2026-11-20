using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Datos del Giro del caballero (tarea 10). En los valores base, damage y radius son
    /// los de cada golpe del giro; la explosión final tiene los suyos.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Giro", fileName = "Ability_Giro")]
    public class SpinAbilityData : AbilityData
    {
        [Header("Giro")]
        [Tooltip("Cuánto dura el giro.")]
        [Min(0.1f)] public float duration = 1.0f;
        [Tooltip("Cada cuánto golpea mientras gira.")]
        [Min(0.05f)] public float tickInterval = 0.25f;
        [Tooltip("Velocidad de movimiento mientras gira (0.4 = 40 %).")]
        [Range(0f, 1f)] public float moveSpeedMultiplier = 0.4f;
        [Tooltip("Grados por segundo que gira el dibujo del personaje.")]
        [Min(0f)] public float visualSpinSpeed = 1080f;

        [Header("Explosión final")]
        [Min(0f)] public float explosionRadius = 3.5f;
        [Min(0f)] public float explosionDamage = 45f;
        [Min(0f)] public float explosionKnockback = 8f;
    }
}
