using System.IO;
using TheLastBeat.Abilities;
using TheLastBeat.Characters;
using TheLastBeat.Core;
using TheLastBeat.Feedback;
using TheLastBeat.Heroes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Monta los efectos del ataque y las habilidades de la bardo desde el menú "The Last Beat" (tarea 33):
    ///  1. Nota: resplandor rosa en el laúd y una ráfaga de notas de 8 bits que revientan en píxeles.
    ///  2. Golpe de bajo: resplandor mientras sube el laúd y, al pisar, una onda morada con píxeles despedidos.
    ///  3. Pista de baile: pista redonda de baldosas de colores que cambian a cada compás, con destellos.
    /// El resplandor sale con BardActionMoments, que lee del Animator el inicio de cada animación. Las notas, la
    /// onda y la pista salen con los eventos del ataque y las habilidades (BardBasicAttack.Fired,
    /// BassDropAbility.Dropped, DanceFloorAbility.Opened) si la bardo ya tiene su combate montado; si no, salen
    /// solos en el instante de la animación. Este menú enlaza lo que encuentre en la bardo.
    /// Las texturas, los materiales y los objetos de la escena se rehacen en cada ejecución: para cambiar el aspecto,
    /// edita las constantes de este archivo y vuelve a ejecutar el menú (lo que retoques a mano se pierde).
    /// Usan mezcla alfa con colores saturados y contorno oscuro, no aditiva, para que se vean sobre suelos claros.
    /// Requiere haber ejecutado antes "Montar bardo", porque el resplandor cuelga de LuteSocket.
    /// Es código de editor: no va en la build.
    /// </summary>
    public static class BardVfxBuilder
    {
        private const string Folder = "Assets/_Project/VFX/Bard/";
        private const string DotTexturePath = Folder + "T_VFX_BardSoftDot.png";
        private const string NoteTexturePath = Folder + "T_VFX_BardNotes.png";
        private const string PixelTexturePath = Folder + "T_VFX_BardPixel.png";
        private const string StarTexturePath = Folder + "T_VFX_BardStar.png";
        private const string RingTexturePath = Folder + "T_VFX_BardBassRing.png";
        private const string FloorTexturePrefix = Folder + "T_VFX_BardDanceFloor_";
        private const string GlowMaterialPath = Folder + "M_VFX_BardGlow.mat";
        private const string NoteMaterialPath = Folder + "M_VFX_BardNote.mat";
        private const string PixelMaterialPath = Folder + "M_VFX_BardPixel.mat";
        private const string StarMaterialPath = Folder + "M_VFX_BardStar.mat";
        private const string RingMaterialPath = Folder + "M_VFX_BardBassRing.mat";
        private const string FlashMaterialPath = Folder + "M_VFX_BardBassFlash.mat";
        private const string FloorMaterialPath = Folder + "M_VFX_BardDanceFloor.mat";

        private const string ModelName = "Bard_Model";
        private const string SocketName = "LuteSocket";
        private const string Prefix = "VFX_";
        private const string UndoName = "Montar efectos de la bardo";

        // ---------- Ajustes (cambiar aquí y volver a ejecutar el menú) ----------
        private static readonly Color DeepPurple = new Color(0.45f, 0.12f, 0.95f, 1f);
        private static readonly Color Magenta = new Color(1.00f, 0.22f, 0.82f, 1f);
        private static readonly Color SoftPink = new Color(1.00f, 0.55f, 0.92f, 1f);
        private static readonly Color PalePink = new Color(1.00f, 0.86f, 0.98f, 1f);
        private static readonly Color Ink = new Color(0.22f, 0.05f, 0.38f, 1f);          // contorno de notas, píxeles y destellos
        private static readonly Color SparkleYellow = new Color(1.00f, 0.95f, 0.60f, 1f);
        private static readonly Color HealGreen = new Color(0.30f, 0.95f, 0.45f, 1f);    // destellos de curación
        private static readonly Color HealPale = new Color(0.80f, 1.00f, 0.75f, 1f);

        // Nota (ataque básico).
        private const float NoteRange = 7f;            // metros que recorren las notas
        private const float NoteFlightSpeed = 14f;     // metros por segundo
        // Segundos entre el inicio de la animación y la salida de la nota: el resplandor llega a su máximo ahí.
        private const float NoteChargeTime = BardVisualBuilder.NoteMoment / BardVisualBuilder.NoteSpeed;
        private const float NoteSizeMin = 0.55f;       // tamaño de cada nota en metros
        private const float NoteSizeMax = 0.75f;
        private const int NoteCount = 5;               // notas por disparo
        private const float NoteSpacing = 0.05f;       // segundos entre una nota y la siguiente
        private static readonly Vector3 NoteOrigin = new Vector3(0f, 1.15f, 0.45f);   // de dónde salen, respecto a los pies
        private const int NoteBurstPixels = 9;         // píxeles en los que revienta cada nota
        private const float NoteTrailPerSecond = 36f;  // píxeles de estela por segundo y por nota

        // Resplandor del laúd (lo usan la Nota y el Golpe de bajo).
        private const float GlowSize = 1.3f;
        private static readonly Vector3 GlowOffset = new Vector3(0f, -0.22f, 0.05f);   // respecto a LuteSocket: entre la caja y el mástil

        // Golpe de bajo.
        private const float BassDropRadius = 4f;        // nivel 1
        private const float BassDropChargeTime = BardVisualBuilder.BassDropMoment / BardVisualBuilder.BassDropSpeed; // hasta el pisotón
        private const int BassDropPixels = 30;

        // Pista de baile.
        private const float DanceFloorRadius = 3.5f;    // nivel 1
        private const float DanceFloorDuration = 6f;
        private const float DanceFloorBeat = 0.4f;      // segundos entre cambios de color
        private const int DanceFloorTiles = 8;          // baldosas por lado del cuadrado que contiene la pista
        private const int DanceFloorFrames = 4;         // variantes de color
        private const float DanceFloorDisc = 0.94f;     // parte de la textura que ocupa el disco (el resto es margen)
        private const float DanceFloorAlpha = 0.90f;
        private const float InnerSparklesPerSecond = 10f;
        private static readonly Color RimPink = new Color(1.00f, 0.30f, 0.78f, 1f);
        private static readonly Color JointColor = new Color(1.00f, 0.94f, 1.00f, 1f);
        private static readonly Color[] TileColors =
        {
            new Color(1.00f, 0.42f, 0.74f, 1f),   // rosa
            new Color(0.80f, 0.36f, 1.00f, 1f),   // morado
            new Color(0.30f, 0.88f, 0.80f, 1f),   // menta
            new Color(1.00f, 0.86f, 0.36f, 1f),   // amarillo
            new Color(0.38f, 0.70f, 1.00f, 1f),   // azul
            new Color(1.00f, 0.60f, 0.35f, 1f),   // naranja
        };

        // Dibujos de 8 bits: '.' transparente, 'o' contorno oscuro, '#' relleno blanco (lo tiñe el color de la partícula).
        private static readonly string[] NotesBitmap =   // dos notas de 16 x 16, una al lado de la otra
        {
            "........oooo........ooooooooooo.",
            ".......o####o.......o#########o.",
            ".......o#o###o......o#########o.",
            ".......o#oo###o.....o#ooooooo#o.",
            ".......o#o.o##o.....o#o.....o#o.",
            ".......o#o..o#o.....o#o.....o#o.",
            ".......o#o..o#o.....o#o.....o#o.",
            ".......o#o...o......o#o.....o#o.",
            ".......o#o..........o#o.....o#o.",
            "...oooo##o.......ooo##o..ooo##o.",
            "..o######o......o#####o.o#####o.",
            ".o#######o......o#####o.o#####o.",
            ".o#######o......o#####o.o#####o.",
            ".o######o........o###o...o###o..",
            "..o####o..........ooo.....ooo...",
            "...oooo.........................",
        };

        private static readonly string[] StarBitmap =
        {
            "....o....",
            "...o#o...",
            "...o#o...",
            ".oo###oo.",
            "o#######o",
            ".oo###oo.",
            "...o#o...",
            "...o#o...",
            "....o....",
        };

        private static readonly string[] PixelBitmap =
        {
            "oooooo",
            "o####o",
            "o####o",
            "o####o",
            "o####o",
            "oooooo",
        };

        [MenuItem("The Last Beat/Personajes/Montar efectos de la bardo")]
        public static void BuildEffects()
        {
            BardCompanion companion = FindBard();
            if (companion == null)
            {
                EditorUtility.DisplayDialog("Efectos de la bardo", "No hay ningún BardCompanion en la escena abierta.", "Entendido");
                return;
            }

            if (Mount(companion.gameObject, true))
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BardVfxBuilder] Efectos de la bardo montados. Materiales y texturas en " + Folder);
            }
        }

        /// <summary>
        /// Lo llama "Montar bardo" al terminar: como ese menú rehace Bard_Model y LuteSocket, el resplandor del laúd
        /// se pierde. Si los efectos ya estaban montados, se vuelven a montar solos.
        /// </summary>
        internal static void RebuildIfMounted()
        {
            BardCompanion companion = FindBard();
            if (companion == null) return;

            GameObject bard = companion.gameObject;
            bool mounted = bard.GetComponent<BardNoteVfx>() != null || bard.GetComponent<BardBassDropVfx>() != null ||
                           bard.GetComponent<BardDanceFloorVfx>() != null;
            if (mounted) Mount(bard, false);
        }

        private static bool Mount(GameObject bard, bool showDialogs)
        {
            Transform model = bard.transform.Find("Visual/" + ModelName);
            Transform socket = model != null ? FindDeep(model, SocketName) : null;
            Animator animator = model != null ? model.GetComponent<Animator>() : null;
            if (socket == null || animator == null)
            {
                string message = "No encuentro " + SocketName + " dentro de Visual/" + ModelName + ".\nEjecuta primero \"Montar bardo\".";
                if (showDialogs) EditorUtility.DisplayDialog("Efectos de la bardo", message, "Entendido");
                else Debug.LogWarning("[BardVfxBuilder] " + message);
                return false;
            }

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            // ----- Texturas -----
            Texture2D dot = WriteTexture(DotTexturePath, 64, 64, SoftDotPixel, false);
            Texture2D ringTexture = WriteTexture(RingTexturePath, 256, 256, BassRingPixel, false);
            Texture2D noteTexture = WriteBitmap(NoteTexturePath, NotesBitmap);
            Texture2D pixelTexture = WriteBitmap(PixelTexturePath, PixelBitmap);
            Texture2D starTexture = WriteBitmap(StarTexturePath, StarBitmap);

            Texture2D[] floorFrames = new Texture2D[DanceFloorFrames];
            for (int i = 0; i < DanceFloorFrames; i++)
            {
                floorFrame = i;
                floorFrames[i] = WriteTexture(FloorTexturePrefix + i + ".png", 512, 512, DanceFloorPixel, false);
            }

            // ----- Materiales -----
            Material glowMaterial = WriteMaterial(GlowMaterialPath, dot);
            Material noteMaterial = WriteMaterial(NoteMaterialPath, noteTexture);
            Material pixelMaterial = WriteMaterial(PixelMaterialPath, pixelTexture);
            Material starMaterial = WriteMaterial(StarMaterialPath, starTexture);
            Material ringMaterial = WriteMaterial(RingMaterialPath, ringTexture);
            Material flashMaterial = WriteMaterial(FlashMaterialPath, dot);
            Material floorMaterial = WriteMaterial(FloorMaterialPath, floorFrames[0]);
            if (glowMaterial == null || noteMaterial == null || pixelMaterial == null || starMaterial == null ||
                ringMaterial == null || flashMaterial == null || floorMaterial == null) return false;

            // ----- Limpia un montaje anterior -----
            RemoveChildrenWithPrefix(socket);
            RemoveChildrenWithPrefix(bard.transform);
            DestroyComponent(bard.GetComponent<BardNoteVfx>());
            DestroyComponent(bard.GetComponent<BardBassDropVfx>());
            DestroyComponent(bard.GetComponent<BardDanceFloorVfx>());

            // ----- El que avisa de los momentos de cada animación -----
            BardActionMoments moments = bard.GetComponent<BardActionMoments>();
            if (moments == null) moments = Undo.AddComponent<BardActionMoments>(bard);
            SetReference(moments, "animator", animator);

            // ----- 1. Nota -----
            ParticleSystem glow = CreateGlow(socket, glowMaterial);
            ParticleSystem notes = CreateNotes(bard.transform, noteMaterial, pixelMaterial);

            BardNoteVfx noteVfx = Undo.AddComponent<BardNoteVfx>(bard);
            SerializedObject note = new SerializedObject(noteVfx);
            note.FindProperty("moments").objectReferenceValue = moments;
            note.FindProperty("glow").objectReferenceValue = glow;
            note.FindProperty("notes").objectReferenceValue = notes;
            note.FindProperty("attack").objectReferenceValue = bard.GetComponent<BardBasicAttack>();
            note.FindProperty("range").floatValue = NoteRange;
            note.FindProperty("speed").floatValue = NoteFlightSpeed;
            note.FindProperty("chargeTime").floatValue = NoteChargeTime;
            note.ApplyModifiedProperties();

            // ----- 2. Golpe de bajo -----
            GameObject bassRoot = CreateChild(bard.transform, Prefix + "BassDrop", Vector3.zero);
            Transform ring = CreateGroundQuad(bassRoot.transform, "Ring", ringMaterial, -1);
            Transform flash = CreateGroundQuad(bassRoot.transform, "Flash", flashMaterial, -2);
            ParticleSystem debris = CreateDebris(bassRoot.transform, pixelMaterial);

            BardBassDropVfx bassVfx = Undo.AddComponent<BardBassDropVfx>(bard);
            SerializedObject bass = new SerializedObject(bassVfx);
            bass.FindProperty("moments").objectReferenceValue = moments;
            bass.FindProperty("ring").objectReferenceValue = ring;
            bass.FindProperty("flash").objectReferenceValue = flash;
            bass.FindProperty("debris").objectReferenceValue = debris;
            bass.FindProperty("glow").objectReferenceValue = glow;
            bass.FindProperty("ability").objectReferenceValue = bard.GetComponent<BassDropAbility>();
            bass.FindProperty("radius").floatValue = BassDropRadius;
            bass.FindProperty("chargeTime").floatValue = BassDropChargeTime;
            bass.FindProperty("ringColor").colorValue = Color.white;   // el color ya va pintado en la textura del anillo
            bass.FindProperty("flashColor").colorValue = new Color(DeepPurple.r, DeepPurple.g, DeepPurple.b, 0.55f);
            bass.ApplyModifiedProperties();

            // ----- 3. Pista de baile -----
            GameObject floorRoot = CreateChild(bard.transform, Prefix + "DanceFloor", Vector3.zero);
            Transform floorQuad = CreateGroundQuad(floorRoot.transform, "Floor", floorMaterial, -3);
            floorQuad.gameObject.SetActive(true);   // lo que se apaga es la raíz entera
            ParticleSystem rimSparkles = CreateSparkles(floorRoot.transform, "RimSparkles", starMaterial, 0f, 10f);
            ParticleSystem innerSparkles = CreateSparkles(floorRoot.transform, "InnerSparkles", starMaterial, 1f, InnerSparklesPerSecond);
            ParticleSystem healSparkles = CreateHealSparkles(floorRoot.transform, starMaterial);
            floorRoot.SetActive(false);

            BardDanceFloorVfx floorVfx = Undo.AddComponent<BardDanceFloorVfx>(bard);
            SerializedObject dance = new SerializedObject(floorVfx);
            dance.FindProperty("moments").objectReferenceValue = moments;
            dance.FindProperty("root").objectReferenceValue = floorRoot.transform;
            dance.FindProperty("floor").objectReferenceValue = floorQuad.GetComponent<Renderer>();
            dance.FindProperty("rimSparkles").objectReferenceValue = rimSparkles;
            dance.FindProperty("innerSparkles").objectReferenceValue = innerSparkles;
            dance.FindProperty("healSparkles").objectReferenceValue = healSparkles;
            dance.FindProperty("ability").objectReferenceValue = bard.GetComponent<DanceFloorAbility>();
            SetArray(dance.FindProperty("frames"), floorFrames);
            dance.FindProperty("radius").floatValue = DanceFloorRadius;
            dance.FindProperty("duration").floatValue = DanceFloorDuration;
            dance.FindProperty("beat").floatValue = DanceFloorBeat;
            dance.FindProperty("discFraction").floatValue = DanceFloorDisc;
            dance.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(bard.scene);
            return true;
        }

        // ---------- Objetos de la escena ----------

        /// <summary>
        /// Resplandor del laúd: una sola partícula grande y suave que BardNoteVfx.EmitGlow emite a mano. Es hija de
        /// LuteSocket y simula en local, así que sigue al laúd también cuando sube sobre la cabeza.
        /// El sistema está siempre en marcha pero no emite nada por sí solo.
        /// </summary>
        private static ParticleSystem CreateGlow(Transform socket, Material material)
        {
            ParticleSystem system = NewSystem(socket, Prefix + "LuteGlow", GlowOffset);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 0.45f;   // EmitGlow lo cambia en cada uso
            main.startSpeed = 0f;
            main.startSize = GlowSize;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 4;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // Crece despacio, destella en BardNoteVfx.GlowPeak (45 % de su vida) y se apaga.
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Magenta, 0f), new GradientColorKey(PalePink, BardNoteVfx.GlowPeak), new GradientColorKey(Magenta, 0.7f), new GradientColorKey(Magenta, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.30f), new GradientAlphaKey(0.92f, BardNoteVfx.GlowPeak), new GradientAlphaKey(0.45f, 0.62f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(0.38f, 0.80f), new Keyframe(BardNoteVfx.GlowPeak + 0.03f, 1.25f),
                new Keyframe(0.65f, 1.0f), new Keyframe(1f, 0.9f)));

            SetupRenderer(system, material, 1);
            return system;
        }

        /// <summary>
        /// Las notas: salen una tras otra por el eje Z del objeto (BardNoteVfx lo orienta al disparar), simulan en el
        /// mundo para ir en línea recta aunque la bardo se mueva, mueren al tocar un collider de la capa Enemy y,
        /// al morir, revientan en píxeles. Mientras vuelan dejan una estela de píxeles pequeños.
        /// </summary>
        private static ParticleSystem CreateNotes(Transform bard, Material noteMaterial, Material pixelMaterial)
        {
            ParticleSystem system = NewSystem(bard, Prefix + "NoteStream", NoteOrigin);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = NoteCount * NoteSpacing + 0.1f;
            main.startLifetime = NoteRange / NoteFlightSpeed;   // BardNoteVfx.Fire lo recalcula
            main.startSpeed = NoteFlightSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(NoteSizeMin, NoteSizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);   // radianes: cada nota un poco ladeada
            main.startColor = new ParticleSystem.MinMaxGradient(Magenta, SoftPink);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = NoteCount * 3;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[NoteCount];
            for (int i = 0; i < NoteCount; i++) bursts[i] = new ParticleSystem.Burst(i * NoteSpacing, (short)1);
            emission.SetBursts(bursts);

            // Cono sin apertura: todas salen hacia delante, repartidas en un círculo pequeño.
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 0.12f;

            // Aparecen de golpe y no se desvanecen: desaparecen al reventar.
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.12f, 1.1f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1f)));

            // La textura trae dos notas; cada partícula se queda con una al azar.
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;

            // Mueren al tocar a un enemigo o una pared, para reventar ahí y no atravesarlos. Vuelan a más de un
            // metro del piso, así que la capa Ground solo las para contra muros.
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = GameLayers.EnemyMask.value | GameLayers.ObstacleMask.value;
            collision.lifetimeLoss = 1f;
            collision.dampen = 0f;
            collision.bounce = 0f;
            collision.radiusScale = 0.4f;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.enableDynamicColliders = true;
            collision.sendCollisionMessages = false;

            SetupRenderer(system, noteMaterial, 3);

            // Estela: píxeles pequeños que se quedan atrás mientras la nota vive.
            ParticleSystem trail = NewSystem(system.transform, "Trail", Vector3.zero);
            ParticleSystem.MainModule trailMain = trail.main;
            trailMain.playOnAwake = false;
            trailMain.loop = false;
            trailMain.duration = 2f;
            trailMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            trailMain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            trailMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            trailMain.startColor = new ParticleSystem.MinMaxGradient(Magenta, PalePink);
            trailMain.simulationSpace = ParticleSystemSimulationSpace.World;
            trailMain.maxParticles = 200;
            ParticleSystem.EmissionModule trailEmission = trail.emission;
            trailEmission.enabled = true;
            trailEmission.rateOverTime = NoteTrailPerSecond;
            ParticleSystem.ShapeModule trailShape = trail.shape;
            trailShape.enabled = true;
            trailShape.shapeType = ParticleSystemShapeType.Sphere;
            trailShape.radius = 0.12f;
            FadeOut(trail);
            SetupRenderer(trail, pixelMaterial, 2);

            // Reventón: píxeles que saltan en todas direcciones y caen.
            ParticleSystem burst = NewSystem(system.transform, "Burst", Vector3.zero);
            ParticleSystem.MainModule burstMain = burst.main;
            burstMain.playOnAwake = false;
            burstMain.loop = false;
            burstMain.duration = 0.5f;
            burstMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
            burstMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            burstMain.startColor = new ParticleSystem.MinMaxGradient(Magenta, PalePink);
            burstMain.gravityModifier = 0.6f;
            burstMain.simulationSpace = ParticleSystemSimulationSpace.World;
            burstMain.maxParticles = NoteCount * NoteBurstPixels * 2;
            ParticleSystem.EmissionModule burstEmission = burst.emission;
            burstEmission.enabled = true;
            burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)NoteBurstPixels) });
            ParticleSystem.ShapeModule burstShape = burst.shape;
            burstShape.enabled = true;
            burstShape.shapeType = ParticleSystemShapeType.Sphere;
            burstShape.radius = 0.15f;
            FadeOut(burst);
            Shrink(burst, 0.35f);
            SetupRenderer(burst, pixelMaterial, 2);

            ParticleSystem.SubEmittersModule sub = system.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(trail, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
            sub.AddSubEmitter(burst, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

            return system;
        }

        /// <summary>Píxeles que salen despedidos a ras de suelo con la onda del Golpe de bajo.</summary>
        private static ParticleSystem CreateDebris(Transform parent, Material material)
        {
            ParticleSystem system = NewSystem(parent, "Debris", new Vector3(0f, 0.25f, 0f));

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);   // BardBassDropVfx la recalcula con el radio real
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(DeepPurple, Magenta);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = BassDropPixels * 2;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)BassDropPixels) });

            // Círculo horizontal: salen hacia fuera desde los pies.
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            FadeOut(system);
            Shrink(system, 0.4f);
            SetupRenderer(system, material, 2);
            return system;
        }

        /// <summary>
        /// Destellos de la pista: nacen en un círculo horizontal (solo el borde con thickness 0, todo el disco con 1)
        /// y suben despacio. BardDanceFloorVfx ajusta el radio del círculo al abrir la pista.
        /// </summary>
        private static ParticleSystem CreateSparkles(Transform parent, string name, Material material, float thickness, float rate)
        {
            ParticleSystem system = NewSystem(parent, name, new Vector3(0f, 0.1f, 0f));

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.startColor = new ParticleSystem.MinMaxGradient(SparkleYellow, PalePink);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            emission.rateOverDistance = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = DanceFloorRadius;
            shape.radiusThickness = thickness;
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            // Hacia arriba, en ejes del mundo. Las tres curvas deben ser del mismo tipo (dos constantes).
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Aparecen, brillan y se apagan.
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            SetupRenderer(system, material, 2);
            return system;
        }

        /// <summary>
        /// Destellos verdes de curación. No emiten por sí solos: BardDanceFloorVfx suelta unos cuantos sobre cada
        /// héroe curado, repartidos en una esfera pequeña alrededor del punto que le pasa.
        /// </summary>
        private static ParticleSystem CreateHealSparkles(Transform parent, Material material)
        {
            ParticleSystem system = NewSystem(parent, "HealSparkles", Vector3.zero);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(HealGreen, HealPale);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            FadeOut(system);
            SetupRenderer(system, material, 3);
            return system;
        }

        /// <summary>Quad plano en el suelo, apagado: lo enciende y lo coloca el script del efecto.</summary>
        private static Transform CreateGroundQuad(Transform parent, string name, Material material, int sortingOrder)
        {
            GameObject go = CreateChild(parent, name, new Vector3(0f, 0.06f, 0f));
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;   // los del suelo se dibujan antes que las partículas

            go.SetActive(false);
            return go.transform;
        }

        private static ParticleSystem NewSystem(Transform parent, string name, Vector3 localPosition)
        {
            GameObject go = CreateChild(parent, name, localPosition);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static void SetupRenderer(ParticleSystem system, Material material, int sortingOrder)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void FadeOut(ParticleSystem system)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Shrink(ParticleSystem system, float endScale)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endScale));
        }

        private static GameObject CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
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

        private static void DestroyComponent(Component component)
        {
            if (component != null) Undo.DestroyObjectImmediate(component);
        }

        private static void SetReference(Object component, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[BardVfxBuilder] " + component.GetType().Name + " no tiene el campo '" + propertyName + "'.");
                return;
            }
            property.objectReferenceValue = value;
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

        /// <summary>Punto suave blanco: opaco en el centro, transparente en el borde. El color lo pone quien lo usa.</summary>
        private static Color SoftDotPixel(float u, float v)
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a);
        }

        /// <summary>
        /// Onda del Golpe de bajo: banda exterior gruesa con filo claro, relleno morado que se apaga hacia el centro
        /// y dos brazos en espiral por dentro.
        /// </summary>
        private static Color BassRingPixel(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float d = Mathf.Sqrt(x * x + y * y);
            if (d > 1f) return new Color(Magenta.r, Magenta.g, Magenta.b, 0f);

            float angle = Mathf.Atan2(y, x);
            float edge = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.11f);
            float inner = Smooth(0.30f, 0.80f, d);
            float swirl = 0.5f + 0.5f * Mathf.Cos(2f * (angle - 5.5f * d));
            float arms = swirl * swirl * Smooth(0.15f, 0.45f, d) * (1f - Smooth(0.70f, 0.85f, d));
            float alpha = Mathf.Clamp01(edge * 0.95f + inner * 0.30f + arms * 0.45f) * Smooth(1f, 0.97f, d);

            Color c = Color.Lerp(DeepPurple, Magenta, inner);
            c = Color.Lerp(c, PalePink, edge * edge);
            c.a = alpha;
            return c;
        }

        // Variante de color que está pintando DanceFloorPixel (0 a DanceFloorFrames - 1).
        private static int floorFrame;

        /// <summary>
        /// Pista de baile: cuadrícula de baldosas de colores con juntas claras, recortada en un disco con borde rosa.
        /// El disco ocupa DanceFloorDisc del quad, para que el borde no quede cortado por el límite de la textura.
        /// </summary>
        private static Color DanceFloorPixel(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float d = Mathf.Sqrt(x * x + y * y) / DanceFloorDisc;
            if (d >= 1f) return new Color(RimPink.r, RimPink.g, RimPink.b, 0f);

            // Baldosa y posición dentro de ella (0 a 1).
            float fx = u * DanceFloorTiles;
            float fy = v * DanceFloorTiles;
            int ix = Mathf.FloorToInt(fx);
            int iy = Mathf.FloorToInt(fy);
            float lx = fx - ix;
            float ly = fy - iy;

            Color tile = TileColors[TileIndex(ix, iy, floorFrame)];
            float border = Mathf.Min(Mathf.Min(lx, 1f - lx), Mathf.Min(ly, 1f - ly));   // 0 en la junta, 0.5 en el centro
            float joint = 1f - Smooth(0.03f, 0.07f, border);
            float shine = Smooth(0.10f, 0.45f, border);

            Color dark = new Color(tile.r * 0.84f, tile.g * 0.84f, tile.b * 0.84f, 1f);
            Color c = Color.Lerp(dark, Color.Lerp(tile, Color.white, 0.26f), shine);
            c = Color.Lerp(c, JointColor, joint * 0.9f);

            float rim = Smooth(0.86f, 0.92f, d);
            c = Color.Lerp(c, Color.Lerp(RimPink, Color.white, 0.35f), rim);
            c.a = Mathf.Lerp(DanceFloorAlpha, 1f, rim) * (1f - Smooth(0.95f, 0.995f, d));
            return c;
        }

        /// <summary>Color de una baldosa: un revoltijo fijo de su columna, su fila y la variante, para que no se repita en filas.</summary>
        private static int TileIndex(int x, int y, int frame)
        {
            unchecked
            {
                int h = (x * 73856093) ^ (y * 19349663) ^ (frame * 83492791);
                h ^= h >> 13;
                return (h & 0x7fffffff) % TileColors.Length;
            }
        }

        /// <summary>Genera la textura con una función por píxel y la guarda como PNG. Se reescribe en cada ejecución del menú.</summary>
        private static Texture2D WriteTexture(string path, int width, int height, PixelFunction pixel, bool pixelArt)
        {
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
            return SaveTexture(path, width, height, pixels, pixelArt);
        }

        /// <summary>Convierte un dibujo de caracteres en textura de 8 bits (sin suavizar al ampliar).</summary>
        private static Texture2D WriteBitmap(string path, string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            Color clear = new Color(Ink.r, Ink.g, Ink.b, 0f);

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];   // la primera fila del dibujo es la de arriba
                for (int x = 0; x < width; x++)
                {
                    char symbol = x < row.Length ? row[x] : '.';
                    pixels[y * width + x] = symbol == '#' ? Color.white : (symbol == 'o' ? Ink : clear);
                }
            }
            return SaveTexture(path, width, height, pixels, true);
        }

        private static Texture2D SaveTexture(string path, int width, int height, Color[] pixels, bool pixelArt)
        {
            Texture2D generated = new Texture2D(width, height, TextureFormat.RGBA32, false);
            generated.SetPixels(pixels);
            generated.Apply();
            File.WriteAllBytes(path, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                FilterMode filter = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
                TextureImporterCompression compression = pixelArt ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
                bool changed = !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.filterMode != filter || importer.mipmapEnabled == pixelArt ||
                               importer.textureCompression != compression;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = filter;
                    importer.mipmapEnabled = !pixelArt;
                    importer.textureCompression = compression;
                    importer.SaveAndReimport();
                }
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) Debug.LogWarning("[BardVfxBuilder] No se pudo cargar la textura " + path + ".");
            return texture;
        }

        /// <summary>
        /// Material de partículas de URP, transparente, sin iluminación y con mezcla alfa. Se reconfigura en cada
        /// ejecución del menú. El color se deja en blanco: lo ponen la textura, la partícula o el script del efecto.
        /// </summary>
        private static Material WriteMaterial(string path, Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[BardVfxBuilder] No encuentro el shader 'Universal Render Pipeline/Particles/Unlit'.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(shader);
            else material.shader = shader;

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            // Lo mismo que hace el Inspector al elegir Surface Type = Transparent y Blending Mode = Alpha.
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
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
