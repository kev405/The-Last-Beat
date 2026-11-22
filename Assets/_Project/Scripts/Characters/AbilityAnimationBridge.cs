using System;
using TheLastBeat.Abilities;
using UnityEngine;

namespace TheLastBeat.Characters
{
    /// <summary>
    /// Dispara triggers del Animator cuando una habilidad empieza y, si hace falta, cuando termina (tarea 28).
    /// Escucha los eventos Ability.Started y Ability.Finished, así que las habilidades no necesitan
    /// conocer el Animator. Va en el mismo objeto que el AbilityCaster. Una fila por habilidad animada:
    ///  - ChargeAbility: trigger "Charge" (la animación termina sola).
    ///  - SpinAbility: trigger "Spin" al empezar y "SpinEnd" al terminar (la pose se mantiene en bucle).
    /// </summary>
    public class AbilityAnimationBridge : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public Ability ability;
            [Tooltip("Trigger del Animator que se dispara al lanzar la habilidad.")]
            public string trigger = "Charge";
            [Tooltip("Opcional. Trigger que se dispara cuando la habilidad termina o se cancela. Vacío = ninguno.")]
            public string finishedTrigger = "";
        }

        [Tooltip("Si se deja vacío, usa el primer Animator de los hijos.")]
        [SerializeField] private Animator animator;
        [SerializeField] private Entry[] entries = new Entry[0];

        private int[] startHashes;
        private int[] finishHashes;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            startHashes = new int[entries.Length];
            finishHashes = new int[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null) continue;
                startHashes[i] = string.IsNullOrEmpty(entries[i].trigger) ? 0 : Animator.StringToHash(entries[i].trigger);
                finishHashes[i] = string.IsNullOrEmpty(entries[i].finishedTrigger) ? 0 : Animator.StringToHash(entries[i].finishedTrigger);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null || entries[i].ability == null) continue;
                entries[i].ability.Started += OnAbilityStarted;
                entries[i].ability.Finished += OnAbilityFinished;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null || entries[i].ability == null) continue;
                entries[i].ability.Started -= OnAbilityStarted;
                entries[i].ability.Finished -= OnAbilityFinished;
            }
        }

        private void OnAbilityStarted(Ability ability)
        {
            int index = IndexOf(ability);
            if (index < 0 || animator == null || !animator.isActiveAndEnabled) return;

            // Por si quedó pendiente de un uso anterior que se canceló.
            if (finishHashes[index] != 0) animator.ResetTrigger(finishHashes[index]);
            if (startHashes[index] != 0) animator.SetTrigger(startHashes[index]);
        }

        private void OnAbilityFinished(Ability ability)
        {
            int index = IndexOf(ability);
            if (index < 0 || animator == null || !animator.isActiveAndEnabled) return;

            if (startHashes[index] != 0) animator.ResetTrigger(startHashes[index]);
            if (finishHashes[index] != 0) animator.SetTrigger(finishHashes[index]);
        }

        private int IndexOf(Ability ability)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].ability == ability) return i;
            }
            return -1;
        }
    }
}
