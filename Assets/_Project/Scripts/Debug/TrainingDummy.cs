using System.Collections;
using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Muñeco de práctica para probar el combate (tareas 8 a 12). Recibe daño, se pone
    /// blanco un instante, retrocede con el empuje, vuelve a su lugar y reaparece al morir.
    /// Necesita un Health en el mismo objeto (Team = Demons) y un collider en la capa Enemy.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0f)] private float flashTime = 0.08f;

        [Header("Empuje")]
        [Tooltip("Qué tanto lo mueve el empuje del golpe.")]
        [SerializeField, Min(0f)] private float knockbackScale = 1f;
        [SerializeField, Min(0f)] private float returnSpeed = 1.5f;

        [Header("Reaparición")]
        [SerializeField, Min(0f)] private float respawnDelay = 2f;

        [Header("Consola")]
        [SerializeField] private bool logHits = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Health health;
        private Collider[] colliders;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Color baseColor = Color.gray;
        private float flashTimer;
        private Vector3 home;
        private Vector3 push;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();

            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null &&
                bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
            {
                baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
            }
            home = transform.position;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetColor(baseColor);
            }

            if (push.sqrMagnitude > 0.0001f)
            {
                transform.position += push * dt;
                push = Vector3.MoveTowards(push, Vector3.zero, 18f * dt);
            }
            else if (!health.IsDead)
            {
                transform.position = Vector3.MoveTowards(transform.position, home, returnSpeed * dt);
            }
        }

        private void OnDamaged(Health h, DamageInfo info)
        {
            flashTimer = flashTime;
            SetColor(flashColor);
            push += info.Direction * (info.Knockback * knockbackScale);

            if (logHits)
            {
                Debug.Log($"[Dummy] {name}: −{info.Amount:0.#}  vida {h.CurrentHealth:0}/{h.MaxHealth:0}" +
                          $"{(info.IsHeavy ? "  [PESADO]" : "")}{(info.Knockback > 0f ? $"  [empuje {info.Knockback:0.#}]" : "")}");
            }
        }

        private void OnDied(Health h, DamageInfo info)
        {
            if (logHits) Debug.Log($"[Dummy] {name} cayó. Reaparece en {respawnDelay:0.#} s.");
            StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            push = Vector3.zero;
            transform.position = home;
            health.ResetHealth();
            SetColor(baseColor);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            foreach (var c in colliders) c.enabled = visible;
        }

        private void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
