using System.Collections;
using TheLastBeat.Level;
using TMPro;
using UnityEngine;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Mensaje grande en el centro de la pantalla (tarea 34): aparece, se queda un momento y se
    /// desvanece. Escucha LevelFlow.Message. Usa tiempo real para no congelarse con la pausa
    /// de impacto ni con la derrota.
    /// </summary>
    public class BannerUI : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField, Min(0f)] private float fadeIn = 0.2f;
        [SerializeField, Min(0f)] private float hold = 2f;
        [SerializeField, Min(0f)] private float fadeOut = 0.5f;

        private Coroutine running;

        private void OnEnable()
        {
            if (group != null) group.alpha = 0f;
            if (flow != null) flow.Message += Show;
        }

        private void OnDisable()
        {
            if (flow != null) flow.Message -= Show;
        }

        public void Show(string message)
        {
            if (text == null || group == null) return;
            text.SetText(message);
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            yield return Fade(group.alpha, 1f, fadeIn);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeOut);
            running = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, duration > 0f ? t / duration : 1f);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
