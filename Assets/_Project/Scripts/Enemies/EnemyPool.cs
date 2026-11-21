using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TheLastBeat.Enemies
{
    /// <summary>
    /// Pool de demonios soldado (tarea 14). Crea todos los enemigos al cargar la escena
    /// (prewarm) y después solo los activa y desactiva: ningún Instantiate ni Destroy en combate.
    /// Al morir, cada soldado avisa con Released y vuelve aquí solo.
    /// </summary>
    public class EnemyPool : MonoBehaviour
    {
        [SerializeField] private EnemySoldier prefab;
        [Tooltip("Cuántos crea al empezar. Debe cubrir el máximo de enemigos vivos a la vez.")]
        [SerializeField, Min(0)] private int prewarm = 20;
        [Tooltip("Máximo que guarda inactivos; si se pasa, destruye los sobrantes.")]
        [SerializeField, Min(1)] private int maxSize = 60;

        private ObjectPool<EnemySoldier> pool;
        private int created;

        /// <summary>Enemigos que están en la escena ahora (vivos o hundiéndose).</summary>
        public int ActiveCount => pool != null ? pool.CountActive : 0;

        /// <summary>Total de enemigos creados desde el inicio. Si deja de subir, el pool está funcionando.</summary>
        public int CreatedCount => created;

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError($"[{nameof(EnemyPool)}] Falta asignar el prefab del soldado.", this);
                enabled = false;
                return;
            }

            pool = new ObjectPool<EnemySoldier>(
                createFunc: Create,
                actionOnGet: null,
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e => Destroy(e.gameObject),
                collectionCheck: false,
                defaultCapacity: prewarm,
                maxSize: maxSize);

            var warm = new List<EnemySoldier>(prewarm);
            for (int i = 0; i < prewarm; i++) warm.Add(pool.Get());
            foreach (EnemySoldier e in warm) pool.Release(e);
        }

        /// <summary>Saca un soldado del pool y lo coloca listo para pelear.</summary>
        public EnemySoldier Spawn(Vector3 position, Quaternion rotation)
        {
            if (pool == null) return null;
            EnemySoldier e = pool.Get();
            e.transform.SetPositionAndRotation(position, rotation);
            e.gameObject.SetActive(true);
            e.ResetForSpawn(position, rotation);
            return e;
        }

        private EnemySoldier Create()
        {
            EnemySoldier e = Instantiate(prefab, transform);
            created++;
            e.name = $"{prefab.name}_{created:00}";
            e.gameObject.SetActive(false);
            e.Released += OnReleased;
            return e;
        }

        private void OnReleased(EnemySoldier e) => pool.Release(e);
    }
}
