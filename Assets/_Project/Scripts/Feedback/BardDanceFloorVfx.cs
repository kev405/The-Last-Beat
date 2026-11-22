using TheLastBeat.Abilities;
using TheLastBeat.Characters;
using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Efecto de la Pista de baile de la bardo (tarea 33). Escucha BardActionMoments y, en el Hit (cuando cae y
    /// rasguea hacia el piso), abre en el suelo una pista redonda de baldosas de colores:
    ///  - crece desde el centro con un pequeño rebote;
    ///  - las baldosas cambian de color a cada compás ("beat") y la pista late con ellas;
    ///  - del borde y del interior suben destellos mientras dura;
    ///  - al acabar la duración se desvanece.
    /// La pista se queda donde se lanzó aunque la bardo se aleje: por eso, al empezar la partida, la raíz del efecto
    /// se suelta de la bardo (en el editor sigue siendo hija suya, para que el menú la pueda rehacer).
    /// Solo dibuja: la curación la hace DanceFloorAbility. Si esa habilidad está enlazada en el campo Ability, la
    /// pista se abre con su evento Opened (mismo centro, radio y duración que la zona que cura); si no, se abre
    /// sola en el momento de la animación con Radius y Duration.
    /// Mientras la pista está abierta, cada curación (CombatEvents.Healed) suelta unos destellos verdes sobre el curado.
    /// </summary>
    public class BardDanceFloorVfx : MonoBehaviour
    {
        [SerializeField] private BardActionMoments moments;
        [Tooltip("Raíz del efecto (VFX_DanceFloor). En Play se suelta de la bardo.")]
        [SerializeField] private Transform root;
        [Tooltip("Quad plano con el material de la pista.")]
        [SerializeField] private Renderer floor;
        [SerializeField] private ParticleSystem rimSparkles;
        [SerializeField] private ParticleSystem innerSparkles;
        [Tooltip("Destellos verdes que salen sobre cada héroe curado.")]
        [SerializeField] private ParticleSystem healSparkles;
        [Tooltip("Opcional. La habilidad que cura: con ella, la pista se abre con su radio y su duración reales.")]
        [SerializeField] private DanceFloorAbility ability;
        [Tooltip("Variantes de color de las baldosas; se alternan a cada compás.")]
        [SerializeField] private Texture2D[] frames = new Texture2D[0];

        [Header("Habilidad (nivel 1)")]
        [SerializeField, Min(0.5f)] private float radius = 3.5f;
        [SerializeField, Min(0.5f)] private float duration = 6f;
        [Tooltip("Sin habilidad enlazada: la pista se abre sola en el momento de la animación, con los valores de arriba.")]
        [SerializeField] private bool showOnHit = true;

        [Header("Aspecto")]
        [Tooltip("Segundos entre cambios de color.")]
        [SerializeField, Min(0.05f)] private float beat = 0.4f;
        [SerializeField, Min(0.05f)] private float growTime = 0.45f;
        [SerializeField, Min(0.05f)] private float fadeTime = 0.5f;
        [Tooltip("Cuánto crece la pista en cada compás (0.03 = 3 %).")]
        [SerializeField, Range(0f, 0.2f)] private float pulse = 0.03f;
        [Tooltip("Giro de las baldosas respecto al norte de la escena, en grados.")]
        [SerializeField] private float tileAngle = 45f;
        [Tooltip("Altura sobre el suelo, para que no parpadee contra el piso.")]
        [SerializeField] private float groundOffset = 0.05f;
        [Tooltip("Parte del quad que ocupa el disco en la textura (el resto es margen transparente). Lo fija el menú.")]
        [SerializeField, Range(0.5f, 1f)] private float discFraction = 0.94f;
        [Tooltip("Destellos por segundo y por metro de borde.")]
        [SerializeField, Min(0f)] private float rimSparklesPerMeter = 0.8f;
        [Tooltip("Destellos verdes por cada curación.")]
        [SerializeField, Min(0)] private int sparklesPerHeal = 5;
        [Tooltip("Altura sobre los pies del curado a la que salen.")]
        [SerializeField] private float healSparklesHeight = 1.1f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private MaterialPropertyBlock block;
        private float currentRadius;
        private float currentDuration;
        private float startTime;
        private bool showing;
        private bool emitting;

        public float Radius
        {
            get { return radius; }
            set { radius = Mathf.Max(0.5f, value); }
        }

        public float Duration
        {
            get { return duration; }
            set { duration = Mathf.Max(0.5f, value); }
        }

        /// <summary>true mientras la pista está en el suelo.</summary>
        public bool IsShowing
        {
            get { return showing; }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (root != null)
            {
                // Fuera de la bardo: así ni la pista ni sus destellos se mueven con ella.
                root.SetParent(null, true);
                root.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (moments != null) moments.Hit += OnHit;
            if (ability != null) ability.Opened += Show;
            CombatEvents.Healed += OnHealed;
        }

        private void OnDisable()
        {
            if (moments != null) moments.Hit -= OnHit;
            if (ability != null) ability.Opened -= Show;
            CombatEvents.Healed -= OnHealed;
            Hide();
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        private void OnHit(BardAction action)
        {
            // Con habilidad enlazada, quien abre la pista es su evento Opened.
            if (action == BardAction.DanceFloor && showOnHit && ability == null) Show(transform.position, radius, duration);
        }

        /// <summary>Destellos verdes sobre quien se acaba de curar, mientras la pista está abierta.</summary>
        private void OnHealed(Health target, float amount)
        {
            if (!showing || healSparkles == null || target == null || sparklesPerHeal <= 0) return;

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.position = target.transform.position + Vector3.up * healSparklesHeight;
            emit.applyShapeToPosition = true;   // repartidos alrededor de ese punto con la forma del sistema
            healSparkles.Emit(emit, sparklesPerHeal);
        }

        /// <summary>Abre la pista centrada en ese punto del suelo, con ese radio, durante esos segundos.</summary>
        public void Show(Vector3 center, float floorRadius, float seconds)
        {
            if (root == null) return;

            currentRadius = Mathf.Max(0.5f, floorRadius);
            currentDuration = Mathf.Max(growTime + fadeTime, seconds);
            startTime = Time.time;
            showing = true;
            emitting = true;

            root.position = new Vector3(center.x, center.y + groundOffset, center.z);
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
            root.gameObject.SetActive(true);

            if (floor != null)
            {
                floor.transform.localPosition = Vector3.zero;
                floor.transform.localRotation = Quaternion.Euler(90f, tileAngle, 0f);
            }

            if (healSparkles != null && !healSparkles.isPlaying) healSparkles.Play();
            StartSparkles(rimSparkles, currentRadius, 2f * Mathf.PI * currentRadius * rimSparklesPerMeter);
            StartSparkles(innerSparkles, currentRadius * 0.9f, -1f);

            Refresh(0f);
        }

        private void Update()
        {
            if (!showing) return;

            float elapsed = Time.time - startTime;
            if (elapsed >= currentDuration)
            {
                Hide();
                return;
            }

            // Los destellos dejan de salir cuando la pista empieza a apagarse.
            if (emitting && elapsed >= currentDuration - fadeTime)
            {
                emitting = false;
                if (rimSparkles != null) rimSparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (innerSparkles != null) innerSparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Refresh(elapsed);
        }

        private void Refresh(float elapsed)
        {
            if (floor == null) return;

            // Crece con un rebote corto (se pasa un poco y vuelve).
            float grow = Mathf.Clamp01(elapsed / growTime);
            float back = grow - 1f;
            float eased = 1f + 2.70158f * back * back * back + 1.70158f * back * back;

            float beatPhase = Mathf.Repeat(elapsed, beat) / beat;
            float throb = 1f + pulse * (1f - beatPhase) * (1f - beatPhase);

            float diameter = Mathf.Max(0.01f, eased) * throb * currentRadius * 2f / discFraction;
            floor.transform.localScale = new Vector3(diameter, diameter, 1f);

            float alpha = Mathf.Clamp01((currentDuration - elapsed) / fadeTime) * Mathf.Clamp01(grow * 4f);

            floor.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
            if (frames != null && frames.Length > 0)
            {
                Texture2D frame = frames[Mathf.FloorToInt(elapsed / beat) % frames.Length];
                if (frame != null) block.SetTexture(BaseMapId, frame);
            }
            floor.SetPropertyBlock(block);
        }

        /// <summary>Ajusta el círculo de emisión al radio y arranca. rate menor que 0 = dejar el del sistema.</summary>
        private static void StartSparkles(ParticleSystem system, float shapeRadius, float rate)
        {
            if (system == null) return;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.radius = shapeRadius;

            if (rate >= 0f)
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = rate;
            }

            system.Clear(true);
            system.Play(true);
        }

        private void Hide()
        {
            showing = false;
            emitting = false;
            if (root != null) root.gameObject.SetActive(false);
        }
    }
}
