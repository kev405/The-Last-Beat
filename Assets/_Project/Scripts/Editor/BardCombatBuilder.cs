using System.IO;
using TheLastBeat.Abilities;
using TheLastBeat.Characters;
using TheLastBeat.Combat;
using TheLastBeat.Heroes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta el combate de la bardo desde el menú "The Last Beat": su ataque básico, sus dos habilidades con recarga,
    /// la IA que decide cuándo usarlos y sus ranuras en el HUD.
    ///  1. Crea los assets de datos Ability_GolpeDeBajo y Ability_PistaDeBaile (si ya existen, respeta sus valores
    ///     y solo actualiza los tiempos que dependen de la animación).
    ///  2. Añade a la bardo y conecta: BardActionMoments, BassDropAbility, DanceFloorAbility, AbilityCaster (ranura 0
    ///     = Golpe de bajo, ranura 1 = Pista de baile), BardBasicAttack (la Nota), BardCombatAI y AbilityAnimationBridge.
    ///  3. Vuelve a montar los efectos, si ya lo estaban, para que escuchen al ataque y a las habilidades.
    ///  4. Añade al HUD de la escena las ranuras de la bardo (UiBuilder.CreateBardAbilities).
    /// Requiere haber ejecutado antes "Montar bardo". Se puede ejecutar varias veces: no duplica componentes.
    /// Una vez montado, "Montar bardo" lo vuelve a enlazar solo (RebuildIfMounted), porque rehace el modelo y con
    /// él el Animator. Es código de editor: no va en la build.
    /// </summary>
    public static class BardCombatBuilder
    {
        private const string DataFolder = "Assets/_Project/ScriptableObjects/Abilities/";
        private const string BassDropDataPath = DataFolder + "Ability_GolpeDeBajo.asset";
        private const string DanceFloorDataPath = DataFolder + "Ability_PistaDeBaile.asset";

        private const string ModelName = "Bard_Model";
        private const string HudName = "HUD";
        private const string Title = "Combate de la bardo";
        private const string UndoName = "Montar combate de la bardo";

        // ---------- Valores iniciales (solo se usan al crear cada asset; después se ajustan en su Inspector) ----------
        // Golpe de bajo, nivel 1: empuje en radio 4 m y daño bajo.
        private const float BassDropCooldown = 9f;
        private const float BassDropDamage = 10f;
        private const float BassDropRadius = 4f;
        private const float BassDropKnockback = 11f;   // con el freno del soldado (18 m/s²) lo aleja unos 3,4 m

        // Pista de baile, nivel 1: cura 4 por segundo en radio 3,5 m durante 6 s.
        private const float DanceFloorCooldown = 16f;
        private const float DanceFloorRadius = 3.5f;
        private const float DanceFloorHealPerSecond = 4f;
        private const float DanceFloorDuration = 6f;

        [MenuItem("The Last Beat/Personajes/Montar combate de la bardo (ataque, habilidades y HUD)")]
        public static void BuildCombat()
        {
            BardCompanion companion = FindBard();
            if (companion == null)
            {
                EditorUtility.DisplayDialog(Title, "No hay ningún BardCompanion en la escena abierta.", "Entendido");
                return;
            }
            Mount(companion, true);
        }

        /// <summary>
        /// Si la bardo de la escena ya tiene su combate montado, lo vuelve a enlazar sin preguntar (y con él los
        /// efectos). Lo llama "Montar bardo" después de rehacer el modelo. Devuelve false si no había nada montado.
        /// </summary>
        internal static bool RebuildIfMounted()
        {
            BardCompanion companion = FindBard();
            if (companion == null || companion.GetComponent<BardBasicAttack>() == null) return false;
            return Mount(companion, false);
        }

        private static bool Mount(BardCompanion companion, bool showDialogs)
        {
            GameObject bard = companion.gameObject;
            Transform model = bard.transform.Find("Visual/" + ModelName);
            Animator animator = model != null ? model.GetComponent<Animator>() : null;
            if (animator == null)
            {
                string message = "No encuentro el Animator en Visual/" + ModelName + ".\nEjecuta primero \"Montar bardo\".";
                if (showDialogs) EditorUtility.DisplayDialog(Title, message, "Entendido");
                else Debug.LogWarning("[BardCombatBuilder] " + message);
                return false;
            }

            // ----- 1. Datos de las habilidades -----
            BassDropAbilityData bassData = LoadOrCreate<BassDropAbilityData>(BassDropDataPath, InitBassDrop);
            DanceFloorAbilityData floorData = LoadOrCreate<DanceFloorAbilityData>(DanceFloorDataPath, InitDanceFloor);

            // Los tiempos salen de las animaciones (BardVisualBuilder): se ponen al día en cada ejecución.
            bassData.hitDelay = BardVisualBuilder.BassDropMoment / BardVisualBuilder.BassDropSpeed;
            bassData.castTime = BardVisualBuilder.BassDropLength / BardVisualBuilder.BassDropSpeed;
            floorData.hitDelay = BardVisualBuilder.DanceFloorMoment / BardVisualBuilder.DanceFloorSpeed;
            floorData.castTime = BardVisualBuilder.DanceFloorLength / BardVisualBuilder.DanceFloorSpeed;
            EditorUtility.SetDirty(bassData);
            EditorUtility.SetDirty(floorData);

            // ----- 2. Componentes de la bardo -----
            Health health = bard.GetComponent<Health>();

            BardActionMoments moments = GetOrAdd<BardActionMoments>(bard);
            Set(moments, "animator", animator);

            BassDropAbility bass = GetOrAdd<BassDropAbility>(bard);
            Set(bass, "data", bassData);
            Set(bass, "moments", moments);

            DanceFloorAbility floor = GetOrAdd<DanceFloorAbility>(bard);
            Set(floor, "data", floorData);
            Set(floor, "moments", moments);

            // Lanzador: ranura 0 = Golpe de bajo, ranura 1 = Pista de baile. Sin PlayerInputReader: las lanza la IA.
            AbilityCaster caster = GetOrAdd<AbilityCaster>(bard);
            SerializedObject casterObject = new SerializedObject(caster);
            casterObject.FindProperty("health").objectReferenceValue = health;
            SerializedProperty slots = casterObject.FindProperty("slots");
            slots.arraySize = 2;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = bass;
            slots.GetArrayElementAtIndex(1).objectReferenceValue = floor;
            casterObject.ApplyModifiedProperties();

            // Ataque básico: la Nota.
            BardBasicAttack attack = GetOrAdd<BardBasicAttack>(bard);
            SerializedObject attackObject = new SerializedObject(attack);
            attackObject.FindProperty("animator").objectReferenceValue = animator;
            attackObject.FindProperty("moments").objectReferenceValue = moments;
            attackObject.FindProperty("companion").objectReferenceValue = companion;
            attackObject.FindProperty("caster").objectReferenceValue = caster;
            attackObject.FindProperty("health").objectReferenceValue = health;
            attackObject.FindProperty("animationTime").floatValue = BardVisualBuilder.NoteLength / BardVisualBuilder.NoteSpeed;
            attackObject.FindProperty("releaseTime").floatValue = BardVisualBuilder.NoteMoment / BardVisualBuilder.NoteSpeed;
            attackObject.ApplyModifiedProperties();

            // La IA que decide cuándo usar cada cosa.
            BardCombatAI brain = GetOrAdd<BardCombatAI>(bard);
            SerializedObject brainObject = new SerializedObject(brain);
            brainObject.FindProperty("companion").objectReferenceValue = companion;
            brainObject.FindProperty("caster").objectReferenceValue = caster;
            brainObject.FindProperty("attack").objectReferenceValue = attack;
            brainObject.FindProperty("health").objectReferenceValue = health;
            brainObject.FindProperty("self").objectReferenceValue = bard.GetComponent<HeroTarget>();
            brainObject.ApplyModifiedProperties();

            // Animaciones de las habilidades: cada una dispara su trigger al empezar.
            AbilityAnimationBridge bridge = GetOrAdd<AbilityAnimationBridge>(bard);
            SerializedObject bridgeObject = new SerializedObject(bridge);
            bridgeObject.FindProperty("animator").objectReferenceValue = animator;
            SerializedProperty entries = bridgeObject.FindProperty("entries");
            entries.arraySize = 2;
            SetEntry(entries.GetArrayElementAtIndex(0), bass, "BassDrop");
            SetEntry(entries.GetArrayElementAtIndex(1), floor, "DanceFloor");
            bridgeObject.ApplyModifiedProperties();

            // ----- 3. Efectos: que escuchen al ataque y a las habilidades -----
            BardVfxBuilder.RebuildIfMounted();
            bool effects = bard.GetComponent<TheLastBeat.Feedback.BardNoteVfx>() != null;

            // ----- 4. HUD -----
            // Desde el menú, las ranuras se rehacen. Al re-enlazar en silencio solo se crean si faltan, para no
            // pisar un ajuste hecho a mano: siguen apuntando al mismo AbilityCaster, que no se rehace.
            GameObject hud = GameObject.Find(HudName);
            RectTransform canvas = hud != null ? hud.transform as RectTransform : null;
            if (canvas != null && (showDialogs || canvas.Find(UiBuilder.BardAbilitiesName) == null))
                UiBuilder.CreateBardAbilities(canvas, caster);

            EditorSceneManager.MarkSceneDirty(bard.scene);
            AssetDatabase.SaveAssets();
            if (!showDialogs) return true;

            Selection.activeGameObject = bard;

            string notes = "";
            if (health == null) notes += "\n- La bardo no tiene Health: sin él no ataca ni se cura.";
            if (bard.GetComponent<HeroTarget>() == null) notes += "\n- La bardo no tiene HeroTarget: la IA no sabrá si la están atacando.";
            if (!effects) notes += "\n- Los efectos no están montados: ejecuta \"Montar efectos de la bardo\".";
            if (canvas == null) notes += "\n- No hay un objeto llamado " + HudName + " en la escena: las ranuras no se añadieron.";

            Debug.Log("[BardCombatBuilder] Combate de la bardo montado. Datos en " + DataFolder);
            EditorUtility.DisplayDialog(Title,
                notes.Length == 0
                    ? "Ataque, habilidades, IA y ranuras del HUD quedaron montados. Guarda la escena con Ctrl+S."
                    : "Quedó montado, con estos avisos:" + notes,
                "Listo");
            return true;
        }

        // ---------- Datos ----------

        private static void InitBassDrop(BassDropAbilityData data)
        {
            data.displayName = "Golpe de bajo";
            data.description = "Pisa al ritmo y suelta una onda que empuja a los enemigos que tiene alrededor.";
            data.cooldown = BassDropCooldown;
            data.damage = BassDropDamage;
            data.radius = BassDropRadius;
            data.knockback = BassDropKnockback;
            data.interrupts = true;
            data.level = 1;
            data.stunSeconds = 0f;   // nivel 2: 1 s
            data.staggers = true;
        }

        private static void InitDanceFloor(DanceFloorAbilityData data)
        {
            data.displayName = "Pista de baile";
            data.description = "Proyecta en el piso una pista de luces. Los aliados que están dentro se curan poco a poco.";
            data.cooldown = DanceFloorCooldown;
            data.damage = 0f;
            data.radius = DanceFloorRadius;
            data.knockback = 0f;
            data.interrupts = false;
            data.level = 1;
            data.healPerSecond = DanceFloorHealPerSecond;
            data.floorDuration = DanceFloorDuration;
            data.tickInterval = 1f;
            data.healsCaster = true;
        }

        /// <summary>Carga el asset o, si no existe, lo crea con sus valores iniciales.</summary>
        private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------- Utilidades ----------

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(target);
            return component;
        }

        private static void Set(Object target, string field, Object value)
        {
            if (target == null) return;

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[BardCombatBuilder] " + target.GetType().Name + " no tiene el campo '" + field + "'.");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetEntry(SerializedProperty entry, Ability ability, string trigger)
        {
            entry.FindPropertyRelative("ability").objectReferenceValue = ability;
            entry.FindPropertyRelative("trigger").stringValue = trigger;
            entry.FindPropertyRelative("finishedTrigger").stringValue = "";
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
