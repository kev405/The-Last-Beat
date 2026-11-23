using System.Collections.Generic;
using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Destello al recibir daño (tareas 12 y 33). Sirve para héroes y enemigos: durante unas centésimas todo el
    /// cuerpo se ve de un solo color (blanco en los soldados, rojo en los héroes).
    /// Usa un MaterialPropertyBlock que, mientras dura el destello, cambia la textura por una blanca y el color por
    /// el del destello. Cambiar solo el color no sirve en un modelo con textura, porque el color se multiplica por
    /// ella: el blanco no cambia nada y el rojo apenas la tiñe. No crea copias del material, y al terminar quita el
    /// bloque para que el material vuelva a ser el suyo.
    /// Va en el mismo objeto que Health.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        [Tooltip("Lo que destella. Si se deja vacío, o si lo asignado ya no se dibuja (las piezas de prueba ocultas), usa el modelo: las mallas que cuelgan del Animator.")]
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = Color.white;
        [Tooltip("Cuánto brilla el destello. Por encima de 1 compensa las sombras en los materiales con iluminación.")]
        [SerializeField, Min(0.1f)] private float brightness = 1.5f;
        [SerializeField, Min(0f)] private float duration = 0.08f;

        /// <summary>Los efectos que montan los menús del editor empiezan así: no son parte del cuerpo.</summary>
        private const string VfxPrefix = "VFX_";

        // Los dos nombres que usan los shaders para el color y la textura principal (URP y los antiguos).
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private Health health;
        private MaterialPropertyBlock block;
        private Renderer[] targets;
        private float timer;
        private bool flashing;

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            targets = FindTargets();
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
            if (flashing) return;   // ya está destellando: solo dura un poco más
            flashing = true;

            Color color = flashColor * brightness;
            color.a = 1f;
            block.Clear();
            block.SetTexture(BaseMapId, Texture2D.whiteTexture);
            block.SetTexture(MainTexId, Texture2D.whiteTexture);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);

            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].SetPropertyBlock(block);
        }

        /// <summary>Quita el destello de inmediato (al reaparecer desde el pool).</summary>
        public void ResetFlash()
        {
            timer = 0f;
            if (!flashing || targets == null) return;
            flashing = false;

            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].SetPropertyBlock(null);
        }

        /// <summary>
        /// Qué destella: lo asignado en el Inspector que de verdad se esté dibujando y, si no queda nada (el caballero
        /// y la bardo tenían asignadas las piezas de prueba, que ahora están ocultas), las mallas del modelo.
        /// </summary>
        private Renderer[] FindTargets()
        {
            List<Renderer> found = new List<Renderer>();
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (IsDrawn(renderers[i])) found.Add(renderers[i]);
            }
            if (found.Count > 0) return found.ToArray();

            Animator model = GetComponentInChildren<Animator>(true);
            Transform root = model != null ? model.transform : transform;
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                bool isMesh = r is SkinnedMeshRenderer || r is MeshRenderer;   // ni partículas ni estelas
                if (isMesh && IsDrawn(r) && !IsEffect(r.transform, root)) found.Add(r);
            }
            return found.ToArray();
        }

        private static bool IsDrawn(Renderer r) => r != null && r.enabled && r.gameObject.activeSelf;

        /// <summary>¿Ese objeto, o alguno de sus padres hasta la raíz, es un efecto montado por los menús?</summary>
        private static bool IsEffect(Transform t, Transform root)
        {
            while (t != null)
            {
                if (t.name.StartsWith(VfxPrefix, System.StringComparison.Ordinal)) return true;
                if (t == root) break;
                t = t.parent;
            }
            return false;
        }
    }
}
