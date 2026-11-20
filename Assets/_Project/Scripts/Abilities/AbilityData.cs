using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Datos comunes de una habilidad, guardados como asset (ScriptableObject) para ajustar
    /// valores sin tocar código. Cada habilidad tiene su propia subclase con sus datos extra
    /// (SpinAbilityData, ChargeAbilityData). El comportamiento vive en el componente Ability.
    /// </summary>
    public abstract class AbilityData : ScriptableObject
    {
        [Header("Presentación")]
        public string displayName = "Habilidad";
        [TextArea] public string description;
        [Tooltip("Ícono para el HUD (tarea 34).")]
        public Sprite icon;

        [Header("Valores base")]
        [Min(0f)] public float cooldown = 6f;
        [Min(0f)] public float damage = 30f;
        [Min(0f)] public float radius = 2.5f;
        [Min(0f)] public float knockback = 0f;
        [Tooltip("Si el golpe corta canciones y hechizos del objetivo.")]
        public bool interrupts = true;

        [Header("Progresión")]
        [Tooltip("Nivel de mejora (1 a 8). Se usará cuando exista el sistema de mejoras.")]
        [Range(1, 8)] public int level = 1;
    }
}
