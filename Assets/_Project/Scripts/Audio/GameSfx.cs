using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using TheLastBeat.Heroes;
using TheLastBeat.Level;
using TheLastBeat.Player;
using TheLastBeat.Waves;
using UnityEngine;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// Hace sonar los efectos del nivel (tarea 32). Escucha los eventos que ya emiten el combate, las habilidades,
    /// la canción, las hordas y el flujo del nivel, y le pide cada sonido a SfxPlayer: ningún otro script sabe de audio.
    ///   Caballero: KnightCombat.AttackStarted, Ability.Started de Giro y Embestida, SpinAbility.Exploded,
    ///              KnightAutoDodge.Dodged, Health.Damaged y Died, BuffReceiver.BuffChanged, AbilityCaster.CooldownReady.
    ///   Golpes:    CombatEvents.Hit (solo los que dan los héroes: normal, pesado o Nota) y CombatEvents.Healed.
    ///   Bardo:     BardBasicAttack.Fired, Ability.Started de Golpe de bajo, BassDropAbility.Dropped,
    ///              DanceFloorAbility.Opened y Closed, Health.Damaged y Died, SongBuff.SongStateChanged.
    ///   Soldados:  CombatEvents.EnemyActed (aparece, prepara el golpe, lo lanza, muere).
    ///   Nivel:     WaveSpawner.WaveStarted y WaveCleared, LevelFlow.StateChanged.
    /// Los clips los asigna el menú "The Last Beat/Audio/Montar audio de la escena".
    /// </summary>
    public class GameSfx : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        [SerializeField] private LevelFlow level;
        [SerializeField] private WaveSpawner spawner;
        [SerializeField] private GameObject knight;
        [SerializeField] private GameObject bard;

        [Header("Caballero")]
        [SerializeField] private SfxCue knightSwing = new SfxCue { pitchVariation = 0.06f, minInterval = 0.08f };
        [SerializeField] private SfxCue knightSwingHeavy = new SfxCue { pitchVariation = 0.04f };
        [Tooltip("Dura 1 s, lo mismo que el Giro: sin variación de tono para que no cambie de duración.")]
        [SerializeField] private SfxCue spinWhirl = new SfxCue { pitchVariation = 0f, minInterval = 0.5f };
        [SerializeField] private SfxCue spinExplosion = new SfxCue { pitchVariation = 0.04f };
        [SerializeField] private SfxCue chargeDash = new SfxCue { pitchVariation = 0.03f };
        [SerializeField] private SfxCue dodge = new SfxCue { pitchVariation = 0.06f };
        [SerializeField] private SfxCue knightHurt = new SfxCue { pitchVariation = 0.05f, minInterval = 0.15f };
        [SerializeField] private SfxCue knightDeath = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue buffOn = new SfxCue { pitchVariation = 0f, minInterval = 0.5f };
        [SerializeField] private SfxCue buffOff = new SfxCue { pitchVariation = 0f, minInterval = 0.5f };
        [SerializeField] private SfxCue abilityReady = new SfxCue { pitchVariation = 0f };

        [Header("Golpes")]
        [SerializeField] private SfxCue hitLight = new SfxCue { pitchVariation = 0.08f, minInterval = 0.05f };
        [SerializeField] private SfxCue hitHeavy = new SfxCue { pitchVariation = 0.06f, minInterval = 0.06f };
        [SerializeField] private SfxCue hitNote = new SfxCue { pitchVariation = 0.05f, minInterval = 0.05f };

        [Header("Bardo")]
        [Tooltip("Cuatro notas de la escala de la canción: sin variación de tono para que no desafinen.")]
        [SerializeField] private SfxCue bardNote = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue bassDropCharge = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue bassDropBoom = new SfxCue { pitchVariation = 0.03f };
        [SerializeField] private SfxCue danceFloorOpen = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue danceFloorClose = new SfxCue { pitchVariation = 0f };
        [Tooltip("La pista cura a varios aliados en el mismo instante: el intervalo hace que suene una sola vez.")]
        [SerializeField] private SfxCue healTick = new SfxCue { pitchVariation = 0f, minInterval = 0.3f };
        [SerializeField] private SfxCue bardHurt = new SfxCue { pitchVariation = 0.05f, minInterval = 0.15f };
        [SerializeField] private SfxCue bardDeath = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue songInterrupt = new SfxCue { pitchVariation = 0f, minInterval = 0.3f };

        [Header("Demonio soldado")]
        [SerializeField] private SfxCue soldierSpawn = new SfxCue { pitchVariation = 0.08f, minInterval = 0.1f };
        [SerializeField] private SfxCue soldierWindup = new SfxCue { pitchVariation = 0.08f, minInterval = 0.12f };
        [SerializeField] private SfxCue soldierSwing = new SfxCue { pitchVariation = 0.08f, minInterval = 0.08f };
        [SerializeField] private SfxCue soldierDeath = new SfxCue { pitchVariation = 0.08f, minInterval = 0.08f };

        [Header("Nivel")]
        [SerializeField] private SfxCue waveStart = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue waveCleared = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue victory = new SfxCue { pitchVariation = 0f };
        [SerializeField] private SfxCue defeat = new SfxCue { pitchVariation = 0f };

        private KnightCombat knightCombat;
        private SpinAbility spin;
        private ChargeAbility charge;
        private KnightAutoDodge autoDodge;
        private Health knightHealth;
        private BuffReceiver knightBuff;
        private AbilityCaster knightCaster;

        private BardBasicAttack bardAttack;
        private BassDropAbility bassDrop;
        private DanceFloorAbility danceFloor;
        private Health bardHealth;
        private SongBuff song;

        private void Awake()
        {
            if (level == null) level = FindFirstObjectByType<LevelFlow>();
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (knight == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null) knight = player.gameObject;
            }
            if (bard == null)
            {
                BardCompanion companion = FindFirstObjectByType<BardCompanion>();
                if (companion != null) bard = companion.gameObject;
            }

            if (knight != null)
            {
                knightCombat = knight.GetComponentInChildren<KnightCombat>(true);
                spin = knight.GetComponentInChildren<SpinAbility>(true);
                charge = knight.GetComponentInChildren<ChargeAbility>(true);
                autoDodge = knight.GetComponentInChildren<KnightAutoDodge>(true);
                knightHealth = knight.GetComponent<Health>();
                knightBuff = knight.GetComponent<BuffReceiver>();
                knightCaster = knight.GetComponent<AbilityCaster>();
            }
            if (bard != null)
            {
                bardAttack = bard.GetComponentInChildren<BardBasicAttack>(true);
                bassDrop = bard.GetComponentInChildren<BassDropAbility>(true);
                danceFloor = bard.GetComponentInChildren<DanceFloorAbility>(true);
                bardHealth = bard.GetComponent<Health>();
                song = bard.GetComponent<SongBuff>();
            }

            SfxCue[] all =
            {
                knightSwing, knightSwingHeavy, spinWhirl, spinExplosion, chargeDash, dodge, knightHurt, knightDeath,
                buffOn, buffOff, abilityReady, hitLight, hitHeavy, hitNote, bardNote, bassDropCharge, bassDropBoom,
                danceFloorOpen, danceFloorClose, healTick, bardHurt, bardDeath, songInterrupt, soldierSpawn,
                soldierWindup, soldierSwing, soldierDeath, waveStart, waveCleared, victory, defeat
            };
            for (int i = 0; i < all.Length; i++) all[i].Preload();
        }

        private void OnEnable()
        {
            CombatEvents.Hit += OnHit;
            CombatEvents.Healed += OnHealed;
            CombatEvents.EnemyActed += OnEnemyActed;

            if (knightCombat != null) knightCombat.AttackStarted += OnAttackStarted;
            if (spin != null)
            {
                spin.Started += OnSpinStarted;
                spin.Exploded += OnSpinExploded;
            }
            if (charge != null) charge.Started += OnChargeStarted;
            if (autoDodge != null) autoDodge.Dodged += OnDodged;
            if (knightHealth != null)
            {
                knightHealth.Damaged += OnKnightDamaged;
                knightHealth.Died += OnKnightDied;
            }
            if (knightBuff != null) knightBuff.BuffChanged += OnKnightBuffChanged;
            if (knightCaster != null) knightCaster.CooldownReady += OnCooldownReady;

            if (bardAttack != null) bardAttack.Fired += OnNoteFired;
            if (bassDrop != null)
            {
                bassDrop.Started += OnBassStarted;
                bassDrop.Dropped += OnBassDropped;
            }
            if (danceFloor != null)
            {
                danceFloor.Opened += OnFloorOpened;
                danceFloor.Closed += OnFloorClosed;
            }
            if (bardHealth != null)
            {
                bardHealth.Damaged += OnBardDamaged;
                bardHealth.Died += OnBardDied;
            }
            if (song != null) song.SongStateChanged += OnSongStateChanged;

            if (spawner != null)
            {
                spawner.WaveStarted += OnWaveStarted;
                spawner.WaveCleared += OnWaveCleared;
            }
            if (level != null) level.StateChanged += OnLevelStateChanged;
        }

        private void OnDisable()
        {
            CombatEvents.Hit -= OnHit;
            CombatEvents.Healed -= OnHealed;
            CombatEvents.EnemyActed -= OnEnemyActed;

            if (knightCombat != null) knightCombat.AttackStarted -= OnAttackStarted;
            if (spin != null)
            {
                spin.Started -= OnSpinStarted;
                spin.Exploded -= OnSpinExploded;
            }
            if (charge != null) charge.Started -= OnChargeStarted;
            if (autoDodge != null) autoDodge.Dodged -= OnDodged;
            if (knightHealth != null)
            {
                knightHealth.Damaged -= OnKnightDamaged;
                knightHealth.Died -= OnKnightDied;
            }
            if (knightBuff != null) knightBuff.BuffChanged -= OnKnightBuffChanged;
            if (knightCaster != null) knightCaster.CooldownReady -= OnCooldownReady;

            if (bardAttack != null) bardAttack.Fired -= OnNoteFired;
            if (bassDrop != null)
            {
                bassDrop.Started -= OnBassStarted;
                bassDrop.Dropped -= OnBassDropped;
            }
            if (danceFloor != null)
            {
                danceFloor.Opened -= OnFloorOpened;
                danceFloor.Closed -= OnFloorClosed;
            }
            if (bardHealth != null)
            {
                bardHealth.Damaged -= OnBardDamaged;
                bardHealth.Died -= OnBardDied;
            }
            if (song != null) song.SongStateChanged -= OnSongStateChanged;

            if (spawner != null)
            {
                spawner.WaveStarted -= OnWaveStarted;
                spawner.WaveCleared -= OnWaveCleared;
            }
            if (level != null) level.StateChanged -= OnLevelStateChanged;
        }

        // ---------- Caballero ----------

        private void OnAttackStarted(int step) => SfxPlayer.Play(step >= 2 ? knightSwingHeavy : knightSwing);
        private void OnSpinStarted(Ability ability) => SfxPlayer.Play(spinWhirl);
        private void OnSpinExploded(Vector3 center, float radius) => SfxPlayer.Play(spinExplosion);
        private void OnChargeStarted(Ability ability) => SfxPlayer.Play(chargeDash);
        private void OnDodged() => SfxPlayer.Play(dodge);
        private void OnKnightDied(Health health, DamageInfo info) => SfxPlayer.Play(knightDeath);
        private void OnCooldownReady(int slot) => SfxPlayer.Play(abilityReady);

        private void OnKnightDamaged(Health health, DamageInfo info)
        {
            // Con el golpe mortal solo suena la muerte. (Damaged llega antes de que Health marque IsDead.)
            if (health.CurrentHealth > 0f) SfxPlayer.Play(knightHurt);
        }

        private void OnKnightBuffChanged(bool buffed)
        {
            if (buffed)
            {
                SfxPlayer.Play(buffOn);
                return;
            }

            // Solo cuando sale del área de la canción: si la canción se cortó o él cayó, ya suena otro aviso.
            bool songOn = song == null || song.State == SongState.Singing;
            bool alive = knightHealth == null || !knightHealth.IsDead;
            if (songOn && alive) SfxPlayer.Play(buffOff);
        }

        // ---------- Golpes ----------

        private void OnHit(IDamageable target, DamageInfo info)
        {
            if (info.SourceTeam != Team.Heroes) return;   // los golpes a los héroes suenan con su propio quejido
            SfxPlayer.Play(info.Quiet ? hitNote : info.IsHeavy ? hitHeavy : hitLight);
        }

        private void OnHealed(Health target, float amount) => SfxPlayer.Play(healTick);

        // ---------- Bardo ----------

        private void OnNoteFired(Vector3 origin, Vector3 direction, float range, float speed) => SfxPlayer.Play(bardNote);
        private void OnBassStarted(Ability ability) => SfxPlayer.Play(bassDropCharge);
        private void OnBassDropped(Vector3 center, float radius) => SfxPlayer.Play(bassDropBoom);
        private void OnFloorOpened(Vector3 center, float radius, float seconds) => SfxPlayer.Play(danceFloorOpen);
        private void OnFloorClosed() => SfxPlayer.Play(danceFloorClose);
        private void OnBardDied(Health health, DamageInfo info) => SfxPlayer.Play(bardDeath);

        private void OnBardDamaged(Health health, DamageInfo info)
        {
            if (health.CurrentHealth > 0f) SfxPlayer.Play(bardHurt);
        }

        private void OnSongStateChanged(SongState state)
        {
            if (state == SongState.Interrupted) SfxPlayer.Play(songInterrupt);
        }

        // ---------- Soldados y nivel ----------

        private void OnEnemyActed(EnemyCue cue, Vector3 position)
        {
            switch (cue)
            {
                case EnemyCue.Spawned: SfxPlayer.Play(soldierSpawn); break;
                case EnemyCue.Windup: SfxPlayer.Play(soldierWindup); break;
                case EnemyCue.Swing: SfxPlayer.Play(soldierSwing); break;
                case EnemyCue.Died: SfxPlayer.Play(soldierDeath); break;
            }
        }

        private void OnWaveStarted(int index, Wave wave) => SfxPlayer.Play(waveStart);

        private void OnWaveCleared(int index)
        {
            // Tras la última horda viene la victoria, que tiene su propia fanfarria.
            if (spawner != null && index < spawner.WaveCount - 1) SfxPlayer.Play(waveCleared);
        }

        private void OnLevelStateChanged(GameState state)
        {
            if (state == GameState.Victory) SfxPlayer.Play(victory);
            else if (state == GameState.Defeat) SfxPlayer.Play(defeat);
        }
    }
}
