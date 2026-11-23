using System;
using UnityEngine;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// Un efecto de sonido tal como se usa en el juego (tarea 32): sus variantes (se elige una al azar, sin repetir
    /// la anterior), su volumen y cuánto cambia el tono en cada reproducción para que no suene siempre igual.
    /// Se reproduce con SfxPlayer.Play(cue).
    /// </summary>
    [Serializable]
    public class SfxCue
    {
        [Tooltip("Variantes del mismo sonido. Con una sola también funciona.")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Cuánto cambia el tono al azar en cada reproducción: 0.05 = hasta un 5 % más agudo o más grave.")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.05f;
        [Tooltip("Segundos mínimos entre dos reproducciones. Evita que suene diez veces en el mismo instante (un Giro contra una horda).")]
        [Min(0f)] public float minInterval = 0.05f;

        [NonSerialized] internal float lastTime = -100f;
        [NonSerialized] internal int lastIndex = -1;

        public bool IsEmpty => clips == null || clips.Length == 0;

        /// <summary>Carga los clips en memoria para que la primera reproducción no dé un tirón.</summary>
        public void Preload()
        {
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null) clips[i].LoadAudioData();
        }
    }
}
