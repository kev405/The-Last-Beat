using System.Collections.Generic;
using TheLastBeat.Combat;
using UnityEngine;
using UnityEngine.Pool;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Crea los números de daño de todos los golpes (tarea 12). Los reutiliza con un
    /// ObjectPool: nunca hace Instantiate ni Destroy durante el combate.
    /// Colores: blanco = golpe de héroe, dorado y más grande = golpe pesado,
    /// rojo = daño que recibe un héroe.
    /// </summary>
    public class DamageNumberPool : MonoBehaviour
    {
        [SerializeField] private DamageNumber prefab;
        [SerializeField, Min(0)] private int prewarm = 20;
        [SerializeField, Min(1)] private int maxSize = 60;

        [Header("Estilo")]
        [SerializeField] private Color heroHitColor = Color.white;
        [SerializeField] private Color heavyHitColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color heroDamagedColor = new Color(1f, 0.32f, 0.26f);
        [SerializeField, Min(0.1f)] private float normalScale = 1f;
        [SerializeField, Min(0.1f)] private float heavyScale = 1.5f;
        [Tooltip("Altura extra sobre el punto de impacto.")]
        [SerializeField] private float heightOffset = 0.8f;

        private ObjectPool<DamageNumber> pool;

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError($"[{nameof(DamageNumberPool)}] Falta asignar el prefab DamageNumber.", this);
                enabled = false;
                return;
            }

            pool = new ObjectPool<DamageNumber>(
                createFunc: Create,
                actionOnGet: n => n.gameObject.SetActive(true),
                actionOnRelease: n => n.gameObject.SetActive(false),
                actionOnDestroy: n => Destroy(n.gameObject),
                collectionCheck: false,
                defaultCapacity: prewarm,
                maxSize: maxSize);

            var warm = new List<DamageNumber>(prewarm);
            for (int i = 0; i < prewarm; i++) warm.Add(pool.Get());
            foreach (var n in warm) pool.Release(n);
        }

        private void OnEnable() => CombatEvents.Hit += OnHit;
        private void OnDisable() => CombatEvents.Hit -= OnHit;

        private DamageNumber Create()
        {
            DamageNumber n = Instantiate(prefab, transform);
            n.gameObject.SetActive(false);
            return n;
        }

        private void OnHit(IDamageable target, DamageInfo info)
        {
            if (pool == null) return;

            Color c;
            float s;
            if (info.SourceTeam == Team.Heroes)
            {
                c = info.IsHeavy ? heavyHitColor : heroHitColor;
                s = info.IsHeavy ? heavyScale : normalScale;
            }
            else
            {
                c = heroDamagedColor;
                s = normalScale;
            }

            DamageNumber n = pool.Get();
            n.Show(info.HitPoint + Vector3.up * heightOffset, info.Amount, c, s, Release);
        }

        /// <summary>Muestra un texto flotante con el mismo estilo (lo usa la esquiva automática, tarea 19).</summary>
        public void ShowLabel(Vector3 position, string label, Color color, float scale = 1f)
        {
            if (pool == null) return;
            DamageNumber n = pool.Get();
            n.ShowLabel(position, label, color, scale, Release);
        }

        private void Release(DamageNumber n) => pool.Release(n);
    }
}
