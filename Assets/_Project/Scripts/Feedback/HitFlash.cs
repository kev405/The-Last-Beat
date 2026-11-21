using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Destello blanco al recibir daño (tarea 12). Sirve para héroes y enemigos.
    /// Cambia el color con un MaterialPropertyBlock, así no crea copias del material.
    /// Va en el mismo objeto que Health.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        [Tooltip("Lo que se pone blanco. Si se deja vacío, usa todos los Renderer activos de los hijos.")]
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0f)] private float duration = 0.08f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Health health;
        private MaterialPropertyBlock block;
        private Color[] baseColors;
        private float timer;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();

            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material m = renderers[i] != null ? renderers[i].sharedMaterial : null;
                baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        private void OnEnable() => health.Damaged += OnDamaged;

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            ResetFlash();
        }

        private void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) ResetFlash();
        }

        private void OnDamaged(Health h, DamageInfo info)
        {
            timer = duration;
            for (int i = 0; i < renderers.Length; i++) SetColor(i, flashColor);
        }

        /// <summary>Quita el destello de inmediato (al reaparecer desde el pool).</summary>
        public void ResetFlash()
        {
            timer = 0f;
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) SetColor(i, baseColors[i]);
        }

        private void SetColor(int i, Color c)
        {
            Renderer r = renderers[i];
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
        }
    }
}
