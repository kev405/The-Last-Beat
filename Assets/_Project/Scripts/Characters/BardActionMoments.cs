using System;
using UnityEngine;

namespace TheLastBeat.Characters
{
    /// <summary>Acciones animadas de la bardo: ataque básico y las dos habilidades del nivel.</summary>
    public enum BardAction
    {
        Note,
        BassDrop,
        DanceFloor
    }

    /// <summary>
    /// Avisa de los dos momentos que importan en cada acción de la bardo, leyéndolos del Animator (tarea 33):
    ///  - Started: la animación empieza (levanta la mano, sube el laúd, arranca el giro).
    ///  - Hit: el instante del efecto. En la Nota, cuando la mano cruza las cuerdas; en el Golpe de bajo,
    ///    cuando pisa; en la Pista de baile, cuando cae y rasguea hacia el piso.
    /// Así los efectos (y más adelante el daño, el empuje y la curación) quedan sincronizados con la animación sin
    /// repetir tiempos en cada script: basta suscribirse a estos eventos. Funciona con cualquier cosa que dispare
    /// los triggers "Note", "BassDrop" o "DanceFloor", sea el probador de teclado o las habilidades reales.
    /// Los momentos están en fracción del clip, así que siguen valiendo si se cambia la velocidad del estado.
    /// </summary>
    public class BardActionMoments : MonoBehaviour
    {
        [Tooltip("Si se deja vacío, usa el primer Animator de los hijos.")]
        [SerializeField] private Animator animator;
        [Tooltip("Capas del Animator donde viven los estados Note, BassDrop y DanceFloor.")]
        [SerializeField] private string[] layerNames = { "Ataque", "Habilidades" };

        [Header("Instante del efecto (0 = inicio del clip, 1 = final)")]
        [SerializeField, Range(0f, 1f)] private float noteMoment = 0.43f;         // fotograma 7 de 15
        [SerializeField, Range(0f, 1f)] private float bassDropMoment = 0.39f;     // fotograma 15 de 37
        [SerializeField, Range(0f, 1f)] private float danceFloorMoment = 0.525f;  // fotograma 22 de 41

        /// <summary>La animación de la acción acaba de empezar.</summary>
        public event Action<BardAction> Started;
        /// <summary>La animación llegó al instante del efecto. Se lanza una sola vez por cada uso.</summary>
        public event Action<BardAction> Hit;

        private static readonly int NoteHash = Animator.StringToHash("Note");
        private static readonly int BassDropHash = Animator.StringToHash("BassDrop");
        private static readonly int DanceFloorHash = Animator.StringToHash("DanceFloor");

        private int[] layers = new int[0];
        private int[] playing = new int[0];      // estado de acción que suena en cada capa (0 = ninguno)
        private float[] lastTime = new float[0];
        private bool[] hitDone = new bool[0];

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            int count = layerNames != null ? layerNames.Length : 0;
            layers = new int[count];
            playing = new int[count];
            lastTime = new float[count];
            hitDone = new bool[count];
            for (int i = 0; i < count; i++) layers[i] = animator.GetLayerIndex(layerNames[i]);
        }

        private void OnDisable()
        {
            for (int i = 0; i < playing.Length; i++) playing[i] = 0;
        }

        // LateUpdate: el Animator ya avanzó este fotograma, así que el estado que se lee es el que se está viendo.
        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] < 0) continue;

                int hash;
                float time;
                if (!ReadAction(layers[i], out hash, out time))
                {
                    playing[i] = 0;
                    continue;
                }

                // Estado nuevo, o el mismo que volvió a empezar (su tiempo retrocede).
                if (hash != playing[i] || time < lastTime[i] - 0.001f)
                {
                    playing[i] = hash;
                    hitDone[i] = false;
                    if (Started != null) Started.Invoke(ToAction(hash));
                }
                lastTime[i] = time;

                if (!hitDone[i] && time >= MomentOf(hash))
                {
                    hitDone[i] = true;
                    if (Hit != null) Hit.Invoke(ToAction(hash));
                }
            }
        }

        /// <summary>
        /// Estado de acción de una capa y su avance (0 a 1). Durante la mezcla de entrada manda el estado que llega;
        /// durante la de salida, el que se va.
        /// </summary>
        private bool ReadAction(int layer, out int hash, out float time)
        {
            if (animator.IsInTransition(layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                if (IsAction(next.shortNameHash))
                {
                    hash = next.shortNameHash;
                    time = next.normalizedTime;
                    return true;
                }
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
            hash = current.shortNameHash;
            time = current.normalizedTime;
            return IsAction(hash);
        }

        private static bool IsAction(int hash)
        {
            return hash == NoteHash || hash == BassDropHash || hash == DanceFloorHash;
        }

        private static BardAction ToAction(int hash)
        {
            if (hash == BassDropHash) return BardAction.BassDrop;
            if (hash == DanceFloorHash) return BardAction.DanceFloor;
            return BardAction.Note;
        }

        private float MomentOf(int hash)
        {
            if (hash == BassDropHash) return bassDropMoment;
            if (hash == DanceFloorHash) return danceFloorMoment;
            return noteMoment;
        }
    }
}
