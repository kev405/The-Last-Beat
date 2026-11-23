using TheLastBeat.Heroes;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Hace que un objeto lata con la canción (tarea 33): en cada pulso de BardSongAudio crece un poco y vuelve a
    /// su tamaño, y en el primer tiempo del compás crece algo más. Va en el objeto que late; por ejemplo, en el
    /// disco del área de la canción (Aura). Para apagarlo basta con desactivar el componente.
    /// </summary>
    public class BeatPulse : MonoBehaviour
    {
        [Tooltip("Si se deja vacío, usa el BardSongAudio de los padres de este objeto.")]
        [SerializeField] private BardSongAudio songAudio;
        [Tooltip("Cuánto crece en cada pulso. 0.03 = 3 %.")]
        [SerializeField, Range(0f, 0.5f)] private float amount = 0.03f;
        [Tooltip("Lo que crece de más en el primer tiempo del compás.")]
        [SerializeField, Range(0f, 0.5f)] private float downbeatExtra = 0.03f;
        [Tooltip("Segundos que tarda en volver a su tamaño.")]
        [SerializeField, Min(0.02f)] private float settleTime = 0.22f;
        [Tooltip("Crece solo a lo ancho (X y Z), no hacia arriba. Para discos y marcas en el suelo.")]
        [SerializeField] private bool flat = true;

        private Vector3 baseScale;
        private float size;        // lo que está crecido ahora mismo
        private float sizeStart;   // lo que creció en el último pulso

        private void Awake()
        {
            baseScale = transform.localScale;
            if (songAudio == null) songAudio = GetComponentInParent<BardSongAudio>();
        }

        private void OnEnable()
        {
            if (songAudio != null) songAudio.Beat += OnBeat;
        }

        private void OnDisable()
        {
            if (songAudio != null) songAudio.Beat -= OnBeat;
            size = 0f;
            transform.localScale = baseScale;
        }

        private void OnBeat(int beat)
        {
            size = sizeStart = amount + (beat == 0 ? downbeatExtra : 0f);
            Apply();
        }

        private void Update()
        {
            if (size <= 0f) return;

            // Tiempo real, como el audio: el pulso no se frena con la pausa de impacto.
            size = Mathf.MoveTowards(size, 0f, sizeStart * Time.unscaledDeltaTime / settleTime);
            Apply();
        }

        private void Apply()
        {
            float k = 1f + size;
            transform.localScale = flat
                ? new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k)
                : baseScale * k;
        }
    }
}
