using System;
using TMPro;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Un número de daño flotante (tarea 12). Aparece con un pequeño salto de tamaño, sube,
    /// se desvanece y vuelve al pool. Siempre mira a la cámara. Va en un prefab con un
    /// componente TextMeshPro (el 3D, no el de UI).
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshPro text;
        [SerializeField, Min(0.1f)] private float lifetime = 0.8f;
        [SerializeField, Min(0f)] private float riseSpeed = 2.2f;
        [Tooltip("Desvío aleatorio horizontal para que varios números no se tapen.")]
        [SerializeField, Min(0f)] private float spread = 0.4f;
        [SerializeField] private AnimationCurve scaleOverLife = new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.12f, 1.25f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.9f));

        private float timer;
        private float baseScale = 1f;
        private Color color = Color.white;
        private Vector3 velocity;
        private Transform cameraTransform;
        private Action<DamageNumber> onFinished;

        private void Awake()
        {
            if (text == null) text = GetComponent<TextMeshPro>();
        }

        public void Show(Vector3 position, float amount, Color newColor, float scale, Action<DamageNumber> finished)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * spread;
            transform.position = position + new Vector3(offset.x, 0f, offset.y);
            text.SetText("{0}", Mathf.RoundToInt(amount)); // sin crear strings nuevos por golpe
            color = newColor;
            text.color = color;
            baseScale = scale;
            transform.localScale = Vector3.one * (baseScale * scaleOverLife.Evaluate(0f));
            velocity = Vector3.up * riseSpeed;
            timer = 0f;
            onFinished = finished;
            if (Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime; // se congela con la pausa de impacto, como el resto
            timer += dt;
            float k = Mathf.Clamp01(timer / lifetime);

            transform.position += velocity * dt;
            velocity = Vector3.Lerp(velocity, Vector3.zero, 3f * dt);
            transform.localScale = Vector3.one * (baseScale * scaleOverLife.Evaluate(k));

            float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            text.color = new Color(color.r, color.g, color.b, alpha);

            if (cameraTransform != null) transform.rotation = cameraTransform.rotation;

            if (timer >= lifetime)
            {
                Action<DamageNumber> done = onFinished;
                onFinished = null;
                done?.Invoke(this);
            }
        }
    }
}
