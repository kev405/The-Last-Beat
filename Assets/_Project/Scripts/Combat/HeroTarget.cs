using System.Collections.Generic;
using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Marca a un héroe como objetivo para los enemigos (tarea 13). Cada héroe se registra
    /// en una lista común al activarse, así los enemigos no tienen que buscarlo con Find.
    /// La prioridad hace que un enemigo cambie de objetivo si un héroe "valioso" está cerca:
    /// caballero 0, bardo 10 (tarea 16). Va en el mismo objeto que Health.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HeroTarget : MonoBehaviour
    {
        private static readonly List<HeroTarget> all = new List<HeroTarget>();

        [Tooltip("0 = normal. Mayor = los enemigos lo prefieren si está dentro de Priority Radius.")]
        [SerializeField, Min(0)] private int priority;
        [Tooltip("Distancia a la que un enemigo prefiere a este héroe sobre el más cercano.")]
        [SerializeField, Min(0f)] private float priorityRadius = 6f;

        private Health health;

        public static IReadOnlyList<HeroTarget> All => all;
        public Health Health => health;
        public int Priority => priority;
        public bool IsAlive => health != null && !health.IsDead && isActiveAndEnabled;

        private void Awake() => health = GetComponent<Health>();
        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() => all.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        /// <summary>
        /// El mejor objetivo para un enemigo en <paramref name="from"/>: un héroe con prioridad
        /// dentro de su radio, o si no hay, el héroe vivo más cercano. Null si no queda ninguno.
        /// </summary>
        public static HeroTarget FindBest(Vector3 from)
        {
            HeroTarget best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                HeroTarget h = all[i];
                if (h == null || !h.IsAlive) continue;

                Vector3 d = h.transform.position - from;
                d.y = 0f;
                float dist = d.magnitude;
                float score = dist;
                if (h.priority > 0 && dist <= h.priorityRadius) score -= 1000f * h.priority;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = h;
                }
            }
            return best;
        }

        /// <summary>Distancia horizontal al héroe vivo más cercano (para no aparecer encima del jugador).</summary>
        public static float NearestDistance(Vector3 from)
        {
            float best = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                HeroTarget h = all[i];
                if (h == null || !h.IsAlive) continue;
                Vector3 d = h.transform.position - from;
                d.y = 0f;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }
    }
}
