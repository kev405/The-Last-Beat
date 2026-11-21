using System;
using UnityEngine;

namespace TheLastBeat.Waves
{
    /// <summary>
    /// Una horda: cuántos enemigos salen, cada cuánto y cuántos pueden estar vivos a la vez.
    /// </summary>
    [Serializable]
    public class Wave
    {
        public string name = "Horda";
        [Tooltip("Enemigos en total en esta horda.")]
        [Min(1)] public int count = 5;
        [Tooltip("Segundos entre una aparición y la siguiente.")]
        [Min(0f)] public float spawnInterval = 0.6f;
        [Tooltip("Máximo de enemigos vivos a la vez. Si se llega, espera a que caiga alguno.")]
        [Min(1)] public int maxAlive = 8;
        [Tooltip("Pausa antes de que empiece esta horda.")]
        [Min(0f)] public float delayBefore = 2f;
    }

    /// <summary>
    /// Lista de hordas de una zona del nivel (tarea 15). Se edita como asset, sin tocar código.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Hordas", fileName = "Hordas_")]
    public class WaveData : ScriptableObject
    {
        public Wave[] waves =
        {
            new Wave { name = "Primera horda", count = 5, spawnInterval = 0.8f, maxAlive = 5, delayBefore = 2f },
            new Wave { name = "Segunda horda", count = 8, spawnInterval = 0.6f, maxAlive = 6, delayBefore = 3f },
            new Wave { name = "Tercera horda", count = 12, spawnInterval = 0.5f, maxAlive = 8, delayBefore = 3f }
        };
    }
}
