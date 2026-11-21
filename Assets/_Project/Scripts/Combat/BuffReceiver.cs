using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Marca a un héroe como capaz de recibir potenciaciones (tarea 17). Lleva la cuenta de
    /// quién lo está potenciando: si dos fuentes lo potencian y una se apaga, sigue potenciado.
    /// Cada héroe se registra en una lista común, así la canción no tiene que buscarlos.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuffReceiver : MonoBehaviour
    {
        private static readonly List<BuffReceiver> all = new List<BuffReceiver>();

        private readonly HashSet<UnityEngine.Object> sources = new HashSet<UnityEngine.Object>();
        private Health health;

        public static IReadOnlyList<BuffReceiver> All => all;

        public bool IsBuffed => sources.Count > 0;
        public bool IsAlive => health == null || !health.IsDead;

        /// <summary>Cambió el estado: true = empezó a estar potenciado, false = dejó de estarlo.</summary>
        public event Action<bool> BuffChanged;

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            ClearAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        /// <summary>Activa o quita la potenciación que viene de <paramref name="source"/>.</summary>
        public void SetBuff(UnityEngine.Object source, bool active)
        {
            bool before = IsBuffed;
            if (active) sources.Add(source);
            else sources.Remove(source);
            if (before != IsBuffed) BuffChanged?.Invoke(IsBuffed);
        }

        public void ClearAll()
        {
            bool before = IsBuffed;
            sources.Clear();
            if (before) BuffChanged?.Invoke(false);
        }
    }
}
