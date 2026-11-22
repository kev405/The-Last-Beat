using TheLastBeat.Combat;
using TMPro;
using UnityEngine;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Barra de vida del HUD (tarea 34). Se actualiza por evento (HealthChanged), no en cada
    /// frame. Tiene una "estela" clara que baja con retraso para que se lea cuánto daño entró.
    /// El relleno se hace cambiando el ancla derecha del rectángulo, así no necesita sprite.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform trail;
        [SerializeField] private TMP_Text valueText;
        [SerializeField, Min(0f)] private float trailDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float trailSpeed = 0.8f;

        private float shown = 1f;
        private float trailValue = 1f;
        private float trailTimer;

        public void SetTarget(Health h)
        {
            if (target != null) target.HealthChanged -= Refresh;
            target = h;
            if (isActiveAndEnabled && target != null)
            {
                target.HealthChanged += Refresh;
                Refresh(target);
            }
        }

        private void OnEnable()
        {
            if (target == null) return;
            target.HealthChanged += Refresh;
            SyncNow();
        }

        // Unity no garantiza que el Awake de Health (que llena la vida) corra antes que el
        // OnEnable de esta barra: están en objetos distintos. En Start ya corrieron todos los
        // Awake de la escena, así que aquí se vuelve a leer el valor real.
        private void Start()
        {
            SyncNow();
        }

        private void SyncNow()
        {
            if (target == null) return;
            Refresh(target);
            trailValue = shown;
            trailTimer = 0f;
            SetWidth(trail, trailValue);
        }

        private void OnDisable()
        {
            if (target != null) target.HealthChanged -= Refresh;
        }

        private void Refresh(Health h)
        {
            shown = h.Normalized;
            SetWidth(fill, shown);
            if (valueText != null) valueText.SetText("{0} / {1}", Mathf.Ceil(h.CurrentHealth), h.MaxHealth);

            if (shown < trailValue) trailTimer = trailDelay;
            else
            {
                trailValue = shown;
                SetWidth(trail, trailValue);
            }
        }

        private void Update()
        {
            if (trailValue <= shown) return; // solo trabaja mientras la estela está bajando
            if (trailTimer > 0f)
            {
                trailTimer -= Time.unscaledDeltaTime;
                return;
            }
            trailValue = Mathf.MoveTowards(trailValue, shown, trailSpeed * Time.unscaledDeltaTime);
            SetWidth(trail, trailValue);
        }

        private static void SetWidth(RectTransform rt, float normalized)
        {
            if (rt == null) return;
            rt.anchorMax = new Vector2(Mathf.Clamp01(normalized), rt.anchorMax.y);
        }
    }
}
