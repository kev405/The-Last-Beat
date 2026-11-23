using TheLastBeat.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Sonido de un botón (tarea 32): uno al pasar el cursor por encima o al llegarle la selección con el mando o
    /// el teclado, y otro al pulsarlo. Va en el mismo objeto que el Button. Como SfxPlayer sobrevive al cambio de
    /// escena, el clic se oye aunque el botón cargue otra escena.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private SfxCue hover = new SfxCue { pitchVariation = 0f, minInterval = 0.04f };
        [SerializeField] private SfxCue click = new SfxCue { pitchVariation = 0f, minInterval = 0.04f };

        private Button button;
        private float enabledAt;

        private void Awake()
        {
            button = GetComponent<Button>();
            hover.Preload();
            click.Preload();
        }

        private void OnEnable()
        {
            enabledAt = Time.unscaledTime;
            button.onClick.AddListener(OnClick);
        }

        private void OnDisable() => button.onClick.RemoveListener(OnClick);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button.interactable) SfxPlayer.Play(hover);
        }

        public void OnSelect(BaseEventData eventData)
        {
            // Al abrirse una pantalla, el primer botón queda seleccionado solo: eso no debe sonar.
            if (Time.unscaledTime - enabledAt > 0.2f) SfxPlayer.Play(hover);
        }

        private void OnClick() => SfxPlayer.Play(click);
    }
}
