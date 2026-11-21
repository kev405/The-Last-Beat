using System;
using TheLastBeat.Combat;
using TheLastBeat.Feedback;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Esquiva automática del caballero potenciado (tarea 19). Es un IDamageFilter: Health le
    /// pregunta antes de aplicar cada golpe, y si la esquiva sale, el golpe se cancela entero
    /// (sin daño, destello ni sacudida) y aparece "¡Esquiva!" sobre el caballero.
    /// Solo funciona mientras la canción lo potencia.
    /// </summary>
    [RequireComponent(typeof(BuffReceiver), typeof(Health))]
    public class KnightAutoDodge : MonoBehaviour, IDamageFilter
    {
        [Tooltip("Probabilidad de esquivar un golpe estando potenciado (0.25 = 1 de cada 4).")]
        [SerializeField, Range(0f, 1f)] private float dodgeChance = 0.25f;
        [Tooltip("Tiempo mínimo entre dos esquivas, para que no esquive una ráfaga entera.")]
        [SerializeField, Min(0f)] private float minInterval = 0.4f;
        [SerializeField] private string label = "¡Esquiva!";
        [SerializeField] private Color labelColor = new Color(0.72f, 0.56f, 1f);

        private BuffReceiver receiver;
        private DamageNumberPool numbers;
        private float lastDodge = -10f;

        /// <summary>Esquivó un golpe (para sonido y efecto en las tareas 32 y 33).</summary>
        public event Action Dodged;

        private void Awake()
        {
            receiver = GetComponent<BuffReceiver>();
            numbers = FindFirstObjectByType<DamageNumberPool>();
        }

        public bool Filter(ref DamageInfo info)
        {
            if (!isActiveAndEnabled || !receiver.IsBuffed) return true;
            if (Time.time - lastDodge < minInterval) return true;
            if (UnityEngine.Random.value > dodgeChance) return true;

            lastDodge = Time.time;
            if (numbers != null) numbers.ShowLabel(transform.position + Vector3.up * 2.2f, label, labelColor, 0.9f);
            Dodged?.Invoke();
            return false;
        }
    }
}
