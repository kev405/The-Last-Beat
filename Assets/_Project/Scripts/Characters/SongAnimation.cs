using TheLastBeat.Heroes;
using UnityEngine;

namespace TheLastBeat.Characters
{
    /// <summary>
    /// Hace que la bardo toque el laúd mientras la canción suena (tarea 28). Escucha SongBuff.SongStateChanged
    /// y sube o baja el peso de la capa "Tocar" del Animator, que solo mueve torso, cabeza y brazos:
    /// así las piernas siguen con la animación de reposo o de correr y ella toca también mientras huye.
    /// Con la canción interrumpida o en silencio, la capa se apaga y los brazos vuelven a la animación base.
    /// Va en el mismo objeto que SongBuff.
    /// </summary>
    public class SongAnimation : MonoBehaviour
    {
        [Tooltip("Si se deja vacío, usa el SongBuff de este objeto.")]
        [SerializeField] private SongBuff song;
        [Tooltip("Si se deja vacío, usa el primer Animator de los hijos.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string layerName = "Tocar";
        [Tooltip("Segundos que tarda en levantar o bajar los brazos.")]
        [SerializeField, Min(0.01f)] private float blendTime = 0.15f;

        private int layer = -1;
        private float weight;
        private float target;

        private void Awake()
        {
            if (song == null) song = GetComponent<SongBuff>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null) layer = animator.GetLayerIndex(layerName);
        }

        private void OnEnable()
        {
            if (song == null) return;
            song.SongStateChanged += OnSongStateChanged;
            target = song.State == SongState.Singing ? 1f : 0f;
        }

        private void OnDisable()
        {
            if (song != null) song.SongStateChanged -= OnSongStateChanged;
        }

        private void OnSongStateChanged(SongState state)
        {
            target = state == SongState.Singing ? 1f : 0f;
        }

        private void Update()
        {
            if (layer < 0 || animator == null || Mathf.Approximately(weight, target)) return;

            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / blendTime);
            animator.SetLayerWeight(layer, weight);
        }
    }
}
