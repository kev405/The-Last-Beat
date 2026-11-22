using TheLastBeat.Characters;
using TheLastBeat.Heroes;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Efecto del ataque básico de la bardo, la Nota (tarea 33). Escucha BardActionMoments:
    ///  - Started: enciende un resplandor rosa sobre el laúd, que crece mientras levanta la mano.
    ///  - Hit: el resplandor destella y sale una ráfaga de notas de 8 bits en línea recta, con una estela de píxeles.
    ///    Cada nota revienta en píxeles al llegar al final del alcance o al tocar a un enemigo o una pared.
    /// Solo dibuja: el daño lo hace BardBasicAttack. Si ese ataque está enlazado en el campo Attack, las notas
    /// salen con su evento Fired (mismo origen, dirección, alcance y velocidad que la nota que hace daño);
    /// si no, salen hacia donde mira la bardo en el momento de la animación.
    /// </summary>
    public class BardNoteVfx : MonoBehaviour
    {
        // El resplandor llega a su máximo en esta fracción de su vida (ver el degradado en BardVfxBuilder.CreateGlow).
        public const float GlowPeak = 0.45f;

        [SerializeField] private BardActionMoments moments;
        [Tooltip("Resplandor del laúd, hijo de LuteSocket.")]
        [SerializeField] private ParticleSystem glow;
        [Tooltip("Las notas. Emite a lo largo de su eje Z; este script lo orienta al disparar.")]
        [SerializeField] private ParticleSystem notes;
        [Tooltip("Opcional. El ataque que hace el daño: con él, las notas salen cuando dispara de verdad y hacia su objetivo.")]
        [SerializeField] private BardBasicAttack attack;

        [Header("Disparo")]
        [Tooltip("Metros que recorren las notas antes de reventar.")]
        [SerializeField, Min(0.5f)] private float range = 7f;
        [SerializeField, Min(1f)] private float speed = 14f;
        [Tooltip("Sin ataque enlazado: dispara hacia donde mira la bardo al llegar el momento de la animación.")]
        [SerializeField] private bool fireForward = true;

        [Header("Resplandor")]
        [Tooltip("Segundos entre que empieza la animación y sale la nota: el resplandor llega a su máximo justo ahí.")]
        [SerializeField, Min(0.05f)] private float chargeTime = 0.2f;

        public float Range
        {
            get { return range; }
            set { range = Mathf.Max(0.5f, value); }
        }

        private void OnEnable()
        {
            if (moments != null)
            {
                moments.Started += OnStarted;
                moments.Hit += OnHit;
            }
            if (attack != null) attack.Fired += Fire;
        }

        private void OnDisable()
        {
            if (moments != null)
            {
                moments.Started -= OnStarted;
                moments.Hit -= OnHit;
            }
            if (attack != null) attack.Fired -= Fire;
        }

        private void OnStarted(BardAction action)
        {
            if (action == BardAction.Note) EmitGlow(glow, chargeTime);
        }

        private void OnHit(BardAction action)
        {
            // Con ataque enlazado, quien dispara las notas es su evento Fired.
            if (action == BardAction.Note && fireForward && attack == null) Fire(transform.forward);
        }

        /// <summary>Lanza la ráfaga desde ese punto, con el alcance y la velocidad de la nota que hace daño.</summary>
        public void Fire(Vector3 origin, Vector3 direction, float noteRange, float noteSpeed)
        {
            if (notes == null) return;

            notes.transform.position = origin;
            range = Mathf.Max(0.5f, noteRange);
            speed = Mathf.Max(1f, noteSpeed);
            Fire(direction);
        }

        /// <summary>Lanza la ráfaga de notas en esa dirección (se aplana al suelo).</summary>
        public void Fire(Vector3 direction)
        {
            if (notes == null) return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            notes.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            // Vida = alcance / velocidad: la nota revienta justo al final del recorrido.
            ParticleSystem.MainModule main = notes.main;
            main.startSpeed = speed;
            main.startLifetime = range / speed;

            notes.Play(true);
        }

        /// <summary>
        /// Emite un resplandor que llega a su máximo a los "secondsToPeak" segundos. Lo usa también BardBassDropVfx,
        /// con el mismo sistema de partículas, mientras el laúd sube sobre la cabeza.
        /// </summary>
        public static void EmitGlow(ParticleSystem glow, float secondsToPeak)
        {
            if (glow == null) return;
            if (!glow.isPlaying) glow.Play();

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.startLifetime = Mathf.Max(0.1f, secondsToPeak / GlowPeak);
            glow.Emit(emit, 1);
        }
    }
}
