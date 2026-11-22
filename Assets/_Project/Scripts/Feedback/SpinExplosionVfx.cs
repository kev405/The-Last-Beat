using TheLastBeat.Abilities;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Onda expansiva al terminar el giro (tarea 33). Escucha SpinAbility.Exploded, que entrega el centro
    /// y el radio real de la explosión (ya con la potenciación de la bardo), y con eso:
    ///  - agranda un anillo plano en el suelo hasta ese radio mientras se desvanece;
    ///  - lanza una ráfaga de chispas hacia fuera que llega hasta el borde.
    /// El anillo se queda donde ocurrió la explosión aunque el caballero se mueva.
    /// "delay" retrasa el efecto para que coincida con la espada tocando el suelo en la animación SpinFinish.
    /// </summary>
    public class SpinExplosionVfx : MonoBehaviour
    {
        [SerializeField] private SpinAbility spin;
        [Tooltip("Quad plano con el material del anillo. Se apaga solo cuando no hay explosión.")]
        [SerializeField] private Transform ring;
        [SerializeField] private ParticleSystem sparks;

        [Header("Tiempos")]
        [Tooltip("Segundos entre la explosión del código y el inicio del efecto.")]
        [SerializeField, Min(0f)] private float delay = 0.15f;
        [SerializeField, Min(0.05f)] private float duration = 0.4f;

        [Header("Aspecto")]
        [SerializeField] private Color color = new Color(0.35f, 0.75f, 1f, 1f);
        [Tooltip("Tamaño inicial del anillo como fracción del radio final.")]
        [SerializeField, Range(0f, 1f)] private float startScale = 0.15f;
        [Tooltip("Altura del anillo sobre el suelo, para que no parpadee contra el piso.")]
        [SerializeField] private float groundOffset = 0.06f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer ringRenderer;
        private MaterialPropertyBlock block;
        private Vector3 center;
        private float radius;
        private float startTime;
        private bool waiting;
        private bool running;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (ring != null)
            {
                ringRenderer = ring.GetComponent<Renderer>();
                ring.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (spin != null) spin.Exploded += OnExploded;
        }

        private void OnDisable()
        {
            if (spin != null) spin.Exploded -= OnExploded;
            waiting = false;
            running = false;
            if (ring != null) ring.gameObject.SetActive(false);
        }

        private void OnExploded(Vector3 explosionCenter, float explosionRadius)
        {
            // SpinAbility entrega el centro medio metro sobre los pies: se baja al suelo.
            center = new Vector3(explosionCenter.x, transform.position.y + groundOffset, explosionCenter.z);
            radius = Mathf.Max(0.1f, explosionRadius);
            startTime = Time.time + delay;
            waiting = true;
            running = false;
        }

        private void LateUpdate()
        {
            if (waiting && Time.time >= startTime)
            {
                waiting = false;
                running = true;
                if (ring != null) ring.gameObject.SetActive(true);
                PlaySparks();
            }

            if (!running) return;

            float t = Mathf.Clamp01((Time.time - startTime) / duration);
            if (ring != null)
            {
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                float diameter = Mathf.Lerp(startScale, 1f, eased) * radius * 2f;

                // Posición y giro en el mundo: el anillo no sigue al caballero ni a su rotación.
                ring.position = center;
                ring.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.localScale = new Vector3(diameter, diameter, 1f);

                if (ringRenderer != null)
                {
                    Color c = color;
                    c.a *= Mathf.Pow(1f - t, 1.5f);
                    ringRenderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, c);
                    ringRenderer.SetPropertyBlock(block);
                }
            }

            if (t >= 1f)
            {
                running = false;
                if (ring != null) ring.gameObject.SetActive(false);
            }
        }

        private void PlaySparks()
        {
            if (sparks == null) return;

            sparks.transform.position = center + Vector3.up * 0.25f;

            // Velocidad para que las chispas recorran el radio durante su vida.
            ParticleSystem.MainModule main = sparks.main;
            float lifetime = Mathf.Max(0.05f, main.startLifetime.constantMax);
            float speed = radius / lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);

            sparks.Play(true);
        }
    }
}
