using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.UI
{
    /// <summary>Enciende la etiqueta "POTENCIADO" del HUD mientras el héroe está potenciado (tarea 34).</summary>
    public class BuffStatusUI : MonoBehaviour
    {
        [SerializeField] private BuffReceiver receiver;
        [SerializeField] private GameObject buffTag;

        private void OnEnable()
        {
            if (receiver == null) return;
            receiver.BuffChanged += Apply;
            Apply(receiver.IsBuffed);
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.BuffChanged -= Apply;
        }

        private void Apply(bool buffed)
        {
            if (buffTag != null) buffTag.SetActive(buffed);
        }
    }
}
