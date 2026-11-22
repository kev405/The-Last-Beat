using UnityEngine;

namespace TheLastBeat.Characters
{
    /// <summary>
    /// Mantiene el laúd y las manos de la bardo coordinados (tarea 28). Trabaja después del Animator (LateUpdate):
    ///
    ///  1. Laúd. En reposo cuelga del pecho (LuteSocket). Durante una acción cuyo estado tiene trayectoria en
    ///     BardLuteTracks (Note, BassDrop, DanceFloor), mueve LuteSocket fotograma a fotograma siguiendo esa
    ///     trayectoria: así el laúd puede subir sobre la cabeza en el Golpe de bajo y volver al pecho.
    ///  2. Manos. Dobla el codo y gira el hombro (método Reach) hasta que cada muñeca cae sobre su punto del laúd,
    ///     sin tocar la rotación de la mano ni los dedos, que vienen de la animación:
    ///       - tocando (capa "Tocar"): Grip_Left en el mástil y Grip_Right sobre las cuerdas, con el vaivén del rasgueo;
    ///       - en una acción: los puntos de la trayectoria, que son las muñecas tal como se animaron en Blender.
    ///
    /// Con la canción cortada y sin acción no hace nada (los brazos son de la animación de correr), y suelta las
    /// manos mientras suena una reacción al golpe.
    /// </summary>
    public class LuteHandIK : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("LuteSocket: el objeto del que cuelga el laúd, hijo del hueso del pecho.")]
        [SerializeField] private Transform socket;
        [Tooltip("Marca de la muñeca izquierda al tocar, hija de LuteSocket.")]
        [SerializeField] private Transform leftGrip;
        [Tooltip("Marca de la muñeca derecha al tocar (punto medio del rasgueo), hija de LuteSocket.")]
        [SerializeField] private Transform rightGrip;
        [Tooltip("Recorrido del rasgueo al tocar, en ejes locales de Grip_Right (metros hacia cada lado).")]
        [SerializeField] private Vector3 strumOffset = new Vector3(-0.0383f, 0.0025f, -0.0132f);
        [SerializeField] private string playLayerName = "Tocar";
        [Tooltip("Capas del Animator con acciones que mueven el laúd. Si dos están activas, manda la última.")]
        [SerializeField] private string[] actionLayerNames = { "Ataque", "Habilidades" };
        [SerializeField] private string reactionLayerName = "Reacciones";
        [SerializeField] private string reactionRestState = "Sin reaccion";
        [Tooltip("Segundos que tarda en soltar o retomar el laúd.")]
        [SerializeField, Min(0.01f)] private float releaseTime = 0.1f;
        [SerializeField, Range(0f, 1f)] private float maxWeight = 1f;

        private Transform leftUpper, leftLower, leftHand;
        private Transform rightUpper, rightLower, rightHand;
        private int playLayer = -1;
        private int reactionLayer = -1;
        private int[] actionLayers = new int[0];
        private int restHash;
        private bool ready;

        private float reactionFactor = 1f;
        private float actionFactor;

        // Pose con la que el menú colgó el laúd; las trayectorias se aplican sobre ella.
        private Vector3 restPosition;
        private Quaternion restRotation = Quaternion.identity;
        private Vector3 restScale = Vector3.one;
        private bool socketMoved;

        // Últimas muñecas leídas de una trayectoria, en ejes locales de LuteSocket.
        private Vector3 trackLeft;
        private Vector3 trackRight;
        private bool hasTrackSample;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();

            if (socket != null)
            {
                restPosition = socket.localPosition;
                restRotation = socket.localRotation;
                restScale = socket.localScale;
            }

            if (animator == null || animator.runtimeAnimatorController == null || !animator.isHuman) return;

            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            playLayer = animator.GetLayerIndex(playLayerName);
            reactionLayer = animator.GetLayerIndex(reactionLayerName);
            restHash = Animator.StringToHash(reactionRestState);

            actionLayers = new int[actionLayerNames != null ? actionLayerNames.Length : 0];
            for (int i = 0; i < actionLayers.Length; i++) actionLayers[i] = animator.GetLayerIndex(actionLayerNames[i]);

            ready = true;
        }

        private void OnDisable()
        {
            RestoreSocket();
            actionFactor = 0f;
            hasTrackSample = false;
        }

        private void LateUpdate()
        {
            if (!ready || animator == null || !animator.isActiveAndEnabled) return;

            float step = Time.deltaTime / releaseTime;

            // Durante una reacción al golpe los brazos son de esa animación: se sueltan las manos.
            bool reacting = false;
            if (reactionLayer >= 0)
            {
                reacting = animator.IsInTransition(reactionLayer) ||
                           animator.GetCurrentAnimatorStateInfo(reactionLayer).shortNameHash != restHash;
            }
            reactionFactor = Mathf.MoveTowards(reactionFactor, reacting ? 0f : 1f, step);

            // ¿Hay una acción con trayectoria sonando?
            float[] track = null;
            float trackTime = 0f;
            if (socket != null)
            {
                for (int i = 0; i < actionLayers.Length; i++)
                {
                    if (actionLayers[i] < 0) continue;

                    float[] found;
                    float time;
                    if (FindTrack(actionLayers[i], out found, out time))
                    {
                        track = found;
                        trackTime = time;
                    }
                }
            }
            bool acting = track != null;
            actionFactor = Mathf.MoveTowards(actionFactor, acting ? 1f : 0f, step);

            // 1. Laúd.
            if (acting) Sample(track, trackTime);
            else RestoreSocket();

            // 2. Manos.
            float playWeight = playLayer >= 0 ? animator.GetLayerWeight(playLayer) : 0f;
            float trackBlend = hasTrackSample ? actionFactor : 0f;
            if (trackBlend <= 0f) hasTrackSample = false;

            float leftWeight = Mathf.Max(playWeight, trackBlend) * reactionFactor * maxWeight;
            float rightWeight = Mathf.Lerp(playWeight, 1f, trackBlend) * reactionFactor * maxWeight;

            if (leftWeight > 0.001f && (leftGrip != null || trackBlend > 0f))
            {
                Vector3 trackTarget = trackBlend > 0f ? socket.TransformPoint(trackLeft) : Vector3.zero;
                Vector3 target = leftGrip != null ? leftGrip.position : trackTarget;
                if (trackBlend > 0f) target = Vector3.Lerp(target, trackTarget, trackBlend);
                Reach(leftUpper, leftLower, leftHand, target, leftWeight);
            }

            if (rightWeight > 0.001f && (rightGrip != null || trackBlend > 0f))
            {
                Vector3 trackTarget = trackBlend > 0f ? socket.TransformPoint(trackRight) : Vector3.zero;
                Vector3 target = trackTarget;
                if (rightGrip != null)
                {
                    // El clip "Play" hace un rasgueo completo por vuelta: abajo en el primer cuarto, arriba en el tercero.
                    float phase = playLayer >= 0 ? animator.GetCurrentAnimatorStateInfo(playLayer).normalizedTime : 0f;
                    target = rightGrip.TransformPoint(strumOffset * Mathf.Sin(phase * 2f * Mathf.PI));
                }
                if (trackBlend > 0f) target = Vector3.Lerp(target, trackTarget, trackBlend);
                Reach(rightUpper, rightLower, rightHand, target, rightWeight);
            }
        }

        /// <summary>
        /// Busca en una capa el estado con trayectoria. Durante la transición de entrada manda el estado que llega;
        /// durante la de salida, el que se va, para que la trayectoria se reproduzca hasta su último fotograma.
        /// </summary>
        private bool FindTrack(int layer, out float[] track, out float time)
        {
            if (animator.IsInTransition(layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
                track = BardLuteTracks.Find(next.shortNameHash);
                if (track != null)
                {
                    time = next.normalizedTime;
                    return true;
                }
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
            track = BardLuteTracks.Find(current.shortNameHash);
            time = current.normalizedTime;
            return track != null;
        }

        /// <summary>Coloca LuteSocket según la trayectoria en el instante "time" (0 = inicio del estado, 1 = final).</summary>
        private void Sample(float[] track, float time)
        {
            int stride = BardLuteTracks.Stride;
            int frames = track.Length / stride;
            if (frames < 1) return;

            float cursor = Mathf.Clamp01(time) * (frames - 1);
            int a = Mathf.Min((int)cursor, frames - 1);
            int b = Mathf.Min(a + 1, frames - 1);
            float t = cursor - a;
            int i = a * stride;
            int j = b * stride;

            Vector3 position = Vector3.Lerp(new Vector3(track[i], track[i + 1], track[i + 2]),
                                            new Vector3(track[j], track[j + 1], track[j + 2]), t);
            Quaternion rotation = Quaternion.Slerp(new Quaternion(track[i + 3], track[i + 4], track[i + 5], track[i + 6]),
                                                   new Quaternion(track[j + 3], track[j + 4], track[j + 5], track[j + 6]), t);
            trackLeft = Vector3.Lerp(new Vector3(track[i + 7], track[i + 8], track[i + 9]),
                                     new Vector3(track[j + 7], track[j + 8], track[j + 9]), t);
            trackRight = Vector3.Lerp(new Vector3(track[i + 10], track[i + 11], track[i + 12]),
                                      new Vector3(track[j + 10], track[j + 11], track[j + 12]), t);
            hasTrackSample = true;

            socket.localPosition = restPosition + restRotation * Vector3.Scale(restScale, position);
            socket.localRotation = restRotation * rotation;
            socketMoved = true;
        }

        private void RestoreSocket()
        {
            if (!socketMoved || socket == null) return;
            socket.localPosition = restPosition;
            socket.localRotation = restRotation;
            socketMoved = false;
        }

        /// <summary>
        /// IK de dos huesos: lleva la muñeca a "target" cambiando solo el ángulo del codo y la dirección del brazo.
        /// Conserva el plano en el que ya venía doblado el codo y la rotación de la mano que dejó la animación.
        /// </summary>
        private static void Reach(Transform upper, Transform lower, Transform hand, Vector3 target, float weight)
        {
            if (upper == null || lower == null || hand == null) return;

            Quaternion handRotation = hand.rotation;
            Vector3 goal = Vector3.Lerp(hand.position, target, weight);

            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, hand.position);
            if (upperLength < 0.0001f || lowerLength < 0.0001f) return;

            float distance = Vector3.Distance(upper.position, goal);
            distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);

            // 1. Codo: el ángulo interior que deja la muñeca a esa distancia del hombro (ley del coseno).
            Vector3 toUpper = upper.position - lower.position;
            Vector3 toHand = hand.position - lower.position;
            Vector3 axis = Vector3.Cross(toUpper, toHand);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(toUpper, Vector3.up);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.right;
            axis.Normalize();

            float current = Vector3.Angle(toUpper, toHand);
            float cosine = (upperLength * upperLength + lowerLength * lowerLength - distance * distance) / (2f * upperLength * lowerLength);
            float wanted = Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f)) * Mathf.Rad2Deg;
            lower.rotation = Quaternion.AngleAxis(wanted - current, axis) * lower.rotation;

            // 2. Hombro: gira el brazo entero hasta que la muñeca apunta a la meta.
            Vector3 fromDirection = hand.position - upper.position;
            Vector3 toDirection = goal - upper.position;
            if (fromDirection.sqrMagnitude > 1e-8f && toDirection.sqrMagnitude > 1e-8f)
                upper.rotation = Quaternion.FromToRotation(fromDirection, toDirection) * upper.rotation;

            hand.rotation = handRotation;
        }
    }
}
