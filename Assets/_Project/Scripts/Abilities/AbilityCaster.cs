using System;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using TheLastBeat.Player;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Lanza las habilidades de un héroe y lleva sus recargas (tarea 9).
    /// Ranura 0 = Ability1 (Q / clic derecho), ranura 1 = Ability2 (E).
    /// Reglas: no se lanza si está en recarga, si ya hay otra habilidad activa o si el
    /// héroe está muerto. Lanzar una habilidad corta el combo en curso.
    /// </summary>
    public class AbilityCaster : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController motor;
        [SerializeField] private KnightCombat combat;
        [SerializeField] private Health health;

        [Header("Habilidades")]
        [Tooltip("Arrastra aquí los componentes de habilidad del mismo objeto, en orden.")]
        [SerializeField] private Ability[] slots = new Ability[2];

        [Tooltip("Capas que golpean las habilidades. Si queda en Nothing, usa la capa Enemy.")]
        [SerializeField] private LayerMask targetMask;

        private float[] remaining;
        private float[] total;

        /// <summary>Multiplicador de daño de todas las habilidades (potenciación, mejoras).</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Multiplicador de recarga (0.8 = 20 % más rápida).</summary>
        public float CooldownMultiplier { get; set; } = 1f;

        public PlayerController Motor => motor;
        public LayerMask TargetMask => targetMask;
        public Team Team => health != null ? health.Team : Team.Heroes;
        public int SlotCount => slots.Length;

        public bool IsCasting
        {
            get
            {
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && slots[i].IsRunning) return true;
                return false;
            }
        }

        /// <summary>Se lanzó la habilidad de esa ranura.</summary>
        public event Action<int> AbilityUsed;

        /// <summary>Terminó la recarga de esa ranura.</summary>
        public event Action<int> CooldownReady;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (motor == null) motor = GetComponent<PlayerController>();
            if (combat == null) combat = GetComponent<KnightCombat>();
            if (health == null) health = GetComponent<Health>();
            if (targetMask.value == 0) targetMask = GameLayers.EnemyMask;

            remaining = new float[slots.Length];
            total = new float[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].Bind(this);
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.Ability1Pressed += UseSlot0;
                input.Ability2Pressed += UseSlot1;
            }
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Ability1Pressed -= UseSlot0;
                input.Ability2Pressed -= UseSlot1;
            }
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] <= 0f) continue;
                remaining[i] -= dt;
                if (remaining[i] <= 0f)
                {
                    remaining[i] = 0f;
                    CooldownReady?.Invoke(i);
                }
            }
        }

        public bool TryUse(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot] == null) return false;
            if (health != null && health.IsDead) return false;
            if (remaining[slot] > 0f || IsCasting) return false;

            Ability ability = slots[slot];
            if (!ability.CanStart()) return false;

            if (combat != null) combat.CancelAttack();
            ability.Begin();

            total[slot] = ability.Data.cooldown * CooldownMultiplier;
            remaining[slot] = total[slot];
            AbilityUsed?.Invoke(slot);
            return true;
        }

        /// <summary>Segundos que faltan para poder usar la ranura.</summary>
        public float GetCooldownRemaining(int slot) =>
            slot >= 0 && slot < remaining.Length ? remaining[slot] : 0f;

        /// <summary>Recarga de 1 (recién usada) a 0 (lista), para llenar el ícono del HUD.</summary>
        public float GetCooldown01(int slot) =>
            slot >= 0 && slot < remaining.Length && total[slot] > 0f ? remaining[slot] / total[slot] : 0f;

        public Ability GetAbility(int slot) => slot >= 0 && slot < slots.Length ? slots[slot] : null;

        private void UseSlot0() => TryUse(0);
        private void UseSlot1() => TryUse(1);

        private void OnDied(Health _, DamageInfo __)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].Cancel();
        }
    }
}
