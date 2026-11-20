using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Datos de la Embestida del caballero (tarea 11). En los valores base, damage es el
    /// daño a cada enemigo que atraviesa; radius no se usa (el ancho va en width).
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Embestida", fileName = "Ability_Embestida")]
    public class ChargeAbilityData : AbilityData
    {
        [Header("Embestida")]
        [Tooltip("Metros que recorre.")]
        [Min(0.5f)] public float distance = 6f;
        [Tooltip("Segundos que tarda en recorrerlos.")]
        [Min(0.05f)] public float duration = 0.25f;
        [Tooltip("Ancho de la franja que golpea.")]
        [Min(0.2f)] public float width = 1.6f;
        [Tooltip("Pausa al terminar antes de poder moverse.")]
        [Min(0f)] public float recovery = 0.15f;
    }
}
