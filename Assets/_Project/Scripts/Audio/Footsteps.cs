using UnityEngine;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// Pasos de un personaje (tarea 32): suena un paso cada cierta distancia recorrida. No depende de cómo se mueva
    /// (CharacterController, NavMeshAgent o lo que sea), solo de cuánto cambió su posición. Una embestida o un
    /// teletransporte van demasiado rápido para ser pasos y no suenan.
    /// Va en el objeto raíz del personaje.
    /// </summary>
    public class Footsteps : MonoBehaviour
    {
        [SerializeField] private SfxCue steps = new SfxCue { volume = 0.4f, pitchVariation = 0.08f, minInterval = 0.12f };
        [Tooltip("Metros entre un paso y el siguiente.")]
        [SerializeField, Min(0.2f)] private float stride = 1.5f;
        [Tooltip("Por debajo de esta velocidad (m/s) no suena: está quieto o lo están empujando despacio.")]
        [SerializeField, Min(0f)] private float minSpeed = 1f;
        [Tooltip("Por encima de esta velocidad (m/s) no son pasos: es una embestida o un teletransporte.")]
        [SerializeField, Min(1f)] private float maxSpeed = 12f;

        private Vector3 last;
        private float travelled;

        private void Awake() => steps.Preload();

        private void OnEnable()
        {
            last = transform.position;
            travelled = stride * 0.5f;   // el primer paso llega pronto al echar a andar
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - last;
            delta.y = 0f;
            last = position;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float distance = delta.magnitude;
            float speed = distance / dt;
            if (speed < minSpeed || speed > maxSpeed)
            {
                if (speed < minSpeed) travelled = stride * 0.5f;
                return;
            }

            travelled += distance;
            if (travelled < stride) return;
            travelled -= stride;
            SfxPlayer.Play(steps);
        }
    }
}
