using System.IO;
using TheLastBeat.Abilities;
using TheLastBeat.Characters;
using TheLastBeat.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta el caballero de Mixamo en la escena abierta desde el menú "The Last Beat" (tarea 28):
    ///  1. Configura la importación: rig Humanoid, avatar compartido, clips en bucle y combo partido en 3.
    ///  2. Crea los materiales de URP con las texturas y el Animator Controller con los parámetros
    ///     que ya usa el código: Speed (PlayerController), Attack1/2/3 (KnightCombat), Hit y Die.
    ///  3. Reemplaza la cápsula de prueba por el modelo, pone la espada en la mano derecha y conecta
    ///     el Animator a PlayerController, KnightCombat y CharacterAnimationBridge.
    /// Se puede ejecutar varias veces: rehace todo sin duplicar nada. Es código de editor: no va en la build.
    /// </summary>
    public static class KnightVisualBuilder
    {
        // ---------- Archivos ----------
        private const string KnightFolder = "Assets/_Project/Art/Models/Knight/";
        private const string ModelPath = KnightFolder + "Knight.fbx";
        private const string IdlePath = KnightFolder + "Knight@Idle.fbx";
        private const string WalkPath = KnightFolder + "Knight@Walk.fbx";
        private const string RunPath = KnightFolder + "Knight@Run.fbx";
        private const string ComboPath = KnightFolder + "Knight@AttackCombo.fbx";
        private const string DeathPath = KnightFolder + "Knight@Death.fbx";
        // Las tres animaciones de habilidades están hechas en Blender sobre el esqueleto de Mixamo.
        private const string ChargePath = KnightFolder + "Knight@Charge.fbx";         // embestida: estocada baja
        private const string SpinPath = KnightFolder + "Knight@Spin.fbx";             // giro: pose con la espada extendida, en bucle
        private const string SpinFinishPath = KnightFolder + "Knight@SpinFinish.fbx"; // remate del giro: clava la espada en el suelo
        private const string HitGutPath = KnightFolder + "Knight@HitGut.fbx";     // Standing React Large Gut
        private const string HitRightPath = KnightFolder + "Knight@HitRight.fbx"; // Standing React Large From Right
        private const string HitLeftPath = KnightFolder + "Knight@HitLeft.fbx";   // Standing React Large From Left
        private const string KnightTexturePath = KnightFolder + "Knight_Color.png";
        private const string ControllerPath = KnightFolder + "Knight.controller";
        private const string UpperBodyMaskPath = KnightFolder + "Knight_UpperBody.mask";

        private const string SwordPath = "Assets/_Project/Art/Models/Weapons/Sword.fbx";
        private const string SwordTexturePath = "Assets/_Project/Art/Models/Weapons/Sword_Color.png";

        private const string KnightMaterialPath = "Assets/_Project/Materials/M_Knight.mat";
        private const string SwordMaterialPath = "Assets/_Project/Materials/M_Sword.mat";

        private const string InstanceName = "Knight_Model";
        private const string SocketName = "SwordSocket";

        // ---------- Ajustes de animación (cambiar aquí y volver a ejecutar el menú) ----------
        private const int TextureMaxSize = 1024;

        // Umbral de Speed (0 a 1) en el que el blend tree ya muestra "caminar" puro.
        private const float WalkThreshold = 0.35f;
        // "Sword And Shield Run" avanza a unos 4.2 m/s y el caballero se mueve a 6 m/s: se acelera para que los pies no patinen.
        private const float RunTimeScale = 1.4f;

        // El clip "Standing Melee Combo Attack Ver. 2" trae los 3 golpes seguidos (127 fotogramas a 30 fps).
        // Cada golpe se recorta como fracción del clip completo (0 = inicio, 1 = final), quitando la preparación lenta.
        // La velocidad de cada estado hace que el impacto caiga en el hitTime de KnightCombat (0.14, 0.14 y 0.22 s).
        private const float Attack1Start = 0.151f, Attack1End = 0.294f, Attack1Speed = 1.90f;
        private const float Attack2Start = 0.310f, Attack2End = 0.492f, Attack2Speed = 2.38f;
        private const float Attack3Start = 0.500f, Attack3End = 0.786f, Attack3Speed = 2.27f;

        // En los tres archivos de Blender el primer fotograma es la pose de reposo del esqueleto (Unity la necesita
        // para crear el avatar de ese archivo) y la animación empieza en el segundo. Por eso cada clip arranca en 1 / N.
        //
        // Embestida (12 fotogramas, 0.37 s): toma impulso con la espada atrás (2 fotogramas), estocada baja que llega
        // extendida a los 0.10 s, se mantiene durante la carrera y se recoge al final. Pensada para Ability_Embestida
        // (duration 0.25 + recovery 0.15). Si cambias esos tiempos, ajusta ChargeSpeed.
        private const float ChargeStart = 1f / 12f, ChargeSpeed = 1f;

        // Giro (17 fotogramas en bucle): pose baja con la espada horizontal; SpinAbility hace girar el objeto Visual.
        private const float SpinStart = 1f / 17f;

        // Remate del giro (24 fotogramas, 0.77 s): levanta la espada, la clava en el suelo a los 0.17 s, aguanta
        // arrodillado y se levanta. Arranca cuando SpinAbility termina, que es cuando ocurre la explosión.
        // Si el jugador se mueve, el remate se corta a partir de SpinFinishMoveExit (fracción del clip).
        private const float SpinFinishStart = 1f / 24f, SpinFinishSpeed = 1f, SpinFinishMoveExit = 0.45f;

        // Reacciones al golpe: solo el sacudón inicial de cada clip (unos 0.8 s), acelerado para que dure medio segundo.
        // Van en una capa aparte que solo mueve el torso, la cabeza y los brazos: las piernas siguen corriendo.
        private const float HitGutEnd = 0.50f, HitRightEnd = 0.45f, HitLeftEnd = 0.75f;
        private const float HitSpeed = 1.5f;

        // Etiqueta de los estados de ataque y embestida. CharacterAnimationBridge no reproduce la reacción
        // al golpe mientras el Animator está en un estado con esta etiqueta.
        private const string ActionTag = "Action";
        private const string ReactionLayerName = "Reacciones";

        // Posición de la empuñadura respecto al hueso de la mano: metros hacia los dedos.
        private const float SwordAlongHand = 0.08f;

        private struct ClipSpec
        {
            public string name;
            public float start;   // fracción del clip original
            public float end;     // fracción del clip original
            public bool loop;
            public bool bakeXZ;   // true: el desplazamiento horizontal queda dentro de la pose (muerte)

            public ClipSpec(string name, float start, float end, bool loop, bool bakeXZ)
            {
                this.name = name;
                this.start = start;
                this.end = end;
                this.loop = loop;
                this.bakeXZ = bakeXZ;
            }
        }

        /// <summary>true mientras el menú está reimportando: KnightAssetWatcher no avisa de esos cambios.</summary>
        internal static bool IsBuilding { get; private set; }

        internal const string WatchedFolder = KnightFolder;
        internal const string MenuPath = "The Last Beat/Personajes/Montar caballero (importar + Animator + escena)";

        [MenuItem(MenuPath)]
        public static void BuildKnight()
        {
            IsBuilding = true;
            try
            {
                BuildKnightSteps();
            }
            finally
            {
                IsBuilding = false;
            }
        }

        private static void BuildKnightSteps()
        {
            if (AssetImporter.GetAtPath(ModelPath) == null)
            {
                EditorUtility.DisplayDialog("Caballero",
                    "No encuentro " + ModelPath + ".\nRevisa que los FBX estén en esa carpeta y que Unity ya los haya importado.", "Entendido");
                return;
            }

            Avatar avatar = ConfigureModel();
            if (avatar == null) return;

            ConfigureTexture(KnightTexturePath);
            ConfigureTexture(SwordTexturePath);

            ConfigureClips(IdlePath, avatar, new ClipSpec("Idle", 0f, 1f, true, false));
            ConfigureClips(WalkPath, avatar, new ClipSpec("Walk", 0f, 1f, true, false));
            ConfigureClips(RunPath, avatar, new ClipSpec("Run", 0f, 1f, true, false));
            ConfigureClips(DeathPath, avatar, new ClipSpec("Death", 0f, 1f, false, true));
            ConfigureClips(ComboPath, avatar,
                new ClipSpec("Attack1", Attack1Start, Attack1End, false, false),
                new ClipSpec("Attack2", Attack2Start, Attack2End, false, false),
                new ClipSpec("Attack3", Attack3Start, Attack3End, false, false));
            ConfigureBlenderClip(ChargePath, avatar, new ClipSpec("Charge", ChargeStart, 1f, false, false));
            ConfigureBlenderClip(SpinPath, avatar, new ClipSpec("Spin", SpinStart, 1f, true, false));
            ConfigureBlenderClip(SpinFinishPath, avatar, new ClipSpec("SpinFinish", SpinFinishStart, 1f, false, false));
            ConfigureClips(HitGutPath, avatar, new ClipSpec("HitGut", 0f, HitGutEnd, false, false));
            ConfigureClips(HitRightPath, avatar, new ClipSpec("HitRight", 0f, HitRightEnd, false, false));
            ConfigureClips(HitLeftPath, avatar, new ClipSpec("HitLeft", 0f, HitLeftEnd, false, false));

            Material knightMaterial = GetOrCreateMaterial(KnightMaterialPath, KnightTexturePath);
            Material swordMaterial = GetOrCreateMaterial(SwordMaterialPath, SwordTexturePath);

            AnimatorController controller = BuildController();
            if (controller == null) return;

            MountInScene(avatar, controller, knightMaterial, swordMaterial);

            // Este menú rehace Knight_Model y SwordSocket: si los efectos ya estaban montados, se vuelven a colgar.
            KnightVfxBuilder.RebuildIfMounted();

            AssetDatabase.SaveAssets();
            Debug.Log("[KnightVisualBuilder] Caballero montado. Animator: " + ControllerPath);
        }

        // ---------- 1. Importación ----------

        private static Avatar ConfigureModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[KnightVisualBuilder] " + ModelPath + " no es un modelo.");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            Avatar avatar = LoadSubAsset<Avatar>(ModelPath, null);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[KnightVisualBuilder] Unity no pudo crear el avatar Humanoid de " + ModelPath +
                               ". Ábrelo en el Inspector > Rig > Configure para ver qué hueso falta.");
                return null;
            }
            return avatar;
        }

        /// <summary>
        /// Clips que no vienen de Mixamo sino de Blender, que añade un nodo "Armature" encima del esqueleto.
        /// Por eso cada archivo usa su propio avatar (Unity pasa la animación al caballero igual, porque ambos son Humanoid).
        /// Si Unity no logra crear ese avatar, se intenta con el del caballero.
        /// </summary>
        private static void ConfigureBlenderClip(string path, Avatar knightAvatar, ClipSpec spec)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] Falta " + path + ": se omite.");
                return;
            }

            ConfigureClipsCore(path, null, true, new[] { spec });

            Avatar own = LoadSubAsset<Avatar>(path, null);
            if (own != null && own.isValid && own.isHuman) return;

            Debug.LogWarning("[KnightVisualBuilder] " + path + " no generó un avatar Humanoid propio: se usa el del caballero.");
            ConfigureClipsCore(path, knightAvatar, false, new[] { spec });
        }

        private static void ConfigureClips(string path, Avatar avatar, params ClipSpec[] specs)
        {
            ConfigureClipsCore(path, avatar, false, specs);
        }

        private static void ConfigureClipsCore(string path, Avatar avatar, bool ownAvatar, ClipSpec[] specs)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] Falta " + path + ": se omite.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            if (ownAvatar)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
            }
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0)
            {
                // Primera importación sin animación leída todavía: reimportar y volver a consultar.
                importer.SaveAndReimport();
                takes = importer.defaultClipAnimations;
            }
            if (takes == null || takes.Length == 0)
            {
                Debug.LogWarning("[KnightVisualBuilder] " + path + " no trae ninguna animación.");
                return;
            }

            ModelImporterClipAnimation take = takes[0];
            float first = take.firstFrame;
            float length = take.lastFrame - take.firstFrame;

            ModelImporterClipAnimation[] clips = new ModelImporterClipAnimation[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                ClipSpec spec = specs[i];
                ModelImporterClipAnimation clip = new ModelImporterClipAnimation();
                clip.name = spec.name;
                clip.takeName = take.takeName;
                clip.firstFrame = Mathf.Round(first + length * Mathf.Clamp01(spec.start));
                clip.lastFrame = Mathf.Round(first + length * Mathf.Clamp01(spec.end));
                clip.loopTime = spec.loop;
                clip.loopPose = spec.loop;

                // El personaje lo mueve el CharacterController, no la animación:
                // rotación y altura quedan dentro de la pose; el avance horizontal se descarta
                // (Animator.applyRootMotion = false), salvo en la muerte, donde cae hacia atrás.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = spec.bakeXZ;
                clip.keepOriginalPositionXZ = true;

                clips[i] = clip;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.maxTextureSize == TextureMaxSize) return;

            importer.maxTextureSize = TextureMaxSize;
            importer.SaveAndReimport();
        }

        // ---------- 2. Materiales y Animator ----------

        private static Material GetOrCreateMaterial(string path, string texturePath)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[KnightVisualBuilder] No encuentro el shader 'Universal Render Pipeline/Lit'.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) Debug.LogWarning("[KnightVisualBuilder] Falta la textura " + texturePath + ".");

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildController()
        {
            AnimationClip idle = LoadSubAsset<AnimationClip>(IdlePath, "Idle");
            AnimationClip walk = LoadSubAsset<AnimationClip>(WalkPath, "Walk");
            AnimationClip run = LoadSubAsset<AnimationClip>(RunPath, "Run");
            AnimationClip death = LoadSubAsset<AnimationClip>(DeathPath, "Death");
            AnimationClip attack1 = LoadSubAsset<AnimationClip>(ComboPath, "Attack1");
            AnimationClip attack2 = LoadSubAsset<AnimationClip>(ComboPath, "Attack2");
            AnimationClip attack3 = LoadSubAsset<AnimationClip>(ComboPath, "Attack3");
            AnimationClip charge = LoadSubAsset<AnimationClip>(ChargePath, "Charge");
            AnimationClip spin = LoadSubAsset<AnimationClip>(SpinPath, "Spin");
            AnimationClip spinFinish = LoadSubAsset<AnimationClip>(SpinFinishPath, "SpinFinish");
            AnimationClip hitGut = LoadSubAsset<AnimationClip>(HitGutPath, "HitGut");
            AnimationClip hitRight = LoadSubAsset<AnimationClip>(HitRightPath, "HitRight");
            AnimationClip hitLeft = LoadSubAsset<AnimationClip>(HitLeftPath, "HitLeft");

            if (idle == null)
            {
                Debug.LogError("[KnightVisualBuilder] No se pudo leer el clip Idle de " + IdlePath + ". Sin él no se crea el Animator.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack3", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Charge", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Spin", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("SpinEnd", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitType", AnimatorControllerParameterType.Int); // 0 = frente, 1 = desde la derecha, 2 = desde la izquierda
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Locomoción: Idle (0) -> Walk -> Run (1), según el parámetro Speed que escribe PlayerController.
            BlendTree tree;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, WalkThreshold);
            if (run != null) tree.AddChild(run, 1f);

            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion == run) children[i].timeScale = RunTimeScale;
            }
            tree.children = children;
            machine.defaultState = locomotion;

            // La muerte se añade primero para que gane si coincide con otro trigger en el mismo fotograma.
            if (death != null)
            {
                AnimatorState die = machine.AddState("Die");
                die.motion = death;
                AnimatorStateTransition toDie = machine.AddAnyStateTransition(die);
                toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                toDie.hasExitTime = false;
                toDie.hasFixedDuration = true;
                toDie.duration = 0.1f;
                toDie.canTransitionToSelf = false;
            }

            AddActionState(machine, locomotion, "Attack1", attack1, Attack1Speed, "Attack1", -1);
            AddActionState(machine, locomotion, "Attack2", attack2, Attack2Speed, "Attack2", -1);
            AddActionState(machine, locomotion, "Attack3", attack3, Attack3Speed, "Attack3", -1);
            AddActionState(machine, locomotion, "Charge", charge, ChargeSpeed, "Charge", -1);

            // Giro: la pose se mantiene en bucle mientras SpinAbility rota el objeto Visual. Cuando la habilidad
            // avisa que terminó (trigger SpinEnd) pasa al remate, y de ahí a Locomotion. Sin remate, vuelve directo.
            if (spin != null)
            {
                AnimatorState spinState = machine.AddState("Spin");
                spinState.motion = spin;
                spinState.tag = ActionTag;

                AnimatorStateTransition toSpin = machine.AddAnyStateTransition(spinState);
                toSpin.AddCondition(AnimatorConditionMode.If, 0f, "Spin");
                toSpin.hasExitTime = false;
                toSpin.hasFixedDuration = true;
                toSpin.duration = 0.08f;
                toSpin.canTransitionToSelf = false;

                AnimatorState afterSpin = locomotion;
                if (spinFinish != null)
                {
                    AnimatorState finishState = machine.AddState("SpinFinish");
                    finishState.motion = spinFinish;
                    finishState.speed = SpinFinishSpeed;
                    finishState.tag = ActionTag;

                    // Primero la salida anticipada: si el jugador ya se está moviendo, no espera a que se levante.
                    AnimatorStateTransition moveOut = finishState.AddTransition(locomotion);
                    moveOut.AddCondition(AnimatorConditionMode.Greater, 0.2f, "Speed");
                    moveOut.hasExitTime = true;
                    moveOut.exitTime = SpinFinishMoveExit;
                    moveOut.hasFixedDuration = true;
                    moveOut.duration = 0.15f;

                    AnimatorStateTransition finishOut = finishState.AddTransition(locomotion);
                    finishOut.hasExitTime = true;
                    finishOut.exitTime = 0.92f;
                    finishOut.hasFixedDuration = true;
                    finishOut.duration = 0.12f;

                    afterSpin = finishState;
                }
                else
                {
                    Debug.LogWarning("[KnightVisualBuilder] Falta el clip SpinFinish: el giro vuelve directo a Locomotion.");
                }

                AnimatorStateTransition fromSpin = spinState.AddTransition(afterSpin);
                fromSpin.AddCondition(AnimatorConditionMode.If, 0f, "SpinEnd");
                fromSpin.hasExitTime = false;
                fromSpin.hasFixedDuration = true;
                fromSpin.duration = spinFinish != null ? 0.06f : 0.15f;
            }
            else
            {
                Debug.LogWarning("[KnightVisualBuilder] Falta el clip Spin: no se crea ese estado.");
            }

            BuildReactionLayer(controller, hitGut, hitRight, hitLeft);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Estado al que se entra desde cualquier otro con un trigger y que vuelve solo al estado de reposo al terminar.
        /// Con hitType >= 0 es una reacción al golpe: además exige que el parámetro HitType valga ese número.
        /// Con hitType = -1 es un ataque o habilidad y lleva la etiqueta "Action".
        /// </summary>
        private static void AddActionState(AnimatorStateMachine machine, AnimatorState restState, string stateName,
                                           AnimationClip clip, float speed, string trigger, int hitType)
        {
            if (clip == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] Falta el clip " + stateName + ": no se crea ese estado.");
                return;
            }

            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speed = speed;
            if (hitType < 0) state.tag = ActionTag;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (hitType >= 0) enter.AddCondition(AnimatorConditionMode.Equals, hitType, "HitType");
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = true;

            AnimatorStateTransition exit = state.AddTransition(restState);
            exit.hasExitTime = true;
            exit.exitTime = hitType >= 0 ? 0.8f : 0.9f;
            exit.hasFixedDuration = true;
            exit.duration = hitType >= 0 ? 0.15f : 0.12f;
        }

        /// <summary>
        /// Capa "Reacciones": solo torso, cabeza y brazos. Por defecto está en un estado vacío (no cambia nada)
        /// y con el trigger Hit reproduce la reacción que indique HitType. Si falta algún clip, usa otro en su lugar
        /// para que el trigger siempre se consuma.
        /// </summary>
        private static void BuildReactionLayer(AnimatorController controller, AnimationClip gut, AnimationClip right, AnimationClip left)
        {
            AnimationClip fallback = gut;
            if (fallback == null) fallback = right;
            if (fallback == null) fallback = left;
            if (fallback == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] No hay clips de reacción al golpe: no se crea la capa " + ReactionLayerName + ".");
                return;
            }
            if (gut == null) gut = fallback;
            if (right == null) right = fallback;
            if (left == null) left = fallback;

            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            EditorUtility.SetDirty(mask);

            AnimatorStateMachine machine = new AnimatorStateMachine();
            machine.name = ReactionLayerName;
            machine.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(machine, controller);

            AnimatorControllerLayer layer = new AnimatorControllerLayer();
            layer.name = ReactionLayerName;
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.avatarMask = mask;
            layer.stateMachine = machine;
            controller.AddLayer(layer);

            AnimatorState none = machine.AddState("Sin reaccion");
            machine.defaultState = none;

            AddActionState(machine, none, "HitGut", gut, HitSpeed, "Hit", 0);
            AddActionState(machine, none, "HitRight", right, HitSpeed, "Hit", 1);
            AddActionState(machine, none, "HitLeft", left, HitSpeed, "Hit", 2);
        }

        // ---------- 3. Escena ----------

        private static void MountInScene(Avatar avatar, AnimatorController controller, Material knightMaterial, Material swordMaterial)
        {
            PlayerController player = FindPlayer();
            if (player == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] No hay ningún PlayerController en la escena abierta: " +
                                 "se crearon el Animator y los materiales, pero no se montó el modelo.");
                return;
            }

            GameObject knight = player.gameObject;

            Transform visual = knight.transform.Find("Visual");
            if (visual == null)
            {
                GameObject visualObject = new GameObject("Visual");
                Undo.RegisterCreatedObjectUndo(visualObject, "Montar caballero");
                visualObject.transform.SetParent(knight.transform, false);
                visual = visualObject.transform;
            }

            // Quita un montaje anterior y apaga las piezas de prueba (cápsula y "Frente"). No se borran.
            // Si ya habías acomodado la espada a mano (SwordSocket), se conserva ese ajuste.
            Transform previous = visual.Find(InstanceName);
            bool keepSocket = false;
            Vector3 socketPosition = Vector3.zero;
            Quaternion socketRotation = Quaternion.identity;
            Vector3 socketScale = Vector3.one;
            if (previous != null)
            {
                Transform oldSocket = FindDeep(previous, SocketName);
                if (oldSocket != null)
                {
                    keepSocket = true;
                    socketPosition = oldSocket.localPosition;
                    socketRotation = oldSocket.localRotation;
                    socketScale = oldSocket.localScale;
                }
                Undo.DestroyObjectImmediate(previous.gameObject);
            }

            for (int i = 0; i < visual.childCount; i++)
            {
                GameObject child = visual.GetChild(i).gameObject;
                if (!child.activeSelf) continue;
                Undo.RecordObject(child, "Montar caballero");
                child.SetActive(false);
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, visual);
            Undo.RegisterCreatedObjectUndo(model, "Montar caballero");
            model.name = InstanceName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (knightMaterial != null)
            {
                SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skins.Length; i++) skins[i].sharedMaterial = knightMaterial;
            }

            Transform socket = MountSword(knight.transform, model.transform, animator, swordMaterial);
            if (socket != null && keepSocket)
            {
                socket.localPosition = socketPosition;
                socket.localRotation = socketRotation;
                socket.localScale = socketScale;
            }

            SetReference(player, "animator", animator);
            SetReference(knight.GetComponent<KnightCombat>(), "animator", animator);

            CharacterAnimationBridge bridge = knight.GetComponent<CharacterAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<CharacterAnimationBridge>(knight);
            SetReference(bridge, "animator", animator);

            // La embestida dispara "Charge" al lanzarse; el giro dispara "Spin" al empezar y "SpinEnd" al terminar.
            ChargeAbility chargeAbility = knight.GetComponent<ChargeAbility>();
            SpinAbility spinAbility = knight.GetComponent<SpinAbility>();
            int entryCount = (chargeAbility != null ? 1 : 0) + (spinAbility != null ? 1 : 0);
            if (entryCount > 0)
            {
                AbilityAnimationBridge abilityBridge = knight.GetComponent<AbilityAnimationBridge>();
                if (abilityBridge == null) abilityBridge = Undo.AddComponent<AbilityAnimationBridge>(knight);

                SerializedObject serialized = new SerializedObject(abilityBridge);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                SerializedProperty entries = serialized.FindProperty("entries");
                entries.arraySize = entryCount;

                int next = 0;
                if (chargeAbility != null) SetAbilityEntry(entries.GetArrayElementAtIndex(next++), chargeAbility, "Charge", "");
                if (spinAbility != null) SetAbilityEntry(entries.GetArrayElementAtIndex(next++), spinAbility, "Spin", "SpinEnd");
                serialized.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(knight.scene);
            Selection.activeGameObject = model;
        }

        private static Transform MountSword(Transform knightRoot, Transform model, Animator animator, Material swordMaterial)
        {
            GameObject swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            if (swordAsset == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] Falta " + SwordPath + ": el caballero queda sin espada.");
                return null;
            }

            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) hand = FindDeep(model, "RightHand");
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (forearm == null) forearm = FindDeep(model, "RightForeArm");
            if (hand == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] No encuentro el hueso de la mano derecha: el caballero queda sin espada.");
                return null;
            }

            // En pose T el brazo apunta hacia la derecha del personaje. La espada se coloca con la hoja
            // hacia el frente y el filo hacia arriba y abajo, y luego se cuelga del hueso para que lo siga.
            Vector3 handDirection = forearm != null ? (hand.position - forearm.position).normalized : knightRoot.right;
            Vector3 bladeDirection = knightRoot.forward;

            GameObject socket = new GameObject(SocketName);
            socket.transform.position = hand.position + handDirection * SwordAlongHand;
            socket.transform.rotation = Quaternion.LookRotation(handDirection, bladeDirection);
            socket.transform.SetParent(hand, true);

            GameObject sword = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset, socket.transform);
            sword.name = "Sword";
            sword.transform.localPosition = Vector3.zero;

            if (swordMaterial != null)
            {
                Renderer[] renderers = sword.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = swordMaterial;
            }

            return socket.transform;
        }

        // ---------- Utilidades ----------

        private static PlayerController FindPlayer()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                PlayerController fromSelection = selected.GetComponentInParent<PlayerController>();
                if (fromSelection != null) return fromSelection;
            }

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                PlayerController found = roots[i].GetComponentInChildren<PlayerController>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetAbilityEntry(SerializedProperty entry, Ability ability, string trigger, string finishedTrigger)
        {
            entry.FindPropertyRelative("ability").objectReferenceValue = ability;
            entry.FindPropertyRelative("trigger").stringValue = trigger;
            entry.FindPropertyRelative("finishedTrigger").stringValue = finishedTrigger;
        }

        private static void SetReference(Object component, string propertyName, Object value)
        {
            if (component == null) return;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[KnightVisualBuilder] " + component.GetType().Name + " no tiene el campo '" + propertyName + "'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>Busca un sub-recurso dentro de un FBX. Con name = null devuelve el primero de ese tipo.</summary>
        private static T LoadSubAsset<T>(string path, string name) where T : Object
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                T candidate = assets[i] as T;
                if (candidate == null) continue;
                if (candidate.name.StartsWith("__preview__")) continue;
                if (name == null || candidate.name == name) return candidate;
            }
            return null;
        }

        /// <summary>Busca en toda la jerarquía un hijo cuyo nombre termine así ("mixamorig:RightHand" termina en "RightHand").</summary>
        private static Transform FindDeep(Transform root, string nameEnding)
        {
            if (root.name.EndsWith(nameEnding)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), nameEnding);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>
    /// Avisa en la consola cuando un FBX de animación del caballero se reemplaza y queda con una configuración de
    /// importación que ya no corresponde a su contenido (o sin configurar). En ese caso el Animator reproduce
    /// fotogramas equivocados hasta que se vuelve a ejecutar el menú "Montar caballero".
    /// </summary>
    public class KnightAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                                                   string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (KnightVisualBuilder.IsBuilding) return;

            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (!path.StartsWith(KnightVisualBuilder.WatchedFolder) || !path.EndsWith(".fbx")) continue;
                if (!path.Contains("@")) continue;
                if (!IsOutOfDate(path)) continue;

                Debug.LogWarning("[KnightVisualBuilder] " + path + " cambió y su configuración de importación ya no coincide. " +
                                 "Ejecuta el menú \"" + KnightVisualBuilder.MenuPath + "\" antes de probar el juego.");
                return;
            }
        }

        private static bool IsOutOfDate(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;
            if (importer.animationType != ModelImporterAnimationType.Human) return true;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return true;
            if (takes == null || takes.Length == 0) return false;

            // El clip configurado apunta a una toma que ya no existe o a fotogramas fuera del archivo nuevo.
            if (clips[0].takeName != takes[0].takeName) return true;
            return clips[clips.Length - 1].lastFrame > takes[0].lastFrame + 0.5f;
        }
    }
}
