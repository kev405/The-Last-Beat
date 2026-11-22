using TheLastBeat.Heroes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Estado de la canción en el HUD (tarea 34): un ícono que cambia de color y un texto.
    /// Morado = suena; rojo con anillo que se llena = cortada y recuperándose; gris = silencio.
    /// Se entiende por color sin tener que leer el texto.
    /// </summary>
    public class SongStatusUI : MonoBehaviour
    {
        [SerializeField] private SongBuff song;
        [SerializeField] private Image icon;
        [Tooltip("Imagen con Image Type = Filled (Radial 360) que se llena durante la recuperación.")]
        [SerializeField] private Image recoveryRing;
        [SerializeField] private TMP_Text label;

        [SerializeField] private Color singingColor = new Color(0.71f, 0.55f, 0.94f);
        [SerializeField] private Color interruptedColor = new Color(0.94f, 0.33f, 0.27f);
        [SerializeField] private Color silentColor = new Color(0.45f, 0.43f, 0.5f);

        private void OnEnable()
        {
            if (song == null) return;
            song.SongStateChanged += Apply;
            Apply(song.State);
        }

        private void OnDisable()
        {
            if (song != null) song.SongStateChanged -= Apply;
        }

        private void Apply(SongState s)
        {
            Color c = s switch
            {
                SongState.Singing => singingColor,
                SongState.Interrupted => interruptedColor,
                _ => silentColor
            };
            if (icon != null) icon.color = c;
            if (label != null)
            {
                label.color = c;
                label.SetText(s switch
                {
                    SongState.Singing => "CANCIÓN: SUENA",
                    SongState.Interrupted => "CANCIÓN: CORTADA",
                    _ => "SILENCIO"
                });
            }
            if (recoveryRing != null)
            {
                recoveryRing.gameObject.SetActive(s == SongState.Interrupted);
                recoveryRing.fillAmount = 0f;
            }
        }

        private void Update()
        {
            if (song == null || recoveryRing == null || song.State != SongState.Interrupted) return;
            float total = song.RecoveryTime;
            recoveryRing.fillAmount = total > 0f ? 1f - song.RecoveryRemaining / total : 1f;
        }
    }
}
