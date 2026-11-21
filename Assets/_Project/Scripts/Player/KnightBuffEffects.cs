using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Lo que le pasa al caballero cuando la canción de la bardo lo potencia (tarea 19):
    /// más daño y más área en el combo y en las habilidades, y se enciende un anillo dorado
    /// a sus pies. La esquiva automática vive en KnightAutoDodge.
    /// </summary>
    [RequireComponent(typeof(BuffReceiver))]
    public class KnightBuffEffects : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private KnightCombat combat;
        [SerializeField] private AbilityCaster caster;
        [Tooltip("Anillo dorado bajo el caballero mientras está potenciado.")]
        [SerializeField] private GameObject buffIndicator;

        [Header("Efecto")]
        [Tooltip("1.3 = 30 % más daño.")]
        [SerializeField, Min(1f)] private float damageMultiplier = 1.3f;
        [Tooltip("1.35 = áreas de golpe 35 % más grandes.")]
        [SerializeField, Min(1f)] private float areaMultiplier = 1.35f;
        [SerializeField] private bool logToConsole = true;

        private BuffReceiver receiver;

        public bool IsBuffed => receiver != null && receiver.IsBuffed;

        private void Awake()
        {
            receiver = GetComponent<BuffReceiver>();
            if (combat == null) combat = GetComponent<KnightCombat>();
            if (caster == null) caster = GetComponent<AbilityCaster>();
            if (buffIndicator != null) buffIndicator.SetActive(false);
        }

        private void OnEnable()
        {
            receiver.BuffChanged += Apply;
            Apply(receiver.IsBuffed);
        }

        private void OnDisable()
        {
            receiver.BuffChanged -= Apply;
            Apply(false);
        }

        private void Apply(bool buffed)
        {
            float damage = buffed ? damageMultiplier : 1f;
            float area = buffed ? areaMultiplier : 1f;

            if (combat != null)
            {
                combat.DamageMultiplier = damage;
                combat.AreaMultiplier = area;
            }
            if (caster != null)
            {
                caster.DamageMultiplier = damage;
                caster.AreaMultiplier = area;
            }
            if (buffIndicator != null) buffIndicator.SetActive(buffed);
            if (logToConsole && Application.isPlaying)
                Debug.Log(buffed ? "[Caballero] potenciado por la canción" : "[Caballero] sin potenciación");
        }
    }
}
