using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// El ambiente del pueblo en silencio (tarea 32): el viento suena en bucle desde que carga el nivel y se apaga
    /// cuando LevelFlow pasa a Combat. No vuelve: desde ahí, lo que se oye (o deja de oírse) es la canción.
    /// Usa tiempo real, así que el fundido no depende de la pausa de impacto ni de Time.timeScale.
    /// </summary>
    public class AmbiencePlayer : MonoBehaviour
    {
        [Tooltip("Si se deja vacío, lo busca en la escena.")]
        [SerializeField] private LevelFlow level;
        [Tooltip("El viento: un clip que se repite sin costura.")]
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
        [Tooltip("Segundos que tarda en entrar al cargar el nivel.")]
        [SerializeField, Min(0.01f)] private float fadeIn = 1f;
        [Tooltip("Segundos que tarda en apagarse cuando empieza el combate.")]
        [SerializeField, Min(0.01f)] private float fadeOut = 2.5f;
        [Tooltip("Opcional: grupo Ambiente del Audio Mixer.")]
        [SerializeField] private AudioMixerGroup output;

        private AudioSource source;
        private float target;

        private void Awake()
        {
            if (level == null) level = FindFirstObjectByType<LevelFlow>();

            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = output;
            if (clip != null) clip.LoadAudioData();
        }

        private void OnEnable()
        {
            if (level != null) level.StateChanged += OnLevelStateChanged;
        }

        private void OnDisable()
        {
            if (level != null) level.StateChanged -= OnLevelStateChanged;
        }

        private void Start()
        {
            // LevelFlow empieza en Exploring. Si no hay LevelFlow, el viento suena siempre.
            if (level == null || level.State == GameState.Exploring) FadeTo(volume);
        }

        private void OnLevelStateChanged(GameState state) => FadeTo(state == GameState.Exploring ? volume : 0f);

        private void FadeTo(float value)
        {
            target = value;
            if (value > 0f && clip != null && !source.isPlaying) source.Play();
        }

        private void Update()
        {
            if (Mathf.Approximately(source.volume, target)) return;

            float seconds = target > source.volume ? fadeIn : fadeOut;
            source.volume = Mathf.MoveTowards(source.volume, target, volume * Time.unscaledDeltaTime / seconds);
            if (source.volume <= 0f && target <= 0f) source.Stop();
        }
    }
}
