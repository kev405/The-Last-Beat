using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.Events;

namespace TheLastBeat.Level
{
    /// <summary>
    /// Zona invisible que dispara algo cuando el caballero entra (tarea 20). Lo que dispara se
    /// conecta en el Inspector (On Player Enter), así sirve para cualquier paso del nivel:
    /// empezar las hordas, un diálogo, la unión del tanque (tarea 60).
    /// Necesita un collider marcado como Is Trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Tooltip("Si está marcado, se dispara una sola vez.")]
        [SerializeField] private bool once = true;
        [SerializeField] private UnityEvent onPlayerEnter;

        private bool fired;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired && once) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            fired = true;
            onPlayerEnter?.Invoke();
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
