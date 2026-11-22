using TheLastBeat.Abilities;
using TheLastBeat.Characters;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Efecto del Golpe de bajo de la bardo (tarea 33). Escucha BardActionMoments:
    ///  - Started: resplandor sobre el laúd mientras lo sube sobre la cabeza.
    ///  - Hit (el pisotón): un anillo morado crece por el suelo hasta el radio de la habilidad mientras se desvanece,
    ///    con un destello en el centro y píxeles que salen despedidos hasta el borde.
    /// El anillo se queda donde pisó aunque la bardo se mueva. Solo dibuja: el empuje y el daño los hace
    /// BassDropAbility. Si esa habilidad está enlazada en el campo Ability, la onda sale con su evento Dropped
    /// (mismo centro y radio real que el golpe); si no, sale sola en el momento de la animación con Radius.
    /// </summary>
    public class BardBassDropVfx : MonoBehaviour
    {
        [SerializeField] private BardActionMoments moments;
        [Tooltip("Quad plano con el material del anillo. Se apaga solo cuando no hay onda.")]
        [SerializeField] private Transform ring;
        [Tooltip("Quad plano con el destello del centro.")]
        [SerializeField] private Transform flash;
        [SerializeField] private ParticleSystem debris;
        [Tooltip("Resplandor del laúd, el mismo que usa BardNoteVfx.")]
        [SerializeField] private ParticleSystem glow;
        [Tooltip("Opcional. La habilidad que empuja: con ella, la onda sale con su radio real en el instante del golpe.")]
        [SerializeField] private BassDropAbility ability;

        [Header("Onda")]
        [Tooltip("Radio de la habilidad en metros (nivel 1: 4).")]
        [SerializeField, Min(0.5f)] private float radius = 4f;
        [SerializeField, Min(0.05f)] private float duration = 0.5f;
        [Tooltip("Sin habilidad enlazada: la onda sale sola al pisar, con el radio de arriba.")]
        [SerializeField] private bool playOnHit = true;

        [Header("Aspecto")]
        [SerializeField] private Color ringColor = Color.white;
        [SerializeField] private Color flashColor = new Color(0.62f, 0.20f, 1f, 0.55f);
        [Tooltip("Tamaño inicial del anillo como fracción del radio final.")]
        [SerializeField, Range(0f, 1f)] private float startScale = 0.12f;
        [Tooltip("Altura sobre el suelo, para que no parpadee contra el piso.")]
        [SerializeField] private float groundOffset = 0.06f;
        [Tooltip("Segundos entre que empieza la animación y pisa: el resplandor llega a su máximo justo ahí.")]
        [SerializeField, Min(0.05f)] private float chargeTime = 0.47f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer ringRenderer;
        private Renderer flashRenderer;
        private MaterialPropertyBlock block;
        private Vector3 center;
        private float currentRadius;
        private float startTime;
        private bool running;

        public float Radius
        {
            get { return radius; }
            set { radius = Mathf.Max(0.5f, value); }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (ring != null)
            {
                ringRenderer = ring.GetComponent<Renderer>();
                ring.gameObject.SetActive(false);
            }
            if (flash != null)
            {
                flashRenderer = flash.GetComponent<Renderer>();
                flash.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (moments != null)
            {
                moments.Started += OnStarted;
                moments.Hit += OnHit;
            }
            if (ability != null) ability.Dropped += Play;
        }

        private void OnDisable()
        {
            if (moments != null)
            {
                moments.Started -= OnStarted;
                moments.Hit -= OnHit;
            }
            if (ability != null) ability.Dropped -= Play;
            Hide();
        }

        private void OnStarted(BardAction action)
        {
            if (action == BardAction.BassDrop) BardNoteVfx.EmitGlow(glow, chargeTime);
        }

        private void OnHit(BardAction action)
        {
            // Con habilidad enlazada, quien lanza la onda es su evento Dropped.
            if (action == BardAction.BassDrop && playOnHit && ability == null) Play(transform.position, radius);
        }

        /// <summary>Lanza la onda desde ese punto (los pies de la bardo) hasta ese radio.</summary>
        public void Play(Vector3 origin, float waveRadius)
        {
            center = new Vector3(origin.x, origin.y + groundOffset, origin.z);
            currentRadius = Mathf.Max(0.1f, waveRadius);
            startTime = Time.time;
            running = true;

            if (ring != null) ring.gameObject.SetActive(true);
            if (flash != null) flash.gameObject.SetActive(true);

            if (debris != null)
            {
                debris.transform.position = center + Vector3.up * 0.2f;

                // Velocidad para que los píxeles recorran el radio durante su vida.
                ParticleSystem.MainModule main = debris.main;
                float lifetime = Mathf.Max(0.05f, main.startLifetime.constantMax);
                float speed = currentRadius / lifetime;
                main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
                debris.Play(true);
            }
        }

        private void LateUpdate()
        {
            if (!running) return;

            float t = Mathf.Clamp01((Time.time - startTime) / duration);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            // Posición y giro en el mundo: la onda no sigue a la bardo ni a su rotación.
            if (ring != null)
            {
                float diameter = Mathf.Lerp(startScale, 1f, eased) * currentRadius * 2f;
                Place(ring, center, diameter);
                Tint(ringRenderer, ringColor, Mathf.Pow(1f - t, 1.4f));
            }

            // El destello es más corto y llega a medio camino.
            if (flash != null)
            {
                float flashT = Mathf.Clamp01(t * 2f);
                float diameter = Mathf.Lerp(0.3f, 1.1f, 1f - (1f - flashT) * (1f - flashT)) * currentRadius;
                Place(flash, center + Vector3.up * 0.01f, diameter);
                Tint(flashRenderer, flashColor, 1f - flashT);
            }

            if (t >= 1f) Hide();
        }

        private static void Place(Transform quad, Vector3 position, float diameter)
        {
            quad.position = position;
            quad.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.localScale = new Vector3(diameter, diameter, 1f);
        }

        private void Tint(Renderer target, Color color, float alpha)
        {
            if (target == null) return;
            color.a *= Mathf.Clamp01(alpha);
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(block);
        }

        private void Hide()
        {
            running = false;
            if (ring != null) ring.gameObject.SetActive(false);
            if (flash != null) flash.gameObject.SetActive(false);
        }
    }
}
