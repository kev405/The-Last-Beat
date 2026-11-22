using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>Menú principal (tarea 35): Jugar carga el nivel y Salir cierra el juego.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [Tooltip("Nombre exacto de la escena del nivel en Build Profiles.")]
        [SerializeField] private string gameSceneName = "Pueblo";

        private void Awake()
        {
            Time.timeScale = 1f;
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void Start()
        {
            if (playButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        private void Play() => SceneManager.LoadScene(gameSceneName);

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
