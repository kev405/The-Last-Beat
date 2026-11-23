using System;
using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.Heroes
{
    /// <summary>
    /// El sonido de la canción de la bardo (tarea 32). Escucha SongBuff.SongStateChanged, igual que SongAnimation:
    ///  - Singing: suena el loop del dembow.
    ///  - Interrupted: la canción se frena como una cinta (el tono baja hasta cero) y queda el silencio. Cuando a la
    ///    recuperación le falta lo que dura la entrada, suena la entrada y el loop arranca pegado a ella, en la
    ///    frase siguiente a la que se cortó. Otro golpe durante la recuperación corta también la entrada.
    ///  - Silent (la bardo cayó): se apaga más despacio.
    /// También se apaga si el nivel termina en derrota (LevelFlow.StateChanged); en la victoria sigue sonando.
    /// Avisa de cada pulso con Beat, para que los efectos se muevan al ritmo (ver BeatPulse).
    /// Va en el mismo objeto que SongBuff. Las dos fuentes de audio las crea sola al empezar.
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
        [Tooltip("El dembow. Debe durar un número entero de compases.")]
        [SerializeField] private AudioClip loopClip;
        [Tooltip("Entrada que suena justo antes de que la canción vuelva. Opcional.")]
        [SerializeField] private AudioClip introClip;

        [Header("Ritmo")]
        [SerializeField, Min(30f)] private float bpm = 96f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;
        [Tooltip("Después de un corte, la canción vuelve en el siguiente bloque de este tamaño (en compases). 4 = una frase.")]
        [SerializeField, Min(1)] private int resumeEveryBars = 4;

        [Header("Mezcla")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [Tooltip("0 = se oye igual en todo el nivel (música). 1 = sale de la posición de la bardo.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
        [Tooltip("Opcional: grupo del Audio Mixer por el que sale.")]
        [SerializeField] private AudioMixerGroup output;

        [Header("Corte")]
        [Tooltip("Segundos que tarda en frenarse cuando un golpe corta la canción.")]
        [SerializeField, Min(0.05f)] private float cutTime = 0.35f;
        [Tooltip("Segundos que tarda en apagarse cuando la bardo cae o se pierde el nivel.")]
        [SerializeField, Min(0.05f)] private float fadeTime = 1.2f;

        /// <summary>Margen para programar un clip con precisión de muestra (AudioSource.PlayScheduled).</summary>
        private const double ScheduleMargin = 0.05;

        private AudioSource loopSource;
        private AudioSource introSource;

        private double samplesPerBeat;
        private int samplesPerBar = 1;
        private int totalBars = 1;
        private double introLength;

        private int nextBar;            // compás del loop en el que arranca la próxima vez
        private double loopStartDsp;    // momento (AudioSettings.dspTime) en el que el loop empieza a oírse
        private bool resumeScheduled;   // la entrada y el loop ya quedaron programados para el regreso
        private float lastRemaining;
        private bool stopping;
        private float stopTimer;
        private float stopDuration;
        private bool silenced;          // el nivel se perdió: no vuelve a sonar
        private int lastBeat = -1;

        /// <summary>Un pulso del dembow. El número es el tiempo dentro del compás: 0 es el primero.</summary>
        public event Action<int> Beat;

        public float Bpm => bpm;

        /// <summary>True mientras el loop se oye (no durante la entrada ni mientras se frena).</summary>
        public bool IsPlaying =>
            loopSource != null && !stopping && loopSource.isPlaying && AudioSettings.dspTime >= loopStartDsp;

        /// <summary>Avance dentro del pulso actual, de 0 a 1. Sirve para animar algo de forma continua con el ritmo.</summary>
        public float BeatProgress =>
            IsPlaying && samplesPerBeat > 0.0 ? (float)(loopSource.timeSamples % samplesPerBeat / samplesPerBeat) : 0f;

        private void Awake()
        {
            if (song == null) song = GetComponent<SongBuff>();
            if (level == null) level = FindFirstObjectByType<LevelFlow>();

            loopSource = CreateSource("SongAudio_Loop", loopClip, true);
            introSource = CreateSource("SongAudio_Intro", introClip, false);

            if (loopClip == null)
            {
                Debug.LogWarning("[BardSongAudio] Falta el clip del loop: ejecuta \"The Last Beat/Audio/Montar canción de la bardo\".", this);
                return;
            }

            samplesPerBeat = loopClip.frequency * 60.0 / bpm;
            samplesPerBar = Mathf.Max(1, (int)Math.Round(samplesPerBeat * beatsPerBar));
            totalBars = Mathf.Max(1, (int)Math.Round(loopClip.samples / (double)samplesPerBar));
            if (Math.Abs(loopClip.samples - (long)totalBars * samplesPerBar) > loopClip.frequency / 100)
                Debug.LogWarning($"[BardSongAudio] {loopClip.name} no dura un número entero de compases a {bpm} BPM: el ritmo se va a descuadrar.", this);

            if (introClip != null) introLength = introClip.samples / (double)introClip.frequency;

            // Los deja cargados desde ya, aunque el clip no tenga marcado Preload Audio Data: así el primer
            // PlayScheduled no espera a que se cargue y la entrada no da un tirón en mitad del combate.
            loopClip.LoadAudioData();
            if (introClip != null) introClip.LoadAudioData();
        }

        private void OnEnable()
        {
            if (song != null) song.SongStateChanged += OnSongStateChanged;
            if (level != null) level.StateChanged += OnLevelStateChanged;
            if (song != null && song.State == SongState.Singing && !silenced) StartLoop();
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
            if (loopSource == null || introSource == null) return;
            loopSource.spatialBlend = spatialBlend;
            introSource.spatialBlend = spatialBlend;
            loopSource.outputAudioMixerGroup = output;
            introSource.outputAudioMixerGroup = output;
            if (stopping) return;
            loopSource.volume = volume;
            introSource.volume = volume;
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
                    // Si la entrada ya sonó, el loop quedó programado pegado a ella: no hay que hacer nada más.
                    if (resumeScheduled) resumeScheduled = false;
                    else StartLoop();
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

        /// <summary>Arranca el loop ya (sin entrada), en el compás que toca.</summary>
        private void StartLoop()
        {
            if (loopClip == null) return;

            FinishStop();
            loopStartDsp = AudioSettings.dspTime + ScheduleMargin;
            loopSource.timeSamples = nextBar * samplesPerBar;
            loopSource.PlayScheduled(loopStartDsp);
            lastBeat = -1;
        }

        /// <summary>
        /// Mientras la canción está cortada: espera a que a la recuperación le falte lo que dura la entrada y entonces
        /// programa la entrada y, pegado a ella, el loop. SongBuff no avisa si otro golpe reinicia la recuperación
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
            else if (!stopping && introClip != null && loopClip != null && remaining <= introLength + ScheduleMargin)
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

            if (skipSamples < introClip.samples)
            {
                introSource.timeSamples = skipSamples;
                introSource.PlayScheduled(startAt);
                loopStartDsp = startAt + (introClip.samples - skipSamples) / (double)introClip.frequency;
            }
            else
            {
                loopStartDsp = startAt;
            }

            loopSource.timeSamples = nextBar * samplesPerBar;
            loopSource.PlayScheduled(loopStartDsp);
            lastBeat = -1;
            resumeScheduled = true;
        }

        /// <summary>Al cortarse, anota dónde vuelve: al inicio del siguiente bloque de resumeEveryBars compases.</summary>
        private void RememberWhereToResume()
        {
            if (!IsPlaying) return;

            int bar = loopSource.timeSamples / samplesPerBar;
            int next = (bar / resumeEveryBars + 1) * resumeEveryBars;
            nextBar = next < totalBars ? next : 0;
        }

        private void TickBeat()
        {
            if (!IsPlaying) return;

            int beat = (int)(loopSource.timeSamples / samplesPerBeat);
            if (beat == lastBeat) return;
            lastBeat = beat;
            Beat?.Invoke(beat % beatsPerBar);
        }

        // ---------- Callar ----------

        /// <summary>Frena lo que esté sonando como una cinta: el tono baja hasta cero en los segundos indicados.</summary>
        private void BeginStop(float seconds)
        {
            if (loopSource == null || introSource == null) return;

            if (!loopSource.isPlaying && !introSource.isPlaying)
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
            loopSource.pitch = pitch;
            introSource.pitch = pitch;
            loopSource.volume = gain;
            introSource.volume = gain;
        }

        /// <summary>Deja las dos fuentes calladas y listas para volver a sonar.</summary>
        private void FinishStop()
        {
            stopping = false;
            if (loopSource == null || introSource == null) return;

            loopSource.Stop();
            introSource.Stop();
            loopSource.pitch = 1f;
            introSource.pitch = 1f;
            loopSource.volume = volume;
            introSource.volume = volume;
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
