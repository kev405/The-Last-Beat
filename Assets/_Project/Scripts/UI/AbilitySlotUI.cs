using TheLastBeat.Abilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Una ranura de habilidad en el HUD (tarea 34): nombre, tecla y recarga.
    /// La sombra oscura se vacía en sentido horario mientras recarga y el número muestra los
    /// segundos que faltan. Solo reescribe el texto cuando cambia el segundo entero.
    /// </summary>
    public class AbilitySlotUI : MonoBehaviour
    {
        [SerializeField] private AbilityCaster caster;
        [Tooltip("0 = Ability1 (Q), 1 = Ability2 (E).")]
        [SerializeField, Min(0)] private int slot;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;

        private int lastSeconds = -1;

        private void Start()
        {
            if (caster == null || nameText == null) return;
            Ability ability = caster.GetAbility(slot);
            if (ability != null && ability.Data != null) nameText.SetText(ability.Data.displayName.ToUpperInvariant());
        }

        private void Update()
        {
            if (caster == null) return;

            float fraction = caster.GetCooldown01(slot);
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = fraction;

            int seconds = Mathf.CeilToInt(caster.GetCooldownRemaining(slot));
            if (seconds == lastSeconds || cooldownText == null) return;
            lastSeconds = seconds;
            if (seconds > 0) cooldownText.SetText("{0}", seconds);
            else cooldownText.SetText(string.Empty);
        }
    }
}
