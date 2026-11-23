using System.IO;
using TheLastBeat.Characters;
using TheLastBeat.Enemies;
using TheLastBeat.Feedback;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta el demonio soldado de Mixamo en su prefab desde el menú "The Last Beat" (tarea 28):
    ///  1. Configura la importación: rig Humanoid con su propio avatar, la textura y el clip de ataque.
    ///  2. Crea los materiales de URP y el Animator Controller con los parámetros que usa el código:
    ///     Speed, Attack y Stagger (EnemySoldier), Hit, HitType y Die (CharacterAnimationBridge).
    ///     Reposo, caminar, correr, golpes y muerte son los clips del caballero: al ser los dos Humanoid, sirven igual.
    ///  3. Abre Prefabs/Enemies/Soldado.prefab, deja de dibujar la cápsula y los cuernos de prueba (no los borra:
    ///     "Cuerpo" lleva el collider), mete el modelo en "Visual", le pone la cimitarra en la mano derecha y conecta
    ///     el Animator a EnemySoldier, CharacterAnimationBridge y HitFlash.
    ///
    /// Los tiempos se leen del prefab: la velocidad del ataque se ajusta para que el tajo caiga justo en Windup Time
    /// y la de correr para que los pies no patinen a la velocidad del NavMeshAgent. Si cambias alguno de esos dos
    /// valores, vuelve a ejecutar el menú.
    /// Se puede ejecutar varias veces: rehace todo sin duplicar nada. Es código de editor: no va en la build.
    /// </summary>
    public static class SoldierVisualBuilder
    {
        // ---------- Archivos ----------
        private const string SoldierFolder = "Assets/_Project/Art/Models/Soldado/";
        private const string ModelPath = SoldierFolder + "Soldado.fbx";            // pose T de Mixamo, con skin
        private const string AttackPath = SoldierFolder + "Soldado@Attack.fbx";    // Sword And Shield Slash, sin skin
        private const string SoldierTexturePath = SoldierFolder + "Soldado_Color.png";
        private const string ControllerPath = SoldierFolder + "Soldado.controller";
        private const string UpperBodyMaskPath = SoldierFolder + "Soldado_UpperBody.mask";

        private const string ScimitarPath = "Assets/_Project/Art/Models/Weapons/Cimitarra.fbx";
        private const string ScimitarTexturePath = "Assets/_Project/Art/Models/Weapons/Cimitarra_Color.png";

        private const string SoldierMaterialPath = "Assets/_Project/Materials/M_SoldadoModel.mat";
        private const string ScimitarMaterialPath = "Assets/_Project/Materials/M_CimitarraModel.mat";

        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Soldado.prefab";

        // Clips del caballero que el soldado reutiliza (los configura el menú "Montar caballero").
        private const string KnightFolder = "Assets/_Project/Art/Models/Knight/";
        private const string IdlePath = KnightFolder + "Knight@Idle.fbx";
        private const string WalkPath = KnightFolder + "Knight@Walk.fbx";
        private const string RunPath = KnightFolder + "Knight@Run.fbx";
        private const string DeathPath = KnightFolder + "Knight@Death.fbx";
        private const string HitGutPath = KnightFolder + "Knight@HitGut.fbx";
        private const string HitRightPath = KnightFolder + "Knight@HitRight.fbx";
        private const string HitLeftPath = KnightFolder + "Knight@HitLeft.fbx";

        private const string VisualName = "Visual";
        private const string InstanceName = "Soldado_Model";
        private const string SocketName = "CimitarraSocket";
        private const string ScimitarName = "Cimitarra";
        private const string ReactionLayerName = "Reacciones";
        private const string Title = "Soldado";

        // ---------- Ajustes (cambiar aquí y volver a ejecutar el menú) ----------
        private const int TextureMaxSize = 1024;

        // true: shader Unlit, que muestra la textura sin sombreado encima, como la bardo.
        // false: shader Lit, como el caballero; reacciona a la luz de la escena.
        private const bool UseUnlitShader = false;

        // Velocidad a la que avanzan los clips del caballero, en metros por segundo (medida en los FBX de Mixamo).
        // Con ellas se calcula el umbral de caminar y cuánto se frena el clip de correr para la velocidad del soldado.
        private const float WalkClipSpeed = 1.06f;   // Standing Walk Forward
        private const float RunClipSpeed = 4.21f;    // Sword And Shield Run

        // Ataque, "Sword And Shield Slash" (51 fotogramas, 1,67 s): lleva la cimitarra al hombro izquierdo y suelta
        // un tajo de revés; el tajo cruza por delante del cuerpo a los 0,83 s. La velocidad del estado se calcula
        // para que ese instante caiga en Windup Time de EnemySoldier (0,55 s -> 1,5x); lo que queda del clip dura
        // entonces lo mismo que Recover Time.
        private const float AttackImpactTime = 25f / 30f;
        // true: el paso al frente del clip (medio metro, ida y vuelta) se ve en el modelo, que se abalanza sobre el
        // héroe y vuelve. false: ataca clavado en el sitio.
        private const bool AttackLunge = true;

        // Muerte, "Two Handed Sword Death" (2,4 s): queda tendido a los 1,75 s. Se acelera un poco, y el prefab
        // espera a que esté en el suelo (Sink Delay) antes de hundirlo.
        private const float DeathSpeed = 1.25f;
        private const float DeathFallTime = 1.75f;

        // Reacciones al golpe: la misma velocidad que las del caballero.
        private const float HitSpeed = 1.5f;

        // Etiqueta del ataque: CharacterAnimationBridge no reproduce la reacción al golpe mientras suena.
        private const string ActionTag = "Action";

        // Posición de la empuñadura respecto al hueso de la mano: metros hacia los dedos.
        private const float ScimitarAlongHand = 0.08f;

        internal const string MenuPath = "The Last Beat/Personajes/Montar soldado (importar + Animator + prefab)";

        [MenuItem(MenuPath)]
        public static void BuildSoldier()
        {
            if (AssetImporter.GetAtPath(ModelPath) == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "No encuentro " + ModelPath + ".\nRevisa que los FBX estén en esa carpeta y que Unity ya los haya importado.", "Entendido");
                return;
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EnemySoldier prefabSoldier = prefabAsset != null ? prefabAsset.GetComponent<EnemySoldier>() : null;
            if (prefabSoldier == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "No encuentro el prefab " + PrefabPath + " con el componente EnemySoldier.", "Entendido");
                return;
            }

            if (LoadSubAsset<AnimationClip>(IdlePath, "Idle") == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "El soldado reutiliza las animaciones del caballero y no encuentro el clip Idle en " + IdlePath +
                    ".\nEjecuta primero \"Montar caballero\".", "Entendido");
                return;
            }

            // Los dos valores del prefab con los que tiene que cuadrar la animación.
            float windupTime = ReadFloat(prefabSoldier, "windupTime", 0.55f);
            NavMeshAgent agent = prefabAsset.GetComponent<NavMeshAgent>();
            float moveSpeed = agent != null && agent.speed > 0.01f ? agent.speed : 3.2f;

            Avatar avatar = ConfigureModel();
            if (avatar == null) return;

            ConfigureTexture(SoldierTexturePath);
            ConfigureTexture(ScimitarTexturePath);
            ConfigureAttackClip(avatar);

            Material soldierMaterial = WriteMaterial(SoldierMaterialPath, SoldierTexturePath);
            Material scimitarMaterial = WriteMaterial(ScimitarMaterialPath, ScimitarTexturePath);

            AnimatorController controller = BuildController(windupTime, moveSpeed);
            if (controller == null) return;

            if (!MountInPrefab(avatar, controller, soldierMaterial, scimitarMaterial)) return;

            AssetDatabase.SaveAssets();
            Debug.Log("[SoldierVisualBuilder] Soldado montado en " + PrefabPath + ". Animator: " + ControllerPath);
            EditorUtility.DisplayDialog(Title,
                "El soldado quedó montado en su prefab. Los que saca el pool al dar Play ya usan el modelo.", "Listo");
        }

        // ---------- 1. Importación ----------

        private static Avatar ConfigureModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[SoldierVisualBuilder] " + ModelPath + " no es un modelo.");
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
                Debug.LogError("[SoldierVisualBuilder] Unity no pudo crear el avatar Humanoid de " + ModelPath +
                               ". Ábrelo en el Inspector > Rig > Configure para ver qué hueso falta.");
                return null;
            }
            return avatar;
        }

        /// <summary>El clip de ataque viene de Mixamo con el esqueleto del propio soldado: usa su avatar.</summary>
        private static void ConfigureAttackClip(Avatar avatar)
        {
            ModelImporter importer = AssetImporter.GetAtPath(AttackPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[SoldierVisualBuilder] Falta " + AttackPath + ": el soldado queda sin animación de ataque.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
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
                Debug.LogWarning("[SoldierVisualBuilder] " + AttackPath + " no trae ninguna animación.");
                return;
            }

            ModelImporterClipAnimation take = takes[0];
            ModelImporterClipAnimation clip = new ModelImporterClipAnimation();
            clip.name = "Attack";
            clip.takeName = take.takeName;
            clip.firstFrame = take.firstFrame;
            clip.lastFrame = take.lastFrame;
            clip.loopTime = false;
            clip.loopPose = false;

            // Al soldado lo mueve el NavMeshAgent, no la animación (Animator.applyRootMotion = false): rotación y
            // altura quedan dentro de la pose. El paso al frente se queda en la pose solo si AttackLunge es true;
            // como el clip termina donde empezó, el modelo vuelve solo a su sitio.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = AttackLunge;
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
                Debug.LogError("[SoldierVisualBuilder] No encuentro el shader '" + shaderName + "'.");
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
            if (texture == null) Debug.LogWarning("[SoldierVisualBuilder] Falta la textura " + texturePath + ".");

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            // Todos los soldados de la horda comparten este material: con instancing se dibujan en menos lotes.
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController BuildController(float windupTime, float moveSpeed)
        {
            AnimationClip idle = LoadSubAsset<AnimationClip>(IdlePath, "Idle");
            AnimationClip walk = LoadSubAsset<AnimationClip>(WalkPath, "Walk");
            AnimationClip run = LoadSubAsset<AnimationClip>(RunPath, "Run");
            AnimationClip death = LoadSubAsset<AnimationClip>(DeathPath, "Death");
            AnimationClip hitGut = LoadSubAsset<AnimationClip>(HitGutPath, "HitGut");
            AnimationClip hitRight = LoadSubAsset<AnimationClip>(HitRightPath, "HitRight");
            AnimationClip hitLeft = LoadSubAsset<AnimationClip>(HitLeftPath, "HitLeft");
            AnimationClip attack = LoadSubAsset<AnimationClip>(AttackPath, "Attack");

            if (idle == null)
            {
                Debug.LogError("[SoldierVisualBuilder] No se pudo leer el clip Idle de " + IdlePath + ". Sin él no se crea el Animator.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);       // lo escribe EnemySoldier
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);    // al empezar a preparar el golpe
            controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);   // golpe pesado o aturdimiento
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);       // CharacterAnimationBridge
            controller.AddParameter("HitType", AnimatorControllerParameterType.Int);       // 0 = frente, 1 = desde la derecha, 2 = desde la izquierda
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Locomoción: Idle (0) -> Walk -> Run (1). Speed es la velocidad del NavMeshAgent entre su máxima,
            // así que caminar va donde el soldado se mueve a la velocidad natural de ese clip.
            BlendTree tree;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, Mathf.Clamp(WalkClipSpeed / moveSpeed, 0.15f, 0.85f));
            if (run != null) tree.AddChild(run, 1f);

            // El clip de correr avanza más rápido que el soldado: se frena para que los pies no patinen.
            float runTimeScale = Mathf.Clamp(moveSpeed / RunClipSpeed, 0.5f, 2f);
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].motion == run) children[i].timeScale = runTimeScale;
            }
            tree.children = children;
            machine.defaultState = locomotion;

            // El orden importa: si dos triggers coinciden en el mismo fotograma gana la transición añadida primero.
            // Muerte, luego tambaleo, luego ataque.
            if (death != null)
            {
                AnimatorState die = machine.AddState("Die");
                die.motion = death;
                die.speed = DeathSpeed;

                AnimatorStateTransition toDie = machine.AddAnyStateTransition(die);
                toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                toDie.hasExitTime = false;
                toDie.hasFixedDuration = true;
                toDie.duration = 0.1f;
                toDie.canTransitionToSelf = false;
            }
            else
            {
                Debug.LogWarning("[SoldierVisualBuilder] Falta el clip Death: el soldado se hunde sin animación de muerte.");
            }

            // Tambaleo: reacción de cuerpo entero que corta el ataque. La dispara EnemySoldier cuando un golpe
            // pesado lo cancela o cuando lo aturden; en esos momentos está quieto, así que las piernas también reaccionan.
            AnimationClip staggerClip = hitGut != null ? hitGut : (hitRight != null ? hitRight : hitLeft);
            if (staggerClip != null)
            {
                AddTriggeredState(machine, locomotion, "Stagger", staggerClip, HitSpeed, "Stagger", -1, null, 0.8f, 0.15f);
            }

            if (attack != null)
            {
                float attackSpeed = windupTime > 0.05f ? Mathf.Clamp(AttackImpactTime / windupTime, 0.5f, 4f) : 1f;
                AddTriggeredState(machine, locomotion, "Attack", attack, attackSpeed, "Attack", -1, ActionTag, 0.9f, 0.12f);
            }
            else
            {
                Debug.LogWarning("[SoldierVisualBuilder] Falta el clip Attack: no se crea ese estado.");
            }

            BuildReactionLayer(controller, hitGut, hitRight, hitLeft);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Estado al que se entra desde cualquier otro con un trigger y que vuelve solo al estado de reposo al terminar.
        /// Con hitType >= 0 exige además que el parámetro HitType valga ese número.
        /// </summary>
        private static void AddTriggeredState(AnimatorStateMachine machine, AnimatorState restState, string stateName,
                                              AnimationClip clip, float speed, string trigger, int hitType, string tag,
                                              float exitTime, float exitDuration)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speed = speed;
            if (!string.IsNullOrEmpty(tag)) state.tag = tag;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (hitType >= 0) enter.AddCondition(AnimatorConditionMode.Equals, hitType, "HitType");
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = true;

            AnimatorStateTransition exit = state.AddTransition(restState);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.hasFixedDuration = true;
            exit.duration = exitDuration;
        }

        /// <summary>
        /// Capa "Reacciones": solo torso, cabeza y brazos, para que siga corriendo mientras recibe golpes. Por defecto
        /// está en un estado vacío y con el trigger Hit reproduce la reacción que indique HitType. Si falta algún
        /// clip, usa otro en su lugar para que el trigger siempre se consuma.
        /// </summary>
        private static void BuildReactionLayer(AnimatorController controller, AnimationClip gut, AnimationClip right, AnimationClip left)
        {
            AnimationClip fallback = gut;
            if (fallback == null) fallback = right;
            if (fallback == null) fallback = left;
            if (fallback == null)
            {
                Debug.LogWarning("[SoldierVisualBuilder] No hay clips de reacción al golpe: no se crea la capa " + ReactionLayerName + ".");
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

            AddTriggeredState(machine, none, "HitGut", gut, HitSpeed, "Hit", 0, null, 0.8f, 0.15f);
            AddTriggeredState(machine, none, "HitRight", right, HitSpeed, "Hit", 1, null, 0.8f, 0.15f);
            AddTriggeredState(machine, none, "HitLeft", left, HitSpeed, "Hit", 2, null, 0.8f, 0.15f);
        }

        // ---------- 3. Prefab ----------

        private static bool MountInPrefab(Avatar avatar, AnimatorController controller, Material soldierMaterial, Material scimitarMaterial)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError("[SoldierVisualBuilder] No se pudo cargar " + ModelPath + ".");
                return false;
            }

            // El prefab se abre aparte, se modifica y se vuelve a guardar: así no hace falta tenerlo en la escena.
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                EnemySoldier soldier = root.GetComponent<EnemySoldier>();
                if (soldier == null)
                {
                    Debug.LogError("[SoldierVisualBuilder] " + PrefabPath + " no tiene EnemySoldier en su raíz.");
                    return false;
                }
                SerializedObject soldierObject = new SerializedObject(soldier);

                Transform visual = root.transform.Find(VisualName);
                if (visual == null)
                {
                    GameObject visualObject = new GameObject(VisualName);
                    visualObject.layer = root.layer;
                    visualObject.transform.SetParent(root.transform, false);
                    visual = visualObject.transform;
                }

                // Quita un montaje anterior. Si ya habías acomodado la cimitarra a mano (CimitarraSocket), se conserva.
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
                    Object.DestroyImmediate(previous.gameObject);
                }

                // Las piezas de prueba (Cuerpo, Cuerno_D, Cuerno_I, Frente) solo dejan de dibujarse. No se apagan ni
                // se borran, porque "Cuerpo" lleva el CapsuleCollider con el que los héroes golpean al soldado.
                // El aviso del suelo se deja como está: lo enciende y lo apaga EnemySoldier.
                SerializedProperty indicatorProperty = soldierObject.FindProperty("attackIndicator");
                GameObject indicator = indicatorProperty != null ? indicatorProperty.objectReferenceValue as GameObject : null;
                MeshRenderer[] placeholders = root.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < placeholders.Length; i++)
                {
                    if (indicator != null && placeholders[i].transform.IsChildOf(indicator.transform)) continue;
                    placeholders[i].enabled = false;
                }

                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, visual);
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

                if (soldierMaterial != null)
                {
                    SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    for (int i = 0; i < skins.Length; i++) skins[i].sharedMaterial = soldierMaterial;
                }

                Transform socket = MountScimitar(root.transform, model.transform, animator, scimitarMaterial);
                if (socket != null && keepSocket)
                {
                    socket.localPosition = socketPosition;
                    socket.localRotation = socketRotation;
                    socket.localScale = socketScale;
                }

                SetLayer(model.transform, root.layer);

                // EnemySoldier: el Animator y cuánto espera tendido antes de hundirse.
                SerializedProperty animatorProperty = soldierObject.FindProperty("animator");
                if (animatorProperty != null) animatorProperty.objectReferenceValue = animator;
                SerializedProperty sinkDelay = soldierObject.FindProperty("sinkDelay");
                if (sinkDelay != null) sinkDelay.floatValue = DeathFallTime / DeathSpeed + 0.1f;
                soldierObject.ApplyModifiedPropertiesWithoutUndo();

                // Hit (según de dónde vino el golpe) y Die los dispara el puente; al volver del pool reinicia el Animator.
                CharacterAnimationBridge bridge = root.GetComponent<CharacterAnimationBridge>();
                if (bridge == null) bridge = root.AddComponent<CharacterAnimationBridge>();
                SetReference(bridge, "animator", animator);

                // El destello al recibir daño pasa de las piezas de prueba al modelo y la cimitarra.
                HitFlash flash = root.GetComponent<HitFlash>();
                if (flash != null)
                {
                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                    SerializedObject flashObject = new SerializedObject(flash);
                    SerializedProperty list = flashObject.FindProperty("renderers");
                    if (list != null)
                    {
                        list.arraySize = renderers.Length;
                        for (int i = 0; i < renderers.Length; i++)
                            list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                        flashObject.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform MountScimitar(Transform soldierRoot, Transform model, Animator animator, Material scimitarMaterial)
        {
            GameObject scimitarAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ScimitarPath);
            if (scimitarAsset == null)
            {
                Debug.LogWarning("[SoldierVisualBuilder] Falta " + ScimitarPath + ": el soldado queda sin cimitarra.");
                return null;
            }

            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) hand = FindDeep(model, "RightHand");
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (forearm == null) forearm = FindDeep(model, "RightForeArm");
            if (hand == null)
            {
                Debug.LogWarning("[SoldierVisualBuilder] No encuentro el hueso de la mano derecha: el soldado queda sin cimitarra.");
                return null;
            }

            // En la pose de reposo el brazo apunta hacia la derecha del soldado. La cimitarra se coloca como se
            // empuña de verdad: la hoja sale por el lado del pulgar (hacia el frente) y el filo mira hacia donde
            // apunta el antebrazo. En el FBX la hoja va por +Y, el filo curvo por -X y la cara plana por +Z.
            Vector3 handDirection = forearm != null ? (hand.position - forearm.position).normalized : soldierRoot.right;
            Vector3 bladeDirection = soldierRoot.forward;
            Vector3 edgeDirection = Vector3.ProjectOnPlane(handDirection, bladeDirection);
            if (edgeDirection.sqrMagnitude < 0.0001f) edgeDirection = soldierRoot.right;
            edgeDirection.Normalize();

            GameObject socket = new GameObject(SocketName);
            socket.transform.position = hand.position + handDirection * ScimitarAlongHand;
            socket.transform.rotation = Quaternion.LookRotation(Vector3.Cross(-edgeDirection, bladeDirection), bladeDirection);
            socket.transform.SetParent(hand, true);

            GameObject scimitar = (GameObject)PrefabUtility.InstantiatePrefab(scimitarAsset, socket.transform);
            scimitar.name = ScimitarName;
            scimitar.transform.localPosition = Vector3.zero;

            if (scimitarMaterial != null)
            {
                Renderer[] renderers = scimitar.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = scimitarMaterial;
            }

            return socket.transform;
        }

        // ---------- Utilidades ----------

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i), layer);
        }

        private static float ReadFloat(Object component, string propertyName, float fallback)
        {
            if (component == null) return fallback;

            SerializedProperty property = new SerializedObject(component).FindProperty(propertyName);
            return property != null && property.propertyType == SerializedPropertyType.Float ? property.floatValue : fallback;
        }

        private static void SetReference(Object component, string propertyName, Object value)
        {
            if (component == null) return;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[SoldierVisualBuilder] " + component.GetType().Name + " no tiene el campo '" + propertyName + "'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
}
