using System.IO;
using TheLastBeat.Characters;
using TheLastBeat.DebugTools;
using TheLastBeat.Heroes;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta la bardo de Mixamo en la escena abierta desde el menú "The Last Beat" (tarea 28):
    ///  1. Configura la importación de Bard.fbx (rig Humanoid) y de los clips hechos en Blender: tocar el laúd,
    ///     la Nota (ataque básico), el Golpe de bajo y la Pista de baile.
    ///  2. Crea los materiales y un Animator de cinco capas:
    ///       - Base: reposo, caminar, correr y muerte, reutilizados del caballero (ambos son Humanoid).
    ///       - Tocar: solo torso, cabeza y brazos, con el bucle del laúd. SongAnimation le sube el peso mientras
    ///         SongBuff está en Singing, así que también toca mientras corre.
    ///       - Ataque: la Nota (trigger "Note"), solo tren superior, para que pueda disparar caminando.
    ///       - Habilidades: Golpe de bajo (trigger "BassDrop") y Pista de baile (trigger "DanceFloor"), cuerpo completo.
    ///       - Reacciones: los golpes del caballero, igual que en su Animator.
    ///  3. Reemplaza las piezas de prueba por el modelo, cuelga el laúd del pecho y conecta el Animator a
    ///     BardCompanion, CharacterAnimationBridge, SongAnimation y LuteHandIK (que mueve el laúd y las manos).
    /// Requiere los clips del caballero ya importados ("Montar caballero"). Se puede ejecutar varias veces.
    /// Los efectos van aparte, en "Montar efectos de la bardo" (BardVfxBuilder); si ya están montados, se rehacen solos.
    /// Es código de editor: no va en la build.
    /// </summary>
    public static class BardVisualBuilder
    {
        // ---------- Archivos ----------
        private const string BardFolder = "Assets/_Project/Art/Models/Bard/";
        private const string ModelPath = BardFolder + "Bard.fbx";
        private const string PlayPath = BardFolder + "Bard@Play.fbx";   // bucle de tocar, hecho en Blender sobre su esqueleto
        // Acciones, hechas en Blender sobre su esqueleto siguiendo los videos de referencia.
        private const string NotePath = BardFolder + "Bard@Note.fbx";
        private const string BassDropPath = BardFolder + "Bard@BassDrop.fbx";
        private const string DanceFloorPath = BardFolder + "Bard@DanceFloor.fbx";
        private const string BardTexturePath = BardFolder + "Bard_Color.png";
        private const string ControllerPath = BardFolder + "Bard.controller";
        private const string UpperBodyMaskPath = BardFolder + "Bard_UpperBody.mask";

        private const string LutePath = "Assets/_Project/Art/Models/Weapons/Lute.fbx";
        private const string LuteTexturePath = "Assets/_Project/Art/Models/Weapons/Lute_Color.png";

        private const string BardMaterialPath = "Assets/_Project/Materials/M_BardModel.mat";
        private const string LuteMaterialPath = "Assets/_Project/Materials/M_LuteModel.mat";

        // Clips que se reutilizan del caballero.
        private const string KnightFolder = "Assets/_Project/Art/Models/Knight/";
        private const string IdlePath = KnightFolder + "Knight@Idle.fbx";
        private const string WalkPath = KnightFolder + "Knight@Walk.fbx";
        private const string RunPath = KnightFolder + "Knight@Run.fbx";
        private const string DeathPath = KnightFolder + "Knight@Death.fbx";
        private const string HitGutPath = KnightFolder + "Knight@HitGut.fbx";
        private const string HitRightPath = KnightFolder + "Knight@HitRight.fbx";
        private const string HitLeftPath = KnightFolder + "Knight@HitLeft.fbx";

        private const string InstanceName = "Bard_Model";
        private const string SocketName = "LuteSocket";
        private const string PlayLayerName = "Tocar";            // el mismo nombre que usa SongAnimation
        private const string ReactionLayerName = "Reacciones";
        private const string AttackLayerName = "Ataque";         // los mismos nombres que usa LuteHandIK
        private const string AbilityLayerName = "Habilidades";
        private const string ActionRestState = "Sin accion";

        // ---------- Ajustes (cambiar aquí y volver a ejecutar el menú) ----------
        private const int TextureMaxSize = 1024;

        // true: shader Unlit, que muestra la textura sin sombreado encima (la cara de pocos polígonos se ve limpia).
        // false: shader Lit, como el caballero; reacciona a la luz de la escena pero marca las facetas de la cara.
        private const bool UseUnlitShader = true;

        private const float WalkThreshold = 0.35f;
        // El clip de correr avanza a unos 4.2 m/s y la bardo se mueve a 5.5 m/s (NavMeshAgent).
        private const float RunTimeScale = 1.3f;

        // Bard@Play.fbx: el primer fotograma es la pose de reposo y el bucle son los 17 siguientes.
        private const float PlayStart = 1f / 17f;

        // Clips de acción: el primer fotograma es la pose de reposo y la animación va del 1 al último.
        // Las trayectorias de BardLuteTracks tienen un punto por cada uno de esos fotogramas.
        private const float NoteStart = 1f / 15f;
        private const float BassDropStart = 1f / 37f;
        private const float DanceFloorStart = 1f / 41f;

        // Duración de cada clip de acción y el instante de su efecto, en segundos a velocidad 1.
        internal const float NoteLength = 14f / 30f;        // 0,47 s
        internal const float NoteMoment = 6f / 30f;         // 0,20 s: sale la nota
        internal const float BassDropLength = 36f / 30f;    // 1,20 s
        internal const float BassDropMoment = 14f / 30f;    // 0,47 s: pisa y sale la onda
        internal const float DanceFloorLength = 40f / 30f;  // 1,33 s
        internal const float DanceFloorMoment = 21f / 30f;  // 0,70 s: cae, rasguea y se abre la pista

        // Velocidad de cada acción (1 = como se animó). Los menús de efectos y de combate leen estas constantes
        // para ajustar sus tiempos, así que después de cambiarlas hay que volver a ejecutar los tres menús.
        internal const float NoteSpeed = 1f;
        // 1,2: el pisotón llega a los 0,39 s, antes de que un soldado termine de preparar su golpe (0,55 s).
        internal const float BassDropSpeed = 1.2f;
        internal const float DanceFloorSpeed = 1f;
        private const float ActionBlendIn = 0.06f;
        private const float ActionBlendOut = 0.12f;

        // Añade a la bardo el probador de teclado (7 = Nota, 8 = Golpe de bajo, 9 = Pista de baile).
        // Ponlo en false cuando el ataque y las habilidades ya disparen los triggers por su cuenta.
        private static readonly bool AddAnimationTester = true;

        // Los golpes ya vienen recortados desde la importación del caballero; aquí solo se elige la velocidad.
        private const float HitSpeed = 1.5f;

        // Posición del laúd respecto a la raíz del modelo, en metros y en ejes de Unity (X derecha, Y arriba, Z frente).
        // Son los mismos valores con los que se hizo la animación en Blender: si los cambias, las manos dejan de coincidir.
        private static readonly Vector3 LuteBodyCenter = new Vector3(0.07f, 0.98f, 0.2596f);   // centro de la caja, frente al vientre
        private static readonly Vector3 LuteNeckDirection = new Vector3(-0.7986f, 0.5989f, 0.0599f); // el mástil sube hacia su izquierda
        private static readonly Vector3 LuteFrontDirection = new Vector3(0f, 0.22f, 1f);       // las cuerdas miran al frente y un poco arriba
        private const float LuteBodyToPivot = 0.345f;   // del centro de la caja al pivote del modelo (mitad del mástil)

        // Marcas de las muñecas para LuteHandIK, en ejes locales de LuteSocket (X a lo ancho del mástil, Y a lo largo
        // del mástil, Z hacia fuera de las cuerdas). Medidas sobre la animación de Blender.
        private const string LeftGripName = "Grip_Left";
        private const string RightGripName = "Grip_Right";
        private static readonly Vector3 LeftGripPosition = new Vector3(-0.082f, -0.015f, 0f);        // bajo el mástil
        private static readonly Vector3 RightGripPosition = new Vector3(0.0817f, -0.3225f, 0.1118f); // sobre las cuerdas
        private static readonly Vector3 StrumOffset = new Vector3(-0.0383f, 0.0025f, -0.0132f);      // vaivén del rasgueo

        private struct ClipSpec
        {
            public string name;
            public float start;
            public float end;
            public bool loop;

            public ClipSpec(string name, float start, float end, bool loop)
            {
                this.name = name;
                this.start = start;
                this.end = end;
                this.loop = loop;
            }
        }

        [MenuItem("The Last Beat/Personajes/Montar bardo (importar + Animator + escena)")]
        public static void BuildBard()
        {
            if (AssetImporter.GetAtPath(ModelPath) == null)
            {
                EditorUtility.DisplayDialog("Bardo",
                    "No encuentro " + ModelPath + ".\nRevisa que los FBX estén en esa carpeta y que Unity ya los haya importado.", "Entendido");
                return;
            }
            if (LoadClip(IdlePath, "Idle") == null)
            {
                EditorUtility.DisplayDialog("Bardo",
                    "La bardo reutiliza las animaciones del caballero y no encuentro el clip Idle en " + IdlePath +
                    ".\nEjecuta primero \"Montar caballero\".", "Entendido");
                return;
            }

            Avatar avatar = ConfigureModel();
            if (avatar == null) return;

            ConfigureTexture(BardTexturePath);
            ConfigureTexture(LuteTexturePath);
            ConfigurePlayClip(avatar);
            ConfigureActionClip(NotePath, "Note", NoteStart, avatar);
            ConfigureActionClip(BassDropPath, "BassDrop", BassDropStart, avatar);
            ConfigureActionClip(DanceFloorPath, "DanceFloor", DanceFloorStart, avatar);

            Material bardMaterial = WriteMaterial(BardMaterialPath, BardTexturePath);
            Material luteMaterial = WriteMaterial(LuteMaterialPath, LuteTexturePath);

            AnimatorController controller = BuildController();
            if (controller == null) return;

            MountInScene(avatar, controller, bardMaterial, luteMaterial);

            AssetDatabase.SaveAssets();
            Debug.Log("[BardVisualBuilder] Bardo montada. Animator: " + ControllerPath);
        }

        // ---------- 1. Importación ----------

        private static Avatar ConfigureModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[BardVisualBuilder] " + ModelPath + " no es un modelo.");
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
                Debug.LogError("[BardVisualBuilder] Unity no pudo crear el avatar Humanoid de " + ModelPath +
                               ". Ábrelo en el Inspector > Rig > Configure para ver qué hueso falta.");
                return null;
            }
            return avatar;
        }

        /// <summary>
        /// El clip de tocar viene de Blender, que añade un nodo "Armature" encima del esqueleto: usa su propio avatar
        /// y, si Unity no logra crearlo, el de la bardo.
        /// </summary>
        private static void ConfigurePlayClip(Avatar bardAvatar)
        {
            if (AssetImporter.GetAtPath(PlayPath) == null)
            {
                Debug.LogWarning("[BardVisualBuilder] Falta " + PlayPath + ": la bardo queda sin animación de tocar.");
                return;
            }

            ClipSpec spec = new ClipSpec("Play", PlayStart, 1f, true);
            ConfigureClip(PlayPath, null, true, spec, false);

            Avatar own = LoadSubAsset<Avatar>(PlayPath, null);
            if (own != null && own.isValid && own.isHuman) return;

            Debug.LogWarning("[BardVisualBuilder] " + PlayPath + " no generó un avatar Humanoid propio: se usa el de la bardo.");
            ConfigureClip(PlayPath, bardAvatar, false, spec, false);
        }

        /// <summary>
        /// Clips de acción (Nota, Golpe de bajo, Pista de baile): no hacen bucle y llevan el movimiento de la cadera
        /// dentro de la pose, para que los pies queden plantados donde se animaron. Igual que el de tocar, usan su
        /// propio avatar y, si Unity no logra crearlo, el de la bardo.
        /// </summary>
        private static void ConfigureActionClip(string path, string clipName, float start, Avatar bardAvatar)
        {
            if (AssetImporter.GetAtPath(path) == null)
            {
                Debug.LogWarning("[BardVisualBuilder] Falta " + path + ": la bardo queda sin la animación " + clipName + ".");
                return;
            }

            ClipSpec spec = new ClipSpec(clipName, start, 1f, false);
            ConfigureClip(path, null, true, spec, true);

            Avatar own = LoadSubAsset<Avatar>(path, null);
            if (own != null && own.isValid && own.isHuman) return;

            Debug.LogWarning("[BardVisualBuilder] " + path + " no generó un avatar Humanoid propio: se usa el de la bardo.");
            ConfigureClip(path, bardAvatar, false, spec, true);
        }

        /// <param name="bakePositionXZ">true: el desplazamiento horizontal de la cadera queda dentro de la pose.</param>
        private static void ConfigureClip(string path, Avatar avatar, bool ownAvatar, ClipSpec spec, bool bakePositionXZ)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

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
                importer.SaveAndReimport();
                takes = importer.defaultClipAnimations;
            }
            if (takes == null || takes.Length == 0)
            {
                Debug.LogWarning("[BardVisualBuilder] " + path + " no trae ninguna animación.");
                return;
            }

            ModelImporterClipAnimation take = takes[0];
            float first = take.firstFrame;
            float length = take.lastFrame - take.firstFrame;

            ModelImporterClipAnimation clip = new ModelImporterClipAnimation();
            clip.name = spec.name;
            clip.takeName = take.takeName;
            clip.firstFrame = Mathf.Round(first + length * Mathf.Clamp01(spec.start));
            clip.lastFrame = Mathf.Round(first + length * Mathf.Clamp01(spec.end));
            clip.loopTime = spec.loop;
            clip.loopPose = spec.loop;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = bakePositionXZ;
            clip.keepOriginalPositionXZ = true;

            importer.clipAnimations = new[] { clip };
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

        private static Material WriteMaterial(string path, string texturePath)
        {
            string shaderName = UseUnlitShader ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[BardVisualBuilder] No encuentro el shader '" + shaderName + "'.");
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
            if (texture == null) Debug.LogWarning("[BardVisualBuilder] Falta la textura " + texturePath + ".");

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
            AnimationClip idle = LoadClip(IdlePath, "Idle");
            AnimationClip walk = LoadClip(WalkPath, "Walk");
            AnimationClip run = LoadClip(RunPath, "Run");
            AnimationClip death = LoadClip(DeathPath, "Death");
            AnimationClip hitGut = LoadClip(HitGutPath, "HitGut");
            AnimationClip hitRight = LoadClip(HitRightPath, "HitRight");
            AnimationClip hitLeft = LoadClip(HitLeftPath, "HitLeft");
            AnimationClip play = LoadClip(PlayPath, "Play");
            AnimationClip note = LoadClip(NotePath, "Note");
            AnimationClip bassDrop = LoadClip(BassDropPath, "BassDrop");
            AnimationClip danceFloor = LoadClip(DanceFloorPath, "DanceFloor");

            if (idle == null)
            {
                Debug.LogError("[BardVisualBuilder] No se pudo leer el clip Idle del caballero. Sin él no se crea el Animator.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);   // lo escribe BardCompanion
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitType", AnimatorControllerParameterType.Int);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Note", AnimatorControllerParameterType.Trigger);         // ataque básico
            controller.AddParameter("BassDrop", AnimatorControllerParameterType.Trigger);     // habilidad 1: Golpe de bajo
            controller.AddParameter("DanceFloor", AnimatorControllerParameterType.Trigger);   // habilidad 2: Pista de baile

            // ----- Capa base: locomoción y muerte -----
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

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

            AvatarMask mask = WriteUpperBodyMask();

            // ----- Capa "Tocar": el bucle del laúd, con peso 0 hasta que SongAnimation lo sube -----
            if (play != null)
            {
                AnimatorStateMachine playMachine = AddLayer(controller, PlayLayerName, mask, 0f);
                AnimatorState playState = playMachine.AddState("Play");
                playState.motion = play;
                playMachine.defaultState = playState;
            }
            else
            {
                Debug.LogWarning("[BardVisualBuilder] Falta el clip Play: no se crea la capa " + PlayLayerName + ".");
            }

            // ----- Capa "Ataque": la Nota, solo tren superior, para que pueda disparar mientras camina -----
            if (note != null)
            {
                AnimatorStateMachine attackMachine = AddLayer(controller, AttackLayerName, mask, 1f);
                AnimatorState attackRest = attackMachine.AddState(ActionRestState);
                attackMachine.defaultState = attackRest;
                AddActionState(attackMachine, attackRest, "Note", note, NoteSpeed);
                AddDeathExit(attackMachine, attackRest);
            }
            else
            {
                Debug.LogWarning("[BardVisualBuilder] Falta el clip Note: no se crea la capa " + AttackLayerName + ".");
            }

            // ----- Capa "Habilidades": cuerpo completo (sin máscara), porque pisa, gira y se agacha -----
            if (bassDrop != null || danceFloor != null)
            {
                AnimatorStateMachine abilityMachine = AddLayer(controller, AbilityLayerName, null, 1f);
                AnimatorState abilityRest = abilityMachine.AddState(ActionRestState);
                abilityMachine.defaultState = abilityRest;
                if (bassDrop != null) AddActionState(abilityMachine, abilityRest, "BassDrop", bassDrop, BassDropSpeed);
                if (danceFloor != null) AddActionState(abilityMachine, abilityRest, "DanceFloor", danceFloor, DanceFloorSpeed);
                AddDeathExit(abilityMachine, abilityRest);
            }
            else
            {
                Debug.LogWarning("[BardVisualBuilder] Faltan los clips BassDrop y DanceFloor: no se crea la capa " + AbilityLayerName + ".");
            }

            // ----- Capa "Reacciones": va al final para que un golpe se vea aunque esté tocando o atacando -----
            AnimationClip fallback = hitGut;
            if (fallback == null) fallback = hitRight;
            if (fallback == null) fallback = hitLeft;
            if (fallback != null)
            {
                if (hitGut == null) hitGut = fallback;
                if (hitRight == null) hitRight = fallback;
                if (hitLeft == null) hitLeft = fallback;

                AnimatorStateMachine hitMachine = AddLayer(controller, ReactionLayerName, mask, 1f);
                AnimatorState none = hitMachine.AddState("Sin reaccion");
                hitMachine.defaultState = none;
                AddHitState(hitMachine, none, "HitGut", hitGut, 0);
                AddHitState(hitMachine, none, "HitRight", hitRight, 1);
                AddHitState(hitMachine, none, "HitLeft", hitLeft, 2);
            }
            else
            {
                Debug.LogWarning("[BardVisualBuilder] No hay clips de reacción al golpe del caballero: no se crea la capa " + ReactionLayerName + ".");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AvatarMask WriteUpperBodyMask()
        {
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
            return mask;
        }

        private static AnimatorStateMachine AddLayer(AnimatorController controller, string name, AvatarMask mask, float weight)
        {
            AnimatorStateMachine machine = new AnimatorStateMachine();
            machine.name = name;
            machine.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(machine, controller);

            AnimatorControllerLayer layer = new AnimatorControllerLayer();
            layer.name = name;
            layer.defaultWeight = weight;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.avatarMask = mask;
            layer.stateMachine = machine;
            controller.AddLayer(layer);
            return machine;
        }

        /// <summary>
        /// Estado de una acción: entra desde cualquier estado con el trigger que se llama igual que el estado y,
        /// al acabar el clip, vuelve solo al estado vacío de la capa (que deja ver las capas de abajo).
        /// </summary>
        private static void AddActionState(AnimatorStateMachine machine, AnimatorState restState, string stateName, AnimationClip clip, float speed)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speed = speed;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, stateName);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = ActionBlendIn;
            enter.canTransitionToSelf = true;

            // La mezcla de salida termina justo con el último fotograma, para que LuteHandIK recorra toda la trayectoria.
            float seconds = clip.length / Mathf.Max(0.01f, speed);
            AnimatorStateTransition exit = state.AddTransition(restState);
            exit.hasExitTime = true;
            exit.exitTime = Mathf.Clamp01(1f - ActionBlendOut / Mathf.Max(ActionBlendOut, seconds));
            exit.hasFixedDuration = true;
            exit.duration = ActionBlendOut;
        }

        /// <summary>Si muere a mitad de una acción, la capa vuelve a su estado vacío y se ve la animación de muerte.</summary>
        private static void AddDeathExit(AnimatorStateMachine machine, AnimatorState restState)
        {
            AnimatorStateTransition toRest = machine.AddAnyStateTransition(restState);
            toRest.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            toRest.hasExitTime = false;
            toRest.hasFixedDuration = true;
            toRest.duration = 0.1f;
            toRest.canTransitionToSelf = false;
        }

        private static void AddHitState(AnimatorStateMachine machine, AnimatorState restState, string stateName, AnimationClip clip, int hitType)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speed = HitSpeed;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            enter.AddCondition(AnimatorConditionMode.Equals, hitType, "HitType");
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = true;

            AnimatorStateTransition exit = state.AddTransition(restState);
            exit.hasExitTime = true;
            exit.exitTime = 0.8f;
            exit.hasFixedDuration = true;
            exit.duration = 0.15f;
        }

        // ---------- 3. Escena ----------

        private static void MountInScene(Avatar avatar, AnimatorController controller, Material bardMaterial, Material luteMaterial)
        {
            BardCompanion companion = FindBard();
            if (companion == null)
            {
                Debug.LogWarning("[BardVisualBuilder] No hay ningún BardCompanion en la escena abierta: " +
                                 "se crearon el Animator y los materiales, pero no se montó el modelo.");
                return;
            }

            GameObject bard = companion.gameObject;

            Transform visual = bard.transform.Find("Visual");
            if (visual == null)
            {
                GameObject visualObject = new GameObject("Visual");
                Undo.RegisterCreatedObjectUndo(visualObject, "Montar bardo");
                visualObject.transform.SetParent(bard.transform, false);
                visual = visualObject.transform;
            }

            // Si ya habías acomodado el laúd a mano (LuteSocket), se conserva ese ajuste.
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

            // Las piezas de prueba (Cuerpo, Laud, Frente) solo dejan de dibujarse. No se apagan ni se borran,
            // porque "Cuerpo" lleva el CapsuleCollider con el que los enemigos golpean a la bardo.
            MeshRenderer[] placeholders = visual.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < placeholders.Length; i++)
            {
                if (!placeholders[i].enabled) continue;
                Undo.RecordObject(placeholders[i], "Montar bardo");
                placeholders[i].enabled = false;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, visual);
            Undo.RegisterCreatedObjectUndo(model, "Montar bardo");
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

            if (bardMaterial != null)
            {
                SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < skins.Length; i++) skins[i].sharedMaterial = bardMaterial;
            }

            Transform socket = MountLute(model.transform, animator, luteMaterial);
            if (socket != null && keepSocket)
            {
                socket.localPosition = socketPosition;
                socket.localRotation = socketRotation;
                socket.localScale = socketScale;
            }

            // BardCompanion escribe "Speed" en su Animator. Su campo "visual" se vacía: servía para acostar la cápsula
            // al morir, y ahora la muerte la hace la animación (con los dos a la vez quedaría girada dos veces).
            SetReference(companion, "animator", animator);
            SetReference(companion, "visual", null);

            CharacterAnimationBridge bridge = bard.GetComponent<CharacterAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<CharacterAnimationBridge>(bard);
            SetReference(bridge, "animator", animator);

            SongBuff song = bard.GetComponent<SongBuff>();
            if (song != null)
            {
                SongAnimation songAnimation = bard.GetComponent<SongAnimation>();
                if (songAnimation == null) songAnimation = Undo.AddComponent<SongAnimation>(bard);
                SetReference(songAnimation, "song", song);
                SetReference(songAnimation, "animator", animator);

                // Mueve el laúd en las acciones y pega las muñecas a él, después del Animator.
                if (socket != null)
                {
                    LuteHandIK handIk = bard.GetComponent<LuteHandIK>();
                    if (handIk == null) handIk = Undo.AddComponent<LuteHandIK>(bard);
                    SetReference(handIk, "animator", animator);
                    SetReference(handIk, "socket", socket);
                    SetReference(handIk, "leftGrip", socket.Find(LeftGripName));
                    SetReference(handIk, "rightGrip", socket.Find(RightGripName));
                    SerializedObject serializedIk = new SerializedObject(handIk);
                    serializedIk.FindProperty("strumOffset").vector3Value = StrumOffset;
                    serializedIk.ApplyModifiedProperties();
                }
            }
            else
            {
                Debug.LogWarning("[BardVisualBuilder] La bardo no tiene SongBuff: no se conecta la capa " + PlayLayerName + ".");
            }

            // Probador de teclado de las acciones, mientras no existan el ataque y las habilidades.
            BardAnimationTester tester = bard.GetComponent<BardAnimationTester>();
            if (AddAnimationTester)
            {
                if (tester == null) tester = Undo.AddComponent<BardAnimationTester>(bard);
                SetReference(tester, "animator", animator);
            }
            else if (tester != null)
            {
                Undo.DestroyObjectImmediate(tester);
            }

            // Los efectos cuelgan del LuteSocket que se acaba de rehacer: si ya estaban montados, se vuelven a montar.
            // El modelo es nuevo, así que su Animator también: se vuelven a enlazar el combate (que a su vez
            // rehace los efectos) o, si el combate aún no está montado, solo los efectos.
            if (!BardCombatBuilder.RebuildIfMounted()) BardVfxBuilder.RebuildIfMounted();

            EditorSceneManager.MarkSceneDirty(bard.scene);
            Selection.activeGameObject = model;
        }

        /// <summary>
        /// Cuelga el laúd del hueso del pecho, en la misma posición con la que se animaron las manos.
        /// Va en el pecho y no en la mano para que no se sacuda con el braceo cuando la canción se interrumpe.
        /// </summary>
        private static Transform MountLute(Transform model, Animator animator, Material luteMaterial)
        {
            GameObject luteAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LutePath);
            if (luteAsset == null)
            {
                Debug.LogWarning("[BardVisualBuilder] Falta " + LutePath + ": la bardo queda sin laúd.");
                return null;
            }

            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = FindDeep(model, "mixamorig:Spine2");
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null)
            {
                Debug.LogWarning("[BardVisualBuilder] No encuentro el hueso del pecho: la bardo queda sin laúd.");
                return null;
            }

            Vector3 neck = LuteNeckDirection.normalized;
            Vector3 front = (LuteFrontDirection - neck * Vector3.Dot(LuteFrontDirection, neck)).normalized;
            Vector3 pivot = LuteBodyCenter + neck * LuteBodyToPivot;

            // En el modelo del laúd, +Y es el mástil y +Z es la cara de las cuerdas.
            GameObject socket = new GameObject(SocketName);
            socket.transform.position = model.TransformPoint(pivot);
            socket.transform.rotation = model.rotation * Quaternion.LookRotation(front, neck);
            socket.transform.SetParent(chest, true);

            GameObject lute = (GameObject)PrefabUtility.InstantiatePrefab(luteAsset, socket.transform);
            lute.name = "Lute";
            lute.transform.localPosition = Vector3.zero;

            AddGrip(socket.transform, LeftGripName, LeftGripPosition);
            AddGrip(socket.transform, RightGripName, RightGripPosition);

            if (luteMaterial != null)
            {
                Renderer[] renderers = lute.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = luteMaterial;
            }

            return socket.transform;
        }

        private static void AddGrip(Transform socket, string name, Vector3 localPosition)
        {
            GameObject grip = new GameObject(name);
            grip.transform.SetParent(socket, false);
            grip.transform.localPosition = localPosition;
            grip.transform.localRotation = Quaternion.identity;
        }

        // ---------- Utilidades ----------

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

        private static void SetReference(Object component, string propertyName, Object value)
        {
            if (component == null) return;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[BardVisualBuilder] " + component.GetType().Name + " no tiene el campo '" + propertyName + "'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static AnimationClip LoadClip(string path, string name)
        {
            return LoadSubAsset<AnimationClip>(path, name);
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

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
