using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Vida de héroes y enemigos. Implementa IDamageable y avisa por eventos
    /// para que animación, audio, VFX y UI reaccionen sin acoplarse entre sí.
    /// Compatible con pooling: ResetHealth() deja la entidad como nueva.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private Team team = Team.Neutral;

        [Tooltip("Mientras sea true no recibe daño (esquiva, cinemáticas, aparición).")]
        [SerializeField] private bool invulnerable;

        private float currentHealth;
        private bool isDead;
        private readonly List<IDamageFilter> filters = new List<IDamageFilter>(4);

        /// <summary>Se dispara cada vez que se aplica daño (después de los filtros).</summary>
        public event Action<Health, DamageInfo> Damaged;

        /// <summary>Se dispara una sola vez, al llegar a 0.</summary>
        public event Action<Health, DamageInfo> Died;

        /// <summary>Se dispara con cualquier cambio de vida: daño, curación o reinicio.</summary>
        public event Action<Health> HealthChanged;

        public Team Team => team;
        public bool IsDead => isDead;
        public Transform Transform => transform;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float Normalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        public bool Invulnerable
        {
            get => invulnerable;
            set => invulnerable = value;
        }

        private void Awake()
        {
            currentHealth = maxHealth;
            GetComponents(filters);
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (isDead || invulnerable) return false;
            if (!CanBeHurtBy(info.SourceTeam)) return false;

            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                if (filter is Behaviour b && !b.isActiveAndEnabled) continue;
                if (!filter.Filter(ref info)) return false;
            }

            if (info.Amount <= 0f) return false;

            currentHealth = Mathf.Max(0f, currentHealth - info.Amount);
            Damaged?.Invoke(this, info);
            HealthChanged?.Invoke(this);

            if (currentHealth <= 0f)
            {
                isDead = true;
                Died?.Invoke(this, info);
            }

            return true;
        }

        /// <summary>
        /// Cura sin pasar del máximo. No revive a una entidad muerta. Devuelve cuánta vida recuperó de verdad
        /// (0 si ya estaba llena), para que quien cura pueda avisar con CombatEvents.RaiseHeal.
        /// </summary>
        public float Heal(float amount)
        {
            if (isDead || amount <= 0f) return 0f;
            float previous = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            float applied = currentHealth - previous;
            if (applied > 0f && !Mathf.Approximately(previous, currentHealth)) HealthChanged?.Invoke(this);
            return applied;
        }

        /// <summary>Deja la vida al máximo y revive. Usado al sacar un enemigo del pool.</summary>
        public void ResetHealth()
        {
            isDead = false;
            currentHealth = maxHealth;
            HealthChanged?.Invoke(this);
        }

        /// <summary>Cambia la vida máxima (mejoras, dificultad). Opcionalmente rellena la vida.</summary>
        public void SetMaxHealth(float value, bool refill)
        {
            maxHealth = Mathf.Max(1f, value);
            currentHealth = refill ? maxHealth : Mathf.Min(currentHealth, maxHealth);
            HealthChanged?.Invoke(this);
        }

        /// <summary>Cambia el bando en tiempo de ejecución (por ejemplo, el demonio al unirse al grupo).</summary>
        public void SetTeam(Team newTeam) => team = newTeam;

        private bool CanBeHurtBy(Team attacker)
        {
            if (attacker == Team.Neutral || team == Team.Neutral) return true;
            return attacker != team;
        }

        private void OnValidate()
        {
            if (maxHealth < 1f) maxHealth = 1f;
        }
    }
}
