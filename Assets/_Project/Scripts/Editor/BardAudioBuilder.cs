using TheLastBeat.Feedback;
using TheLastBeat.Heroes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta el audio de la canción de la bardo desde el menú "The Last Beat" (tarea 32):
    ///  1. Ajusta la importación de BardSong_Loop.wav y BardSong_Intro.wav (Vorbis, Compressed In Memory).
    ///  2. Añade BardSongAudio a la bardo y le conecta el SongBuff, los dos clips y el tempo.
    ///  3. Añade BeatPulse al disco del área de la canción (el Aura de SongBuff) para que lata con el dembow.
    /// Se puede ejecutar varias veces: no duplica componentes y respeta el volumen y los tiempos ya ajustados.
    /// Los clips los genera Audio/Cancion de la bardo/generar_dembow.py (fuera del proyecto de Unity).
    /// Es código de editor: no va en la build.
    /// </summary>
    public static class BardAudioBuilder
    {
        private const string MusicFolder = "Assets/_Project/Audio/Music/";
        private const string LoopPath = MusicFolder + "BardSong_Loop.wav";
        private const string IntroPath = MusicFolder + "BardSong_Intro.wav";
        private const string Title = "Canción de la bardo";

        // Con lo que se generó el dembow: 96 BPM, compás de 4/4 y frases de 4 compases.
        private const float Bpm = 96f;
        private const int BeatsPerBar = 4;
        private const int PhraseBars = 4;

        private const float VorbisQuality = 0.7f;

        [MenuItem("The Last Beat/Audio/Montar canción de la bardo")]
        public static void Build()
        {
            AssetDatabase.Refresh();

            AudioClip loop = ImportClip(LoopPath);
            AudioClip intro = ImportClip(IntroPath);
            if (loop == null)
            {
                EditorUtility.DisplayDialog(Title, "No encuentro " + LoopPath + ".", "Entendido");
                return;
            }

            BardCompanion companion = FindBard();
            if (companion == null)
            {
                EditorUtility.DisplayDialog(Title, "No hay ningún BardCompanion en la escena abierta.", "Entendido");
                return;
            }

            GameObject bard = companion.gameObject;
            SongBuff song = bard.GetComponent<SongBuff>();
            if (song == null)
            {
                EditorUtility.DisplayDialog(Title, "La bardo no tiene SongBuff: sin él no hay canción que sonar.", "Entendido");
                return;
            }

            // ----- La canción -----
            BardSongAudio songAudio = bard.GetComponent<BardSongAudio>();
            if (songAudio == null) songAudio = Undo.AddComponent<BardSongAudio>(bard);

            SerializedObject audioObject = new SerializedObject(songAudio);
            audioObject.FindProperty("song").objectReferenceValue = song;
            audioObject.FindProperty("loopClip").objectReferenceValue = loop;
            audioObject.FindProperty("introClip").objectReferenceValue = intro;
            audioObject.FindProperty("bpm").floatValue = Bpm;
            audioObject.FindProperty("beatsPerBar").intValue = BeatsPerBar;
            audioObject.FindProperty("resumeEveryBars").intValue = PhraseBars;
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

            EditorSceneManager.MarkSceneDirty(bard.scene);
            Selection.activeGameObject = bard;

            string notes = "";
            if (intro == null) notes += "\n- No encuentro " + IntroPath + ": la canción volverá sin entrada.";
            if (aura == null) notes += "\n- SongBuff no tiene asignado el Aura: nada late con el pulso.";
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                notes += "\n- No hay ningún AudioListener en la escena: no se va a oír nada.";

            Debug.Log("[BardAudioBuilder] Canción de la bardo montada con " + LoopPath);
            EditorUtility.DisplayDialog(Title,
                notes.Length == 0
                    ? "La canción quedó montada en la bardo. Guarda la escena con Ctrl+S y dale a Play."
                    : "Quedó montada, con estos avisos:" + notes,
                "Listo");
        }

        /// <summary>Deja el clip en Vorbis y Compressed In Memory (pesa poco y el loop no tiene costura) y lo devuelve.</summary>
        private static AudioClip ImportClip(string path)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return null;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool changed = settings.loadType != AudioClipLoadType.CompressedInMemory ||
                           settings.compressionFormat != AudioCompressionFormat.Vorbis ||
                           !Mathf.Approximately(settings.quality, VorbisQuality) ||
                           settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                           importer.forceToMono || importer.loadInBackground;
            if (changed)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = VorbisQuality;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
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
