using System.IO;
using TheLastBeat.Abilities;
using TheLastBeat.Feedback;
using TheLastBeat.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta los efectos de las habilidades del caballero desde el menú "The Last Beat" (tarea 33):
    ///  1. Arco azul del giro: media luna plana que gira con el objeto Visual mientras dura SpinAbility.
    ///  2. Onda expansiva: anillo que crece hasta el radio de SpinAbility.Exploded, con chispas.
    ///  3. Polvo de la embestida: partículas en los pies mientras dura ChargeAbility.
    ///  4. Rayo de la estocada: estela fina en la punta de la espada durante ChargeAbility.
    /// Las texturas, los materiales y los objetos de la escena se rehacen en cada ejecución: para cambiar el aspecto,
    /// edita las constantes de este archivo y vuelve a ejecutar el menú (lo que retoques a mano se pierde).
    /// Los efectos usan mezcla alfa con azul saturado, no aditiva, para que se vean también sobre suelos claros.
    /// Requiere haber ejecutado antes "Montar caballero", porque la estela de la estocada cuelga de SwordSocket.
    /// Es código de editor: no va en la build.
    /// </summary>
    public static class KnightVfxBuilder
    {
        private const string Folder = "Assets/_Project/VFX/Knight/";
        private const string DotTexturePath = Folder + "T_VFX_SoftDot.png";
        private const string RingTexturePath = Folder + "T_VFX_Ring.png";
        private const string TrailTexturePath = Folder + "T_VFX_Trail.png";
        private const string ArcTexturePath = Folder + "T_VFX_SpinArc.png";
        private const string ArcMaterialPath = Folder + "M_VFX_SpinArc.mat";
        private const string TrailMaterialPath = Folder + "M_VFX_SwordTrail.mat";
        private const string RingMaterialPath = Folder + "M_VFX_Ring.mat";
        private const string SparkMaterialPath = Folder + "M_VFX_Spark.mat";
        private const string DustMaterialPath = Folder + "M_VFX_Dust.mat";

        private const string ModelName = "Knight_Model";
        private const string SocketName = "SwordSocket";
        private const string Prefix = "VFX_";

        // ---------- Ajustes (cambiar aquí y volver a ejecutar el menú) ----------
        private static readonly Color DeepBlue = new Color(0.10f, 0.42f, 1f, 1f);   // cuerpo de los efectos
        private static readonly Color PaleBlue = new Color(0.85f, 0.97f, 1f, 1f);   // núcleo brillante
        private static readonly Color SparkBlue = new Color(0.20f, 0.55f, 1f, 1f);
        private static readonly Color Dust = new Color(0.62f, 0.58f, 0.52f, 0.60f);

        // Arco del giro: media luna plana, hija de Visual, así que gira con el caballero sin escalones.
        private const float ArcRadius = 2.0f;         // metros desde el centro hasta el borde exterior (un poco más allá de la punta de la espada)
        private const float ArcHeight = 0.90f;        // altura de la espada durante el giro
        private const float ArcLengthDegrees = 260f;  // cuánto rodea al caballero antes de desvanecerse
        private const float ArcHeadDegrees = 12f;     // ángulo de la espada respecto a la derecha del caballero
        private const float ArcInnerAtHead = 0.42f;   // grosor: radio interior junto a la espada (fracción del radio)
        private const float ArcInnerAtTail = 0.80f;   // y en la cola, donde se afina

        // Rayo de la estocada, en la punta.
        private const float ChargeTrailAlongBlade = 1.0f;
        private const float ChargeTrailWidth = 0.42f;
        private const float ChargeTrailTime = 0.25f;

        // Polvo de la embestida: partículas por metro recorrido.
        private const float DustPerMeter = 5f;
        private const int DustMaxParticles = 60;

        // Chispas de la onda expansiva.
        private const int SparkCount = 28;

        [MenuItem("The Last Beat/Personajes/Montar efectos del caballero")]
        public static void BuildEffects()
        {
            PlayerController player = FindPlayer();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Efectos del caballero", "No hay ningún PlayerController en la escena abierta.", "Entendido");
                return;
            }

            if (Mount(player.gameObject))
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[KnightVfxBuilder] Efectos del caballero montados. Materiales y texturas en " + Folder);
            }
        }

        /// <summary>
        /// Lo llama "Montar caballero" al terminar: como ese menú rehace Knight_Model y SwordSocket,
        /// las estelas se pierden. Si los efectos ya estaban montados, se vuelven a montar solos.
        /// </summary>
        internal static void RebuildIfMounted()
        {
            PlayerController player = FindPlayer();
            if (player == null) return;

            GameObject knight = player.gameObject;
            bool mounted = knight.GetComponent<SpinExplosionVfx>() != null || knight.GetComponents<AbilityVfx>().Length > 0;
            if (mounted) Mount(knight);
        }

        private static bool Mount(GameObject knight)
        {
            Transform model = knight.transform.Find("Visual/" + ModelName);
            Transform socket = model != null ? FindDeep(model, SocketName) : null;
            if (socket == null)
            {
                EditorUtility.DisplayDialog("Efectos del caballero",
                    "No encuentro " + SocketName + " dentro de Visual/" + ModelName + ".\nEjecuta primero \"Montar caballero\".", "Entendido");
                return false;
            }

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            Texture2D dot = WriteTexture(DotTexturePath, 64, 64, SoftDotPixel);
            Texture2D ringTexture = WriteTexture(RingTexturePath, 256, 256, RingPixel);
            Texture2D trailTexture = WriteTexture(TrailTexturePath, 8, 64, TrailPixel);
            Texture2D arcTexture = WriteTexture(ArcTexturePath, 256, 256, ArcPixel);

            Material trailMaterial = WriteMaterial(TrailMaterialPath, trailTexture, Color.white, false);
            Material ringMaterial = WriteMaterial(RingMaterialPath, ringTexture, Color.white, false);
            Material sparkMaterial = WriteMaterial(SparkMaterialPath, dot, Color.white, false);
            Material dustMaterial = WriteMaterial(DustMaterialPath, dot, Color.white, false);
            Material arcMaterial = WriteMaterial(ArcMaterialPath, arcTexture, Color.white, false);
            if (trailMaterial == null || ringMaterial == null || sparkMaterial == null || dustMaterial == null || arcMaterial == null) return false;

            Transform visual = knight.transform.Find("Visual");

            // Limpia un montaje anterior.
            RemoveChildrenWithPrefix(socket);
            RemoveChildrenWithPrefix(knight.transform);
            if (visual != null) RemoveChildrenWithPrefix(visual);
            AbilityVfx[] oldLinks = knight.GetComponents<AbilityVfx>();
            for (int i = 0; i < oldLinks.Length; i++) Undo.DestroyObjectImmediate(oldLinks[i]);
            SpinExplosionVfx oldExplosion = knight.GetComponent<SpinExplosionVfx>();
            if (oldExplosion != null) Undo.DestroyObjectImmediate(oldExplosion);

            SpinAbility spin = knight.GetComponent<SpinAbility>();
            ChargeAbility charge = knight.GetComponent<ChargeAbility>();
            if (spin == null) Debug.LogWarning("[KnightVfxBuilder] El caballero no tiene SpinAbility: se omiten el arco y la onda expansiva.");
            if (charge == null) Debug.LogWarning("[KnightVfxBuilder] El caballero no tiene ChargeAbility: se omiten el polvo y el rayo.");

            if (spin != null)
            {
                // 1. Arco del giro: se enciende mientras dura la habilidad y gira con Visual.
                GameObject arc = CreateArc(visual != null ? visual : knight.transform, arcMaterial);
                AbilityVfx link = Undo.AddComponent<AbilityVfx>(knight);
                Configure(link, spin, null, null, new Object[] { arc });

                // 2. Onda expansiva.
                GameObject root = CreateChild(knight.transform, Prefix + "SpinExplosion", Vector3.zero);
                Transform ring = CreateRing(root.transform, ringMaterial);
                ParticleSystem sparks = CreateSparks(root.transform, sparkMaterial);

                SpinExplosionVfx explosion = Undo.AddComponent<SpinExplosionVfx>(knight);
                SerializedObject serialized = new SerializedObject(explosion);
                serialized.FindProperty("spin").objectReferenceValue = spin;
                serialized.FindProperty("ring").objectReferenceValue = ring;
                serialized.FindProperty("sparks").objectReferenceValue = sparks;
                serialized.FindProperty("color").colorValue = Color.white;   // el color ya va pintado en la textura del anillo
                serialized.ApplyModifiedProperties();
            }

            if (charge != null)
            {
                // 3. Polvo en los pies y 4. rayo en la punta de la espada.
                ParticleSystem dust = CreateDust(knight.transform, dustMaterial);
                TrailRenderer streak = CreateTrail(socket, Prefix + "ChargeStreak", new Vector3(0f, ChargeTrailAlongBlade, 0f),
                                                   ChargeTrailWidth, ChargeTrailTime, trailMaterial);
                AbilityVfx link = Undo.AddComponent<AbilityVfx>(knight);
                Configure(link, charge, new Object[] { streak }, new Object[] { dust }, null);
            }

            EditorSceneManager.MarkSceneDirty(knight.scene);
            return true;
        }

        // ---------- Objetos de la escena ----------

        private static TrailRenderer CreateTrail(Transform parent, string name, Vector3 localPosition,
                                                 float width, float time, Material material)
        {
            GameObject go = CreateChild(parent, name, localPosition);
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            // 0 = extremo recién creado (pegado a la espada), 1 = cola.
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.75f), new Keyframe(1f, 0f));
            trail.colorGradient = MakeTrailGradient();
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.autodestruct = false;
            trail.emitting = false;
            return trail;
        }

        /// <summary>
        /// Media luna plana a la altura de la espada. Es hija de Visual: como SpinAbility rota ese objeto,
        /// el arco gira con el caballero. Empieza apagada; AbilityVfx la enciende durante el giro.
        /// </summary>
        private static GameObject CreateArc(Transform parent, Material material)
        {
            GameObject go = CreateChild(parent, Prefix + "SpinArc", new Vector3(0f, ArcHeight, 0f));
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(ArcRadius * 2f, ArcRadius * 2f, 1f);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go;
        }

        private static Transform CreateRing(Transform parent, Material material)
        {
            GameObject go = CreateChild(parent, "Ring", new Vector3(0f, 0.06f, 0f));
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go.transform;
        }

        private static ParticleSystem CreateSparks(Transform parent, Material material)
        {
            GameObject go = CreateChild(parent, "Sparks", new Vector3(0f, 0.3f, 0f));
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);   // SpinExplosionVfx la recalcula con el radio real
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(SparkBlue, PaleBlue);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = SparkCount * 2;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)SparkCount) });

            // Círculo horizontal: las chispas salen hacia fuera a ras de suelo.
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(MakeGradient(Color.white, 1f, 0f));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static ParticleSystem CreateDust(Transform parent, Material material)
        {
            GameObject go = CreateChild(parent, Prefix + "ChargeDust", new Vector3(0f, 0.15f, 0f));
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(Dust);
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // el polvo se queda atrás mientras el caballero avanza
            main.gravityModifier = -0.03f;
            main.maxParticles = DustMaxParticles;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = DustPerMeter;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(MakeGradient(Color.white, 1f, 0f));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.7f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Montar efectos del caballero");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void RemoveChildrenWithPrefix(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(Prefix)) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void Configure(AbilityVfx link, Ability ability, Object[] trails, Object[] whileActive, Object[] objects)
        {
            SerializedObject serialized = new SerializedObject(link);
            serialized.FindProperty("ability").objectReferenceValue = ability;
            SetArray(serialized.FindProperty("trails"), trails);
            SetArray(serialized.FindProperty("whileActive"), whileActive);
            SetArray(serialized.FindProperty("objects"), objects);
            serialized.ApplyModifiedProperties();
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            int count = values != null ? values.Length : 0;
            array.arraySize = count;
            for (int i = 0; i < count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------- Texturas y materiales ----------

        private delegate Color PixelFunction(float u, float v);

        private static float Smooth(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        /// <summary>Punto suave blanco: opaco en el centro, transparente en el borde.</summary>
        private static Color SoftDotPixel(float u, float v)
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }

        /// <summary>Anillo: borde exterior grueso y brillante, con un resplandor azul que se apaga hacia dentro.</summary>
        private static Color RingPixel(float u, float v)
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            if (d > 1f) return new Color(DeepBlue.r, DeepBlue.g, DeepBlue.b, 0f);

            float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.14f);
            float glow = Smooth(0.25f, 0.85f, d);
            float alpha = Mathf.Clamp01(edge * 0.95f + glow * 0.40f) * Smooth(1f, 0.97f, d);

            Color c = Color.Lerp(DeepBlue, PaleBlue, edge * edge);
            c.a = alpha;
            return c;
        }

        /// <summary>Estela blanca: opaca en el centro del ancho y suave hacia los dos bordes. El color lo pone el degradado.</summary>
        private static Color TrailPixel(float u, float v)
        {
            float a = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI);
            return new Color(1f, 1f, 1f, a * Mathf.Sqrt(a));
        }

        /// <summary>
        /// Media luna del giro. Vista desde arriba, u es la derecha del caballero y v su frente. La cabeza del arco está
        /// en la espada y la cola queda hacia donde la espada ya pasó (el giro de SpinAbility es en sentido horario).
        /// </summary>
        private static Color ArcPixel(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            Color clear = new Color(DeepBlue.r, DeepBlue.g, DeepBlue.b, 0f);
            if (r > 1f) return clear;

            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg - ArcHeadDegrees;
            angle = Mathf.Repeat(angle, 360f);
            if (angle > ArcLengthDegrees) return clear;
            float t = angle / ArcLengthDegrees;   // 0 = junto a la espada, 1 = final de la cola

            float inner = Mathf.Lerp(ArcInnerAtHead, ArcInnerAtTail, t);
            const float outer = 0.97f;
            float band = Smooth(inner, inner + 0.10f, r) * (1f - Smooth(outer - 0.04f, outer, r));
            float along = Mathf.Pow(1f - t, 1.3f) * Smooth(0f, 0.03f, t);
            float core = Smooth(outer - 0.24f, outer - 0.05f, r) * (1f - t) * (1f - t);

            Color c = Color.Lerp(DeepBlue, PaleBlue, core);
            c.a = Mathf.Clamp01(band * along * 0.92f);
            return c;
        }

        /// <summary>Genera la textura y la guarda como PNG. Se reescribe en cada ejecución del menú.</summary>
        private static Texture2D WriteTexture(string path, int width, int height, PixelFunction pixel)
        {
            Texture2D generated = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    pixels[y * width + x] = pixel(u, v);
                }
            }
            generated.SetPixels(pixels);
            generated.Apply();
            File.WriteAllBytes(path, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) Debug.LogWarning("[KnightVfxBuilder] No se pudo cargar la textura " + path + ".");
            return texture;
        }

        /// <summary>
        /// Material de partículas de URP, transparente y sin iluminación. additive = true suma luz (solo se nota sobre
        /// fondos oscuros); false mezcla normal. Se reconfigura en cada ejecución del menú.
        /// </summary>
        private static Material WriteMaterial(string path, Texture2D texture, Color color, bool additive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[KnightVfxBuilder] No encuentro el shader 'Universal Render Pipeline/Particles/Unlit'.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader);
            else material.shader = shader;

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            // Lo mismo que hace el Inspector al elegir Surface Type = Transparent y Blending Mode = Alpha o Additive.
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", additive ? 2f : 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            SetFloat(material, "_Cull", (float)CullMode.Off);
            SetFloat(material, "_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);

            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        /// <summary>Rayo de la estocada: casi blanco junto a la espada, azul saturado en el resto, transparente en la cola.</summary>
        private static Gradient MakeTrailGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(PaleBlue, 0f), new GradientColorKey(DeepBlue, 0.3f), new GradientColorKey(DeepBlue, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.75f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Gradient MakeGradient(Color color, float startAlpha, float endAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) });
            return gradient;
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
