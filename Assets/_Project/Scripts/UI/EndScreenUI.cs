using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Pantallas de victoria y derrota (tarea 35). Se muestran cuando LevelFlow termina el nivel.
    /// Cada una tiene Reintentar (reinicia el nivel limpio) y Menú.
    /// Selecciona el primer botón para que también funcione con control.
    /// </summary>
    public class EndScreenUI : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;

        [Header("Victoria")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Button victoryRetry;
        [SerializeField] private Button victoryMenu;

        [Header("Derrota")]
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Button defeatRetry;
        [SerializeField] private Button defeatMenu;

        private void Awake()
        {
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            Wire(victoryRetry, Retry);
            Wire(defeatRetry, Retry);
            Wire(victoryMenu, Menu);
            Wire(defeatMenu, Menu);
        }

        private void OnEnable()
        {
            if (flow != null) flow.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (flow != null) flow.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState s)
        {
            if (s == GameState.Victory) Open(victoryPanel, victoryRetry);
            else if (s == GameState.Defeat) Open(defeatPanel, defeatRetry);
        }

        private static void Open(GameObject panel, Button first)
        {
            if (panel == null) return;
            panel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private void Retry()
        {
            if (flow != null) flow.Restart();
        }

        private void Menu()
        {
            if (flow != null) flow.GoToMenu();
        }

        private static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b != null) b.onClick.AddListener(action);
        }
    }
}
