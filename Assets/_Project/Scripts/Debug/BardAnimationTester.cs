using TheLastBeat.Abilities;
using TheLastBeat.Heroes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Teclas de prueba para la bardo. En Play: 7 = Nota (ataque básico), 8 = Golpe de bajo, 9 = Pista de baile.
    ///
    /// Si la bardo ya tiene su combate montado (BardBasicAttack y AbilityCaster), las teclas usan el ataque y las
    /// habilidades de verdad, con las mismas reglas que la IA: la Nota no vuelve a salir hasta que termina la
    /// anterior y las habilidades respetan su recarga (se ve en el HUD). Así se puede probar a mano; para que la IA
    /// no las use por su cuenta mientras tanto, apaga Auto Cast en BardCombatAI.
    /// Si el combate no está montado, solo dispara los triggers del Animator para ver las animaciones.
    ///
    /// Lo añade el menú "Montar bardo" mientras BardVisualBuilder.AddAnimationTester sea true.
    /// </summary>
    public class BardAnimationTester : MonoBehaviour
    {
        [Tooltip("Si se deja vacío, usa el primer Animator de los hijos.")]
        [SerializeField] private Animator animator;

        private static readonly int NoteHash = Animator.StringToHash("Note");
        private static readonly int BassDropHash = Animator.StringToHash("BassDrop");
        private static readonly int DanceFloorHash = Animator.StringToHash("DanceFloor");

        private BardBasicAttack attack;
        private AbilityCaster caster;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            attack = GetComponent<BardBasicAttack>();
            caster = GetComponent<AbilityCaster>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit7Key.wasPressedThisFrame) PlayNote();
            if (keyboard.digit8Key.wasPressedThisFrame) PlayBassDrop();
            if (keyboard.digit9Key.wasPressedThisFrame) PlayDanceFloor();
        }

        [ContextMenu("Probar Nota")]
        public void PlayNote()
        {
            if (attack != null) attack.TryAttackNearest();
            else Fire(NoteHash);
        }

        [ContextMenu("Probar Golpe de bajo")]
        public void PlayBassDrop()
        {
            if (caster != null && caster.SlotCount > 0 && caster.GetAbility(0) != null) caster.TryUse(0);
            else Fire(BassDropHash);
        }

        [ContextMenu("Probar Pista de baile")]
        public void PlayDanceFloor()
        {
            if (caster != null && caster.SlotCount > 1 && caster.GetAbility(1) != null) caster.TryUse(1);
            else Fire(DanceFloorHash);
        }

        private void Fire(int trigger)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.SetTrigger(trigger);
        }
    }
}
