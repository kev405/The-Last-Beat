using TheLastBeat.Combat;
using Unity.Cinemachine;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Sacudida de cámara por golpe (tarea 12). Emite un impulso de Cinemachine que la cámara
    /// recibe con su extensión Cinemachine Impulse Listener. Golpes fuertes sacuden más,
    /// y cuando un enemigo golpea a un héroe también se sacude, para que el jugador lo note.
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class CameraShakeOnHit : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float lightForce = 0.15f;
        [SerializeField, Min(0f)] private float heavyForce = 0.45f;
        [Tooltip("Fuerza cuando un enemigo golpea a un héroe.")]
        [SerializeField, Min(0f)] private float heroDamagedForce = 0.3f;
        [Tooltip("Tiempo mínimo entre sacudidas, para que el Giro contra muchos no la vuelva un temblor.")]
        [SerializeField, Min(0f)] private float minInterval = 0.06f;

        private CinemachineImpulseSource source;
        private float lastTime = -1f;

        private void Awake() => source = GetComponent<CinemachineImpulseSource>();
        private void OnEnable() => CombatEvents.Hit += OnHit;
        private void OnDisable() => CombatEvents.Hit -= OnHit;

        private void OnHit(IDamageable target, DamageInfo info)
        {
            float now = Time.unscaledTime;
            bool heavy = info.IsHeavy;
            if (!heavy && now - lastTime < minInterval) return;
            lastTime = now;

            float force;
            if (info.SourceTeam == Team.Heroes) force = heavy ? heavyForce : lightForce;
            else force = heroDamagedForce;

            source.GenerateImpulseWithForce(force);
        }
    }
}
