using TheLastBeat.Feedback;
using TheLastBeat.Heroes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta el audio de la canción de la bardo (tarea 32):
    ///  1. Añade BardSongAudio a la bardo y le conecta el SongBuff, las tres capas (base, percusión y melodía),
    ///     la entrada y el tempo.
    ///  2. Añade BeatPulse al disco del área de la canción (el Aura de SongBuff) para que lata con el dembow.
    /// Se puede ejecutar varias veces: no duplica componentes y respeta el volumen y los tiempos ya ajustados.
    /// "Montar audio de la escena" (AudioBuilder) lo llama por dentro; este menú queda para rehacer solo la canción.
    /// Los clips los genera Audio/Cancion de la bardo/generar_dembow.py (fuera del proyecto de Unity).
    /// Es código de editor: no va en la build.
    /// </summary>
    public static class BardAudioBuilder
    {
        private const string MusicFolder = "Assets/_Project/Audio/Music/";
        private const string IntroPath = MusicFolder + "BardSong_Intro.wav";
        private const string Title = "Canción de la bardo";

        // En el orden en que entran.
        private static readonly string[] LayerPaths =
        {
            MusicFolder + "BardSong_Base.wav",
            MusicFolder + "BardSong_Percussion.wav",
            MusicFolder + "BardSong_Melody.wav"
        };

        // Con lo que se generó el dembow: 96 BPM, compás de 4/4 y frases de 4 compases.
        private const float Bpm = 96f;
        private const int BeatsPerBar = 4;
        private const int PhraseBars = 4;

        [MenuItem("The Last Beat/Audio/Montar canción de la bardo", priority = 20)]
        public static void Build()
        {
            AssetDatabase.Refresh();

            BardCompanion companion = FindBard();
            if (companion == null)
            {
                EditorUtility.DisplayDialog(Title, "No hay ningún BardCompanion en la escena abierta.", "Entendido");
                return;
            }

            string notes = Mount(companion.gameObject, null);
            EditorSceneManager.MarkSceneDirty(companion.gameObject.scene);
            Selection.activeGameObject = companion.gameObject;

            EditorUtility.DisplayDialog(Title,
                notes.Length == 0
                    ? "La canción quedó montada en la bardo. Guarda la escena con Ctrl+S y dale a Play."
                    : "Estos son los avisos:" + notes,
                "Listo");
        }

        /// <summary>
        /// Monta la canción en la bardo y devuelve los avisos (vacío si todo salió bien). Si musicGroup no es null,
        /// la canción sale por ese grupo del Audio Mixer.
        /// </summary>
        internal static string Mount(GameObject bard, AudioMixerGroup musicGroup)
        {
            SongBuff song = bard.GetComponent<SongBuff>();
            if (song == null) return "\n- La bardo no tiene SongBuff: sin él no hay canción que sonar.";

            string notes = "";
            AudioClip[] layers = new AudioClip[LayerPaths.Length];
            for (int i = 0; i < LayerPaths.Length; i++)
            {
                layers[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(LayerPaths[i]);
                if (layers[i] == null) notes += "\n- No encuentro " + LayerPaths[i] + ".";
            }
            if (layers[0] == null) return notes;

            AudioClip intro = AssetDatabase.LoadAssetAtPath<AudioClip>(IntroPath);
            if (intro == null) notes += "\n- No encuentro " + IntroPath + ": la canción volverá sin entrada.";

            // ----- La canción -----
            BardSongAudio songAudio = bard.GetComponent<BardSongAudio>();
            if (songAudio == null) songAudio = Undo.AddComponent<BardSongAudio>(bard);

            SerializedObject audioObject = new SerializedObject(songAudio);
            audioObject.FindProperty("song").objectReferenceValue = song;
            SerializedProperty layerClips = audioObject.FindProperty("layerClips");
            layerClips.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
                layerClips.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            audioObject.FindProperty("introClip").objectReferenceValue = intro;
            audioObject.FindProperty("bpm").floatValue = Bpm;
            audioObject.FindProperty("beatsPerBar").intValue = BeatsPerBar;
            audioObject.FindProperty("resumeEveryBars").intValue = PhraseBars;
            if (musicGroup != null) audioObject.FindProperty("output").objectReferenceValue = musicGroup;
            audioObject.ApplyModifiedProperties();

            // ----- El área de la canción late con el pulso -----
            GameObject aura = new SerializedObject(song).FindProperty("aura").objectReferenceValue as GameObject;
            if (aura != null)
            {
                BeatPulse pulse = aura.GetComponent<BeatPulse>();
                if (pulse == null) pulse = Undo.AddComponent<BeatPulse>(aura);

                SerializedObject pulseObject = new SerializedObject(pulse);
                pulseObject.FindProperty("songAudio").objectReferenceValue = songAudio;
                pulseObject.ApplyModifiedProperties();
            }
            else
            {
                notes += "\n- SongBuff no tiene asignado el Aura: nada late con el pulso.";
            }

            return notes;
        }

        private static BardCompanion FindBard()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                BardCompanion fromSelection = selected.GetComponentInParent<BardCompanion>();
                if (fromSelection != null) return fromSelection;
            }

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                BardCompanion found = roots[i].GetComponentInChildren<BardCompanion>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
