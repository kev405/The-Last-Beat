using System;
using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.Heroes
{
    /// <summary>
    /// El sonido de la canción de la bardo, por capas (tarea 32). Escucha SongBuff.SongStateChanged, igual que
    /// SongAnimation. La canción son varios clips que duran lo mismo (base, percusión y melodía) y suenan
    /// sincronizados, cada uno en su fuente:
    ///  - Singing por primera vez: las capas entran una por una, cada startGapBars compases.
    ///  - Interrupted: todo se frena como una cinta (el tono baja hasta cero) y queda el silencio. Cuando a la
    ///    recuperación le falta lo que dura la entrada, suena la entrada y, pegadas a ella, vuelven las primeras
    ///    layersOnReturn capas en la frase siguiente a la que se cortó; las demás entran returnGapBars compases
    ///    después. Si la golpean seguido, la melodía no alcanza a volver. Otro golpe durante la recuperación
    ///    corta también la entrada.
    ///  - Silent (la bardo cayó): se apaga más despacio.
    /// También se apaga si el nivel termina en derrota (LevelFlow.StateChanged); en la victoria sigue sonando.
    /// Avisa de cada pulso con Beat, para que los efectos se muevan al ritmo (ver BeatPulse).
    /// Va en el mismo objeto que SongBuff. Las fuentes de audio las crea sola al empezar.
    /// Usa tiempo real (no Time.deltaTime): la pausa de impacto y la pantalla de derrota no la afectan.
    /// </summary>
    public class BardSongAudio : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Si se deja vacío, usa el SongBuff de este objeto.")]
        [SerializeField] private SongBuff song;
        [Tooltip("Para apagar la canción en la derrota. Si se deja vacío, lo busca en la escena.")]
        [SerializeField] private LevelFlow level;

        [Header("Clips")]
        [Tooltip("Las capas del dembow en el orden en que entran: base, percusión y melodía. Deben durar lo mismo y un número entero de compases.")]
        [SerializeField] private AudioClip[] layerClips = new AudioClip[0];
        [Tooltip("Entrada que suena justo antes de que la canción vuelva. Opcional.")]
        [SerializeField] private AudioClip introClip;

        [Header("Ritmo")]
        [SerializeField, Min(30f)] private float bpm = 96f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [Tooltip("Después de un corte, la canción vuelve en el siguiente bloque de este tamaño (en compases). 4 = una frase.")]
        [SerializeField, Min(1)] private int resumeEveryBars = 4;

        [Header("Capas")]
        [Tooltip("Al empezar la canción, cada capa entra estos compases después de la anterior. 4 = una frase (10 s). 0 = todas juntas.")]
        [SerializeField, Min(0)] private int startGapBars = 4;
        [Tooltip("Al volver de un corte, cuántas capas entran de una vez con la entrada.")]
        [SerializeField, Min(1)] private int layersOnReturn = 2;
        [Tooltip("Al volver de un corte, cada capa que falta entra estos compases después. 4 = una frase.")]
        [SerializeField, Min(0)] private int returnGapBars = 4;

        [Header("Mezcla")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [Tooltip("0 = se oye igual en todo el nivel (música). 1 = sale de la posición de la bardo.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
        [Tooltip("Opcional: grupo Música del Audio Mixer.")]
        [SerializeField] private AudioMixerGroup output;

        [Header("Corte")]
        [Tooltip("Segundos que tarda en frenarse cuando un golpe corta la canción.")]
        [SerializeField, Min(0.05f)] private float cutTime = 0.35f;
        [Tooltip("Segundos que tarda en apagarse cuando la bardo cae o se pierde el nivel.")]
        [SerializeField, Min(0.05f)] private float fadeTime = 1.2f;

        /// <summary>Margen para programar un clip con precisión de muestra (AudioSource.PlayScheduled).</summary>
        private const double ScheduleMargin = 0.05;

        private AudioSource[] layerSources = new AudioSource[0];
        private AudioSource introSource;
        private AudioSource baseSource;   // la primera capa: lleva el compás

        private double samplesPerBeat;
        private int samplesPerBar = 1;
        private int totalBars = 1;
        private double barSeconds;
        private double introLength;

        private int nextBar;            // compás en el que arranca la próxima vez
        private double loopStartDsp;    // momento (AudioSettings.dspTime) en el que la primera capa empieza a oírse
        private bool everStarted;       // la canción ya sonó alguna vez: lo siguiente es un regreso, no un inicio
        private bool resumeScheduled;   // la entrada y las capas ya quedaron programadas para el regreso
        private float lastRemaining;
        private bool stopping;
        private float stopTimer;
        private float stopDuration;
        private bool silenced;          // el nivel se perdió: no vuelve a sonar
        private int lastBeat = -1;

        /// <summary>Un pulso del dembow. El número es el tiempo dentro del compás: 0 es el primero.</summary>
        public event Action<int> Beat;

        public float Bpm => bpm;

        /// <summary>True mientras la canción se oye (no durante la entrada ni mientras se frena).</summary>
        public bool IsPlaying =>
            baseSource != null && !stopping && baseSource.isPlaying && AudioSettings.dspTime >= loopStartDsp;

        /// <summary>Avance dentro del pulso actual, de 0 a 1. Sirve para animar algo de forma continua con el ritmo.</summary>
        public float BeatProgress =>
            IsPlaying && samplesPerBeat > 0.0 ? (float)(baseSource.timeSamples % samplesPerBeat / samplesPerBeat) : 0f;

        private void Awake()
        {
            if (song == null) song = GetComponent<SongBuff>();
            if (level == null) level = FindFirstObjectByType<LevelFlow>();

            layerSources = new AudioSource[layerClips != null ? layerClips.Length : 0];
            for (int i = 0; i < layerSources.Length; i++)
            {
                layerSources[i] = CreateSource("SongAudio_Layer" + i, layerClips[i], true);
                if (baseSource == null && layerClips[i] != null) baseSource = layerSources[i];
            }
            introSource = CreateSource("SongAudio_Intro", introClip, false);

            if (baseSource == null)
            {
                Debug.LogWarning("[BardSongAudio] Faltan las capas de la canción: ejecuta \"The Last Beat/Audio/Montar audio de la escena\".", this);
                return;
            }

            AudioClip first = baseSource.clip;
            samplesPerBeat = first.frequency * 60.0 / bpm;
            samplesPerBar = Mathf.Max(1, (int)Math.Round(samplesPerBeat * beatsPerBar));
            totalBars = Mathf.Max(1, (int)Math.Round(first.samples / (double)samplesPerBar));
            barSeconds = samplesPerBar / (double)first.frequency;
            if (Math.Abs(first.samples - (long)totalBars * samplesPerBar) > first.frequency / 100)
                Debug.LogWarning($"[BardSongAudio] {first.name} no dura un número entero de compases a {bpm} BPM: el ritmo se va a descuadrar.", this);

            // Los deja cargados desde ya, aunque el clip no tenga marcado Preload Audio Data: así el primer
            // PlayScheduled no espera a que se cargue y nada da un tirón en mitad del combate.
            for (int i = 0; i < layerClips.Length; i++)
            {
                AudioClip clip = layerClips[i];
                if (clip == null) continue;
                clip.LoadAudioData();
                if (clip.samples != first.samples || clip.frequency != first.frequency)
                    Debug.LogWarning($"[BardSongAudio] {clip.name} no dura lo mismo que {first.name}: las capas se van a desfasar.", this);
            }

            if (introClip != null)
            {
                introLength = introClip.samples / (double)introClip.frequency;
                introClip.LoadAudioData();
            }
        }

        private void OnEnable()
        {
            if (song != null) song.SongStateChanged += OnSongStateChanged;
            if (level != null) level.StateChanged += OnLevelStateChanged;
            if (song != null && song.State == SongState.Singing && !silenced) StartLayers();
        }

        private void OnDisable()
        {
            if (song != null) song.SongStateChanged -= OnSongStateChanged;
            if (level != null) level.StateChanged -= OnLevelStateChanged;
            resumeScheduled = false;
            FinishStop();
        }

        private void OnValidate()
        {
            // Para ajustar la mezcla desde el Inspector con el juego corriendo.
            if (introSource == null) return;
            Apply(introSource);
            for (int i = 0; i < layerSources.Length; i++) Apply(layerSources[i]);
        }

        private void Apply(AudioSource source)
        {
            source.spatialBlend = spatialBlend;
            source.outputAudioMixerGroup = output;
            if (!stopping) source.volume = volume;
        }

        private void Update()
        {
            if (stopping) TickStop();
            if (silenced || song == null) return;

            if (song.State == SongState.Interrupted) TickRecovery();
            else if (song.State == SongState.Singing) TickBeat();
        }

        // ---------- Lo que dice la canción ----------

        private void OnSongStateChanged(SongState state)
        {
            if (silenced) return;

            switch (state)
            {
                case SongState.Singing:
                    // Si la entrada ya sonó, las capas quedaron programadas pegadas a ella: no hay que hacer nada más.
                    if (resumeScheduled) resumeScheduled = false;
                    else StartLayers();
                    break;

                case SongState.Interrupted:
                    RememberWhereToResume();
                    resumeScheduled = false;
                    lastRemaining = float.MaxValue;
                    BeginStop(cutTime);
                    break;

                default: // Silent: la bardo cayó
                    resumeScheduled = false;
                    nextBar = 0;
                    BeginStop(fadeTime);
                    break;
            }
        }

        private void OnLevelStateChanged(GameState state)
        {
            if (state != GameState.Defeat) return;
            silenced = true;
            resumeScheduled = false;
            BeginStop(fadeTime);
        }

        // ---------- Sonar ----------

        /// <summary>Arranca las capas ya (sin entrada), en el compás que toca.</summary>
        private void StartLayers()
        {
            if (baseSource == null) return;

            FinishStop();
            ScheduleLayers(AudioSettings.dspTime + ScheduleMargin);
        }

        /// <summary>
        /// Programa cada capa en su momento a partir de startDsp. La primera vez entran una por una, cada startGapBars
        /// compases. En un regreso entran juntas las primeras layersOnReturn, y las demás cada returnGapBars compases.
        /// Una capa que entra tarde arranca en el compás por el que ya van las otras: todas siguen la misma partitura.
        /// </summary>
        private void ScheduleLayers(double startDsp)
        {
            loopStartDsp = startDsp;
            for (int i = 0; i < layerSources.Length; i++)
            {
                AudioSource source = layerSources[i];
                if (source.clip == null) continue;

                int delayBars = everStarted ? Math.Max(0, i - layersOnReturn + 1) * returnGapBars : i * startGapBars;
                source.timeSamples = (nextBar + delayBars) % totalBars * samplesPerBar;
                source.PlayScheduled(startDsp + delayBars * barSeconds);
            }
            everStarted = true;
            lastBeat = -1;
        }

        /// <summary>
        /// Mientras la canción está cortada: espera a que a la recuperación le falte lo que dura la entrada y entonces
        /// programa la entrada y, pegadas a ella, las capas. SongBuff no avisa si otro golpe reinicia la recuperación
        /// (el estado no cambia), así que eso se detecta aquí: el tiempo que falta sube en vez de bajar.
        /// </summary>
        private void TickRecovery()
        {
            float remaining = song.RecoveryRemaining;

            if (resumeScheduled)
            {
                if (remaining > lastRemaining + 0.001f)
                {
                    resumeScheduled = false;
                    BeginStop(cutTime);
                }
            }
            else if (!stopping && introClip != null && baseSource != null && remaining <= introLength + ScheduleMargin)
            {
                ScheduleResume(remaining);
            }

            lastRemaining = remaining;
        }

        private void ScheduleResume(float remaining)
        {
            FinishStop();

            double startAt = AudioSettings.dspTime + ScheduleMargin;
            // Si la recuperación dura menos que la entrada, la entrada arranca ya empezada para terminar a tiempo.
            double skip = introLength - (remaining - ScheduleMargin);
            if (skip < 0.03) skip = 0.0;
            int skipSamples = (int)(skip * introClip.frequency);

            double layersAt = startAt;
            if (skipSamples < introClip.samples)
            {
                introSource.timeSamples = skipSamples;
                introSource.PlayScheduled(startAt);
                layersAt = startAt + (introClip.samples - skipSamples) / (double)introClip.frequency;
            }

            ScheduleLayers(layersAt);
            resumeScheduled = true;
        }

        /// <summary>Al cortarse, anota dónde vuelve: al inicio del siguiente bloque de resumeEveryBars compases.</summary>
        private void RememberWhereToResume()
        {
            if (!IsPlaying) return;

            int bar = baseSource.timeSamples / samplesPerBar;
            int next = (bar / resumeEveryBars + 1) * resumeEveryBars;
            nextBar = next < totalBars ? next : 0;
        }

        private void TickBeat()
        {
            if (!IsPlaying) return;

            int beat = (int)(baseSource.timeSamples / samplesPerBeat);
            if (beat == lastBeat) return;
            lastBeat = beat;
            Beat?.Invoke(beat % beatsPerBar);
        }

        // ---------- Callar ----------

        private bool AnythingPlaying()
        {
            if (introSource != null && introSource.isPlaying) return true;
            for (int i = 0; i < layerSources.Length; i++)
                if (layerSources[i].isPlaying) return true;
            return false;
        }

        /// <summary>Frena lo que esté sonando como una cinta: el tono baja hasta cero en los segundos indicados.</summary>
        private void BeginStop(float seconds)
        {
            if (introSource == null) return;

            if (!AnythingPlaying())
            {
                FinishStop();
                return;
            }
            stopping = true;
            stopTimer = 0f;
            stopDuration = Mathf.Max(0.01f, seconds);
        }

        private void TickStop()
        {
            stopTimer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(stopTimer / stopDuration);
            if (k >= 1f)
            {
                FinishStop();
                return;
            }

            float pitch = Mathf.Max(0.02f, 1f - k);
            float gain = volume * (1f - k * k);
            SetAll(pitch, gain);
        }

        /// <summary>Deja todas las fuentes calladas y listas para volver a sonar.</summary>
        private void FinishStop()
        {
            stopping = false;
            if (introSource == null) return;

            introSource.Stop();
            for (int i = 0; i < layerSources.Length; i++) layerSources[i].Stop();
            SetAll(1f, volume);
        }

        private void SetAll(float pitch, float gain)
        {
            introSource.pitch = pitch;
            introSource.volume = gain;
            for (int i = 0; i < layerSources.Length; i++)
            {
                layerSources[i].pitch = pitch;
                layerSources[i].volume = gain;
            }
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip, bool loop)
        {
            GameObject holder = new GameObject(sourceName);
            holder.transform.SetParent(transform, false);

            AudioSource source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = spatialBlend;
            source.outputAudioMixerGroup = output;
            source.dopplerLevel = 0f;
            source.priority = 0;   // es la música del nivel: que nunca la desplace un efecto
            return source;
        }
    }
}
