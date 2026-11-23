using System;
using System.Collections.Generic;
using System.Reflection;
using TheLastBeat.Audio;
using TheLastBeat.Heroes;
using TheLastBeat.Level;
using TheLastBeat.Player;
using TheLastBeat.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta todo el audio de la escena abierta desde el menú "The Last Beat" (tarea 32):
    ///  1. Audio Mixer: usa Assets/_Project/Audio/MainMixer.mixer (lo crea si no existe) y le añade los grupos
    ///     Música, Efectos y Ambiente que le falten.
    ///  2. SfxPlayer: un objeto raíz con el pool de efectos (sobrevive a los cambios de escena).
    ///  3. Botones: ButtonSfx en cada Button de la escena, con el sonido de pasar por encima y el de pulsar.
    ///  4. Si la escena tiene LevelFlow (el nivel): un objeto raíz "Audio" con AmbiencePlayer (el viento) y GameSfx
    ///     (todos los efectos, con sus variantes); Footsteps en el caballero y en la bardo; y la canción por capas
    ///     en la bardo (BardAudioBuilder).
    /// En la escena del menú principal solo hace los pasos 1 a 3. Se puede ejecutar varias veces: no duplica nada y
    /// respeta los volúmenes ya ajustados. Es código de editor: no va en la build.
    /// </summary>
    public static class AudioBuilder
    {
        private const string MixerPath = "Assets/_Project/Audio/MainMixer.mixer";
        private const string SfxFolder = "Assets/_Project/Audio/SFX/";
        private const string WindPath = "Assets/_Project/Audio/Ambience/Village_Wind_Loop.wav";

        private const string MusicGroup = "Música";
        private const string SfxGroup = "Efectos";
        private const string AmbienceGroup = "Ambiente";

        private const string AudioRootName = "Audio";
        private const string PlayerRootName = "SfxPlayer";
        private const string Title = "Audio de la escena";
        private const string UndoName = "Montar audio de la escena";

        // Campo de GameSfx → nombre del archivo en Audio/SFX (sin _01, _02...: las variantes se cargan solas).
        private static readonly string[,] Cues =
        {
            { "knightSwing", "Knight_Swing" }, { "knightSwingHeavy", "Knight_Swing_Heavy" },
            { "spinWhirl", "Spin_Whirl" }, { "spinExplosion", "Spin_Explosion" }, { "chargeDash", "Charge_Dash" },
            { "dodge", "Dodge" }, { "knightHurt", "Knight_Hurt" }, { "knightDeath", "Knight_Death" },
            { "buffOn", "Buff_On" }, { "buffOff", "Buff_Off" }, { "abilityReady", "Ability_Ready" },
            { "hitLight", "Hit_Light" }, { "hitHeavy", "Hit_Heavy" }, { "hitNote", "Hit_Note" },
            { "bardNote", "Bard_Note" }, { "bassDropCharge", "BassDrop_Charge" }, { "bassDropBoom", "BassDrop_Boom" },
            { "danceFloorOpen", "DanceFloor_Open" }, { "danceFloorClose", "DanceFloor_Close" }, { "healTick", "Heal_Tick" },
            { "bardHurt", "Bard_Hurt" }, { "bardDeath", "Bard_Death" }, { "songInterrupt", "Song_Interrupt" },
            { "soldierSpawn", "Soldier_Spawn" }, { "soldierWindup", "Soldier_Windup" },
            { "soldierSwing", "Soldier_Swing" }, { "soldierDeath", "Soldier_Death" },
            { "waveStart", "Wave_Start" }, { "waveCleared", "Wave_Cleared" }, { "victory", "Victory" }, { "defeat", "Defeat" }
        };

        [MenuItem("The Last Beat/Audio/Montar audio de la escena", priority = 0)]
        public static void Build()
        {
            AssetDatabase.Refresh();

            Scene scene = SceneManager.GetActiveScene();
            List<string> missing = new List<string>();
            string notes = "";
            string done = "";

            // ----- 1. Audio Mixer -----
            AudioMixer mixer = LoadOrCreateMixer();
            AudioMixerGroup music = FindGroup(mixer, MusicGroup);
            AudioMixerGroup sfx = FindGroup(mixer, SfxGroup);
            AudioMixerGroup ambience = FindGroup(mixer, AmbienceGroup);
            if (mixer == null)
            {
                notes += "\n- No pude crear el Audio Mixer solo. Todo suena igual sin él; para tenerlo, créalo a mano: " +
                         "en Assets/_Project/Audio, clic derecho, Create, Audio, Audio Mixer, con el nombre MainMixer; " +
                         "ábrelo y, con Master seleccionado, añade tres grupos llamados " + MusicGroup + ", " + SfxGroup +
                         " y " + AmbienceGroup + "; después vuelve a ejecutar este menú.";
            }
            else if (music == null || sfx == null || ambience == null)
            {
                notes += "\n- Al Audio Mixer le falta algún grupo (" + MusicGroup + ", " + SfxGroup + ", " + AmbienceGroup + ").";
            }
            else
            {
                done += "\n- Audio Mixer con los grupos " + MusicGroup + ", " + SfxGroup + " y " + AmbienceGroup + ".";
            }

            // ----- 2. El reproductor de efectos -----
            SfxPlayer player = FindInScene<SfxPlayer>(scene);
            if (player == null)
            {
                GameObject holder = new GameObject(PlayerRootName);
                Undo.RegisterCreatedObjectUndo(holder, UndoName);
                player = Undo.AddComponent<SfxPlayer>(holder);
            }
            if (sfx != null) SetReference(player, "output", sfx);
            done += "\n- " + PlayerRootName + ": el pool de efectos.";

            // ----- 3. Los botones -----
            int buttons = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Button[] found = roots[r].GetComponentsInChildren<Button>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    ButtonSfx buttonSfx = GetOrAdd<ButtonSfx>(found[i].gameObject);
                    SerializedObject buttonObject = new SerializedObject(buttonSfx);
                    SetCue(buttonObject, "hover", "UI_Hover", missing);
                    SetCue(buttonObject, "click", "UI_Click", missing);
                    buttonObject.ApplyModifiedProperties();
                    buttons++;
                }
            }
            done += "\n- Sonido en " + buttons + " botones.";

            // ----- 4. El nivel -----
            LevelFlow level = FindInScene<LevelFlow>(scene);
            if (level != null)
            {
                GameObject audioRoot = null;
                for (int r = 0; r < roots.Length; r++)
                    if (roots[r].name == AudioRootName) audioRoot = roots[r];
                if (audioRoot == null)
                {
                    audioRoot = new GameObject(AudioRootName);
                    Undo.RegisterCreatedObjectUndo(audioRoot, UndoName);
                }

                // El viento del pueblo.
                AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>(WindPath);
                if (wind == null) notes += "\n- No encuentro " + WindPath + ": el pueblo no tendrá viento.";
                AmbiencePlayer ambiencePlayer = GetOrAdd<AmbiencePlayer>(audioRoot);
                SetReference(ambiencePlayer, "level", level);
                SetReference(ambiencePlayer, "clip", wind);
                if (ambience != null) SetReference(ambiencePlayer, "output", ambience);

                // Los efectos del combate, las habilidades y el nivel.
                GameSfx gameSfx = GetOrAdd<GameSfx>(audioRoot);
                SerializedObject sfxObject = new SerializedObject(gameSfx);
                sfxObject.FindProperty("level").objectReferenceValue = level;
                for (int i = 0; i < Cues.GetLength(0); i++) SetCue(sfxObject, Cues[i, 0], Cues[i, 1], missing);
                sfxObject.ApplyModifiedProperties();
                done += "\n- " + AudioRootName + ": el viento (AmbiencePlayer) y los efectos del nivel (GameSfx).";

                // Los pasos.
                PlayerController knight = FindInScene<PlayerController>(scene);
                BardCompanion bard = FindInScene<BardCompanion>(scene);
                if (knight != null) MountFootsteps(knight.gameObject, 0.4f, missing);
                if (bard != null) MountFootsteps(bard.gameObject, 0.25f, missing);
                if (knight != null || bard != null) done += "\n- Pasos en el caballero y en la bardo.";

                // La canción.
                if (bard != null)
                {
                    string songNotes = BardAudioBuilder.Mount(bard.gameObject, music);
                    notes += songNotes;
                    if (songNotes.Length == 0) done += "\n- La canción por capas en la bardo (BardSongAudio).";
                }
                else
                {
                    notes += "\n- No hay ningún BardCompanion en la escena: la canción no quedó montada.";
                }
            }

            if (missing.Count > 0)
                notes += "\n- No encuentro estos sonidos en " + SfxFolder + ": " + string.Join(", ", missing.ToArray()) + ".";

            bool hasListener = FindInScene<AudioListener>(scene) != null;
            if (!hasListener) notes += "\n- No hay ningún AudioListener en la escena: no se va a oír nada.";

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AudioBuilder] Audio montado en la escena " + scene.name + "." + done + notes);

            EditorUtility.DisplayDialog(Title,
                "Quedó montado en la escena " + scene.name + ":" + done +
                (notes.Length == 0 ? "" : "\n\nAvisos:" + notes) +
                "\n\nGuarda la escena con Ctrl+S.",
                "Listo");
        }

        // ---------- Audio Mixer ----------

        /// <summary>
        /// Carga el mixer (o lo crea) y le añade los grupos que le falten. Unity no tiene una API pública para crear
        /// un Audio Mixer ni sus grupos, así que usa por reflexión la misma clase que usa el editor
        /// (UnityEditor.Audio.AudioMixerController). Si esa clase cambia en otra versión de Unity, devuelve lo que
        /// haya y el menú avisa de lo que falta.
        /// </summary>
        private static AudioMixer LoadOrCreateMixer()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);

            try
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
                if (type == null) return mixer;

                if (mixer == null)
                {
                    MethodInfo create = type.GetMethod("CreateMixerControllerAtPath", flags);
                    if (create == null) return null;
                    mixer = create.Invoke(null, new object[] { MixerPath }) as AudioMixer;
                    if (mixer == null) mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                }
                if (mixer == null || !type.IsInstanceOfType(mixer)) return mixer;

                MethodInfo createGroup = type.GetMethod("CreateNewGroup", flags);
                MethodInfo addChild = type.GetMethod("AddChildToParent", flags);
                MethodInfo addToView = type.GetMethod("AddGroupToCurrentView", flags);
                PropertyInfo master = type.GetProperty("masterGroup", flags);
                if (createGroup == null || addChild == null || master == null) return mixer;

                object masterGroup = master.GetValue(mixer, null);
                string[] names = { MusicGroup, SfxGroup, AmbienceGroup };
                bool changed = false;
                for (int i = 0; i < names.Length; i++)
                {
                    if (FindGroup(mixer, names[i]) != null) continue;

                    object group = createGroup.Invoke(mixer, new object[] { names[i], false });
                    addChild.Invoke(mixer, new object[] { group, masterGroup });
                    changed = true;

                    // Que el grupo se vea en la ventana del mixer. Es opcional y puede fallar: un mixer recién creado
                    // todavía no tiene vista (Unity la crea al abrir la ventana). El grupo funciona igual.
                    try
                    {
                        if (addToView != null) addToView.Invoke(mixer, new object[] { group });
                    }
                    catch (Exception)
                    {
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(mixer);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e)
            {
                Exception cause = e.InnerException ?? e;
                Debug.LogWarning("[AudioBuilder] No pude completar el Audio Mixer por código: " + cause.GetType().Name + ": " + cause.Message);
            }

            return mixer != null ? mixer : AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string groupName)
        {
            if (mixer == null) return null;

            AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
            for (int i = 0; i < groups.Length; i++)
                if (groups[i].name == groupName) return groups[i];
            return null;
        }

        // ---------- Clips ----------

        /// <summary>Carga Nombre.wav o, si el sonido tiene variantes, Nombre_01.wav, Nombre_02.wav...</summary>
        private static AudioClip[] LoadClips(string clipName)
        {
            List<AudioClip> clips = new List<AudioClip>();
            AudioClip single = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + clipName + ".wav");
            if (single != null) clips.Add(single);

            for (int i = 1; i <= 9; i++)
            {
                AudioClip variant = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + clipName + "_0" + i + ".wav");
                if (variant == null) break;
                clips.Add(variant);
            }
            return clips.ToArray();
        }

        /// <summary>Asigna los clips de un SfxCue. No toca su volumen ni sus demás ajustes.</summary>
        private static void SetCue(SerializedObject target, string field, string clipName, List<string> missing)
        {
            SerializedProperty clips = target.FindProperty(field + ".clips");
            if (clips == null)
            {
                Debug.LogWarning("[AudioBuilder] " + target.targetObject.GetType().Name + " no tiene el campo '" + field + "'.");
                return;
            }

            AudioClip[] found = LoadClips(clipName);
            if (found.Length == 0)
            {
                if (!missing.Contains(clipName)) missing.Add(clipName);
                return;
            }

            clips.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        private static void MountFootsteps(GameObject character, float volume, List<string> missing)
        {
            bool isNew = character.GetComponent<Footsteps>() == null;
            Footsteps footsteps = GetOrAdd<Footsteps>(character);

            SerializedObject stepsObject = new SerializedObject(footsteps);
            SetCue(stepsObject, "steps", "Footstep", missing);
            if (isNew) stepsObject.FindProperty("steps.volume").floatValue = volume;   // después se respeta lo que se ajuste a mano
            stepsObject.ApplyModifiedProperties();
        }

        // ---------- Utilidades ----------

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(target);
            return component;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[AudioBuilder] " + target.GetType().Name + " no tiene el campo '" + field + "'.");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
