using System;
using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Un golpe del combo. Todos los tiempos se miden en segundos desde que empieza ese golpe.
    ///
    ///   0 ──── lungeTime ──── hitTime ──────── duration
    ///   │  avanza un poco  │  aplica el daño  │  termina; si hubo clic en la ventana, sigue el siguiente
    ///                 comboWindowStart ───────┘
    /// </summary>
    [Serializable]
    public class AttackStep
    {
        public string name = "Golpe";

        [Header("Daño")]
        [Min(0f)] public float damage = 20f;
        [Min(0.1f)] public float radius = 1.3f;
        [Tooltip("Distancia del centro del golpe hacia el frente del personaje.")]
        [Min(0f)] public float forwardOffset = 1.0f;
        [Min(0f)] public float knockback = 0f;
        [Tooltip("Golpe fuerte: más pausa de impacto y sacudida (tarea 12).")]
        public bool isHeavy;

        [Header("Tiempos")]
        [Tooltip("Segundo en que se aplica el daño.")]
        [Min(0f)] public float hitTime = 0.14f;
        [Tooltip("Duración total del golpe.")]
        [Min(0.05f)] public float duration = 0.40f;
        [Tooltip("Desde este segundo se acepta el clic para el siguiente golpe.")]
        [Min(0f)] public float comboWindowStart = 0.12f;

        [Header("Avance")]
        [Tooltip("Metros que avanza al golpear.")]
        [Min(0f)] public float lungeDistance = 0.4f;
        [Tooltip("En cuántos segundos hace ese avance.")]
        [Min(0f)] public float lungeTime = 0.10f;
    }
}
