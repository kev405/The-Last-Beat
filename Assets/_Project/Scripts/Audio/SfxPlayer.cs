using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// Reproduce los efectos de sonido (tarea 32). Tiene un pool de AudioSource: cada efecto toma una fuente libre
    /// con su propio tono y volumen, así que pueden sonar muchos a la vez sin cortarse entre sí. Si todas están
    /// ocupadas, se reutiliza la que lleva más tiempo sonando.
    /// Solo hay uno y sobrevive a los cambios de escena, para que el clic de un botón que carga otra escena alcance
    /// a oírse. Va en un objeto raíz propio. Se usa desde cualquier script con SfxPlayer.Play(cue).
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        [Tooltip("Cuántos efectos pueden sonar a la vez.")]
        [SerializeField, Range(4, 48)] private int voices = 24;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [Tooltip("Opcional: grupo Efectos del Audio Mixer.")]
        [SerializeField] private AudioMixerGroup output;

        private static SfxPlayer instance;

        private AudioSource[] pool;
        private float[] startedAt;

        // Si se desactiva "Domain Reload" en Play Mode, la referencia estática sobrevive entre partidas.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);   // ya hay uno: el que viene de la escena anterior
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            pool = new AudioSource[voices];
            startedAt = new float[voices];
            for (int i = 0; i < voices; i++)
            {
                GameObject holder = new GameObject("Voice_" + i);
                holder.transform.SetParent(transform, false);

                AudioSource source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = output;
                pool[i] = source;
                startedAt[i] = -100f;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Reproduce un efecto. No hace nada si el cue está vacío, si sonó hace menos de su minInterval o si la
        /// escena no tiene SfxPlayer.
        /// </summary>
        public static void Play(SfxCue cue)
        {
            if (instance != null) instance.PlayCue(cue);
        }

        private void PlayCue(SfxCue cue)
        {
            if (cue == null || cue.IsEmpty || pool == null) return;

            // Tiempo real: los efectos suenan igual durante la pausa de impacto y en las pantallas de fin (timeScale 0).
            float now = Time.unscaledTime;
            if (now - cue.lastTime < cue.minInterval) return;

            AudioClip clip = Pick(cue);
            if (clip == null) return;
            cue.lastTime = now;

            int voice = FreeVoice();
            AudioSource source = pool[voice];
            source.Stop();
            source.clip = clip;
            source.volume = cue.volume * volume;
            source.pitch = 1f + Random.Range(-cue.pitchVariation, cue.pitchVariation);
            source.Play();
            startedAt[voice] = now;
        }

        /// <summary>Una variante al azar, distinta de la última que sonó.</summary>
        private static AudioClip Pick(SfxCue cue)
        {
            int count = cue.clips.Length;
            int index = count == 1 ? 0 : Random.Range(0, count);
            if (count > 1 && index == cue.lastIndex) index = (index + 1) % count;
            cue.lastIndex = index;
            return cue.clips[index];
        }

        /// <summary>Una fuente que no esté sonando o, si todas suenan, la que empezó hace más tiempo.</summary>
        private int FreeVoice()
        {
            int oldest = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].isPlaying) return i;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
