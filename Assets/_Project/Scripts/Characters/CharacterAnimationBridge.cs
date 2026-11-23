using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.Characters
{
    /// <summary>
    /// Conecta la vida con el Animator (tarea 28): al recibir daño dispara el trigger "Hit"
    /// y al morir el trigger "Die". Sirve para héroes y enemigos: va en el mismo objeto que Health.
    /// Si el Animator no tiene alguno de los parámetros, simplemente no lo usa.
    ///
    /// Antes de disparar "Hit" escribe el entero "HitType" según de dónde vino el golpe
    /// (DamageInfo.Direction): 0 = de frente o por detrás, 1 = desde la derecha, 2 = desde la izquierda.
    /// No reproduce la reacción mientras el Animator está en un estado con la etiqueta "Action"
    /// (ataques y habilidades), para que una horda no corte cada golpe del jugador.
    ///
    /// Compatible con pooling: cuando Health.ResetHealth() revive al personaje, reinicia el Animator. Los parámetros
    /// del Animator se leen en Awake y, si ahí todavía no estaba listo (un enemigo recién creado por el pool),
    /// se vuelven a leer la primera vez que hacen falta.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CharacterAnimationBridge : MonoBehaviour
    {
        [Tooltip("Si se deja vacío, usa el primer Animator de los hijos.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string hitTypeParameter = "HitType";
        [SerializeField] private string dieTrigger = "Die";
        [Tooltip("Mientras la capa base esté en un estado con esta etiqueta no se reproduce la reacción al golpe. Vacío = siempre se reproduce.")]
        [SerializeField] private string skipHitWhileTag = "Action";
        [Tooltip("Tiempo mínimo entre dos reacciones al golpe, para que una ráfaga no la reinicie sin parar.")]
        [SerializeField, Min(0f)] private float hitCooldown = 0.6f;

        private Health health;
        private int hitHash;
        private int hitTypeHash;
        private int dieHash;
        private bool hasHit;
        private bool hasHitType;
        private bool hasDie;
        private bool parametersRead;
        private bool wasDead;
        private float lastHitTime = -999f;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            hitHash = Animator.StringToHash(hitTrigger);
            hitTypeHash = Animator.StringToHash(hitTypeParameter);
            dieHash = Animator.StringToHash(dieTrigger);
            ReadParameters();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            health.HealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            health.HealthChanged -= OnHealthChanged;
        }

        private void OnDamaged(Health h, DamageInfo info)
        {
            ReadParameters();
            if (!hasHit || animator == null || !animator.isActiveAndEnabled) return;
            // El golpe mortal no reproduce "Hit": pasa directo a "Die".
            if (h.CurrentHealth <= 0f) return;
            if (Time.time - lastHitTime < hitCooldown) return;
            if (IsBusy()) return;

            lastHitTime = Time.time;
            if (hasHitType) animator.SetInteger(hitTypeHash, HitTypeFor(info.Direction));
            animator.SetTrigger(hitHash);
        }

        private void OnDied(Health h, DamageInfo info)
        {
            wasDead = true;
            if (animator == null) return;
            ReadParameters();
            if (hasHit) animator.ResetTrigger(hitHash);
            if (hasDie) animator.SetTrigger(dieHash);
        }

        private void OnHealthChanged(Health h)
        {
            // Revivió (ResetHealth al salir del pool): vuelve al estado inicial del Animator.
            if (!wasDead || h.IsDead) return;
            wasDead = false;
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.Rebind();
            animator.Update(0f);
        }

        /// <summary>true si la capa base está en un ataque o habilidad, o entrando a uno.</summary>
        private bool IsBusy()
        {
            if (string.IsNullOrEmpty(skipHitWhileTag)) return false;
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag(skipHitWhileTag)) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(skipHitWhileTag);
        }

        /// <summary>
        /// Direction es hacia dónde viaja el golpe. Visto desde el personaje: si viaja hacia su izquierda,
        /// el golpe vino desde la derecha (1); si viaja hacia su derecha, vino desde la izquierda (2).
        /// </summary>
        private int HitTypeFor(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return 0;

            Vector3 local = transform.InverseTransformDirection(worldDirection.normalized);
            if (local.x < -0.5f) return 1;
            if (local.x > 0.5f) return 2;
            return 0;
        }

        /// <summary>
        /// Mira qué parámetros tiene el Animator. Si todavía no devuelve ninguno (el Animator de un objeto recién
        /// creado o inactivo aún no está listo), no da la lectura por hecha y se repite en el siguiente aviso.
        /// </summary>
        private void ReadParameters()
        {
            if (parametersRead || animator == null || animator.runtimeAnimatorController == null) return;

            AnimatorControllerParameter[] parameters = animator.parameters;
            if (parameters.Length == 0) return;

            parametersRead = true;
            hasHit = HasParameter(parameters, hitHash, AnimatorControllerParameterType.Trigger);
            hasHitType = HasParameter(parameters, hitTypeHash, AnimatorControllerParameterType.Int);
            hasDie = HasParameter(parameters, dieHash, AnimatorControllerParameterType.Trigger);
        }

        private static bool HasParameter(AnimatorControllerParameter[] parameters, int hash, AnimatorControllerParameterType type)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type) return true;
            }
            return false;
        }
    }
}
