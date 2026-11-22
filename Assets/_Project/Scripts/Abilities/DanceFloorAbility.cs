using System;
using TheLastBeat.Characters;
using TheLastBeat.Combat;
using TheLastBeat.Heroes;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Pista de baile de la bardo (habilidad de apoyo): gira, cae y abre en el suelo una pista que cura poco a
    /// poco a los héroes que estén dentro. La pista se queda donde se abrió aunque la bardo se aleje, y sigue
    /// curando hasta que se acaba su tiempo aunque la habilidad ya haya terminado de lanzarse.
    ///
    /// La pista se abre en el instante que avisa BardActionMoments leyendo la animación; si ese aviso no llegara,
    /// se abre igual un poco después de Hit Delay. Mientras lanza, la bardo no se mueve.
    /// Cada curación avisa por CombatEvents.RaiseHeal (números verdes). El efecto visual escucha Opened.
    /// Necesita un asset DanceFloorAbilityData en el campo Data.
    /// </summary>
    public class DanceFloorAbility : Ability
    {
        // Margen sobre Hit Delay antes de abrir la pista sin esperar más al aviso de la animación.
        private const float MomentGrace = 0.3f;

        [Tooltip("Si se deja vacío, usa el BardActionMoments de este objeto.")]
        [SerializeField] private BardActionMoments moments;

        private BardCompanion companion;
        private float timer;
        private bool opened;
        private bool lockHeld;

        // La pista abierta: se guardan sus valores al abrirla para que no cambien a mitad.
        private Vector3 floorCenter;
        private float floorRadius;
        private float floorRemaining;
        private float tickTimer;
        private float tickInterval;
        private float healPerTick;
        private bool healsCaster;

        private DanceFloorAbilityData Floor => Data as DanceFloorAbilityData;

        /// <summary>true mientras hay una pista en el suelo.</summary>
        public bool IsFloorActive => floorRemaining > 0f;
        public Vector3 FloorCenter => floorCenter;
        public float FloorRadius => floorRadius;

        /// <summary>Se abrió la pista: centro en el suelo, radio real y segundos que va a durar.</summary>
        public event Action<Vector3, float, float> Opened;

        /// <summary>La pista se acabó.</summary>
        public event Action Closed;

        private void Awake()
        {
            companion = GetComponent<BardCompanion>();
            if (moments == null) moments = GetComponent<BardActionMoments>();
        }

        private void OnEnable()
        {
            if (moments != null) moments.Hit += OnMoment;
        }

        protected override void OnDisable()
        {
            if (moments != null) moments.Hit -= OnMoment;
            base.OnDisable();
        }

        public override bool CanStart()
        {
            if (Floor == null)
            {
                Debug.LogError($"[{nameof(DanceFloorAbility)}] El campo Data necesita un asset de tipo Pista de baile (DanceFloorAbilityData).", this);
                return false;
            }
            return base.CanStart();
        }

        protected override void OnBegin()
        {
            timer = 0f;
            opened = false;
            if (companion != null && !lockHeld)
            {
                companion.AddMoveLock();
                lockHeld = true;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (IsRunning)
            {
                timer += dt;
                if (!opened && timer >= Floor.hitDelay + MomentGrace) Open();
                if (timer >= Floor.castTime) End();
            }

            if (floorRemaining > 0f) TickFloor(dt);
        }

        private void OnMoment(BardAction action)
        {
            if (action == BardAction.DanceFloor && IsRunning && !opened) Open();
        }

        private void Open()
        {
            opened = true;

            DanceFloorAbilityData data = Floor;
            floorCenter = transform.position;
            floorRadius = data.radius * Caster.AreaMultiplier;
            floorRemaining = data.floorDuration;
            tickInterval = Mathf.Max(0.1f, data.tickInterval);
            tickTimer = tickInterval;   // la primera curación llega un intervalo después de abrirse
            healPerTick = data.healPerSecond * tickInterval;
            healsCaster = data.healsCaster;

            if (Opened != null) Opened.Invoke(floorCenter, floorRadius, floorRemaining);
        }

        private void TickFloor(float dt)
        {
            floorRemaining -= dt;
            tickTimer -= dt;

            while (tickTimer <= 0f)
            {
                tickTimer += tickInterval;
                HealInside();
            }

            if (floorRemaining <= 0f)
            {
                // El último tic cae justo al cerrarse: que un error de redondeo no se lo coma.
                if (tickTimer < tickInterval * 0.05f) HealInside();

                floorRemaining = 0f;
                if (Closed != null) Closed.Invoke();
            }
        }

        /// <summary>Cura a cada héroe vivo dentro de la pista (HeroTarget.All) y avisa de lo que curó de verdad.</summary>
        private void HealInside()
        {
            if (healPerTick <= 0f) return;

            float r2 = floorRadius * floorRadius;
            var heroes = HeroTarget.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroTarget hero = heroes[i];
                if (hero == null || !hero.IsAlive) continue;
                if (!healsCaster && hero.gameObject == gameObject) continue;

                Vector3 d = hero.transform.position - floorCenter;
                d.y = 0f;
                if (d.sqrMagnitude > r2) continue;

                float healed = hero.Health.Heal(healPerTick);
                if (healed > 0f) CombatEvents.RaiseHeal(hero.Health, healed);
            }
        }

        protected override void OnEnd()
        {
            if (lockHeld)
            {
                if (companion != null) companion.RemoveMoveLock();
                lockHeld = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            DanceFloorAbilityData d = Data as DanceFloorAbilityData;
            if (d == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.8f);
            Gizmos.DrawWireSphere(Application.isPlaying && IsFloorActive ? floorCenter : transform.position, d.radius);
        }
    }
}
