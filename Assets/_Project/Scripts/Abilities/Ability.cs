using System;
using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Base de toda habilidad. El AbilityCaster decide cuándo se puede lanzar (recarga,
    /// otra habilidad activa) y llama a Begin(); la habilidad hace su trabajo en Update y
    /// llama a End() al terminar. Cada subclase implementa OnBegin y, si bloqueó algo, lo
    /// libera en OnEnd, que se ejecuta tanto al terminar normal como al cancelarse.
    /// </summary>
    public abstract class Ability : MonoBehaviour
    {
        [SerializeField] private AbilityData data;

        public AbilityData Data => data;
        public bool IsRunning { get; private set; }

        protected AbilityCaster Caster { get; private set; }
        protected HitDetector Hits { get; } = new HitDetector(32);

        public event Action<Ability> Started;
        public event Action<Ability> Finished;

        internal void Bind(AbilityCaster caster) => Caster = caster;

        /// <summary>Comprobaciones propias de la habilidad antes de lanzarse.</summary>
        public virtual bool CanStart() => !IsRunning && data != null && Caster != null;

        internal void Begin()
        {
            IsRunning = true;
            Hits.BeginAttack();
            Started?.Invoke(this);
            OnBegin();
        }

        /// <summary>Interrumpe la habilidad (por ejemplo, si el personaje muere).</summary>
        public void Cancel()
        {
            if (!IsRunning) return;
            OnCancel();
            End();
        }

        protected void End()
        {
            if (!IsRunning) return;
            IsRunning = false;
            OnEnd();
            Finished?.Invoke(this);
        }

        protected abstract void OnBegin();
        protected virtual void OnCancel() { }
        protected virtual void OnEnd() { }

        /// <summary>Arma el DamageInfo con el multiplicador de daño, el bando y la regla de interrupción.</summary>
        protected DamageInfo MakeDamage(float amount, Vector3 point, Vector3 direction, float knockback, bool heavy)
        {
            return DamageInfo.Create(amount * Caster.DamageMultiplier, Caster.gameObject, Caster.Team,
                                     point, direction, knockback, data.interrupts, heavy);
        }

        protected virtual void OnDisable() => Cancel();
    }
}
