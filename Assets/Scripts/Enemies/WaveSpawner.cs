using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// Spawns a single night's wave from whichever sides <see cref="AttackDirectionSelector"/>
    /// has chosen for this night. The <see cref="GameManager"/> owns the day/night loop: it calls
    /// <see cref="AttackDirectionSelector.SelectForWave"/> first (revealing the direction at
    /// nightfall), then calls <see cref="SpawnWave"/> here to produce the horde.
    ///
    /// Enemy counts and spawn cadence come from <see cref="LevelConfig"/>; spawn *positions* come
    /// from <see cref="AttackDirectionSelector"/>. Groups are spawned sequentially with a
    /// configurable inter-group delay, interleaved across active sides within each group.
    /// Difficulty modifiers from the <see cref="WaveDefinition"/> are applied on top of each
    /// archetype's base stats. The "maze" emerges from NavMesh rerouting — no scripted lanes.
    /// </summary>
    [RequireComponent(typeof(AttackDirectionSelector))]
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform hq;
        [Tooltip("Per-level wave schedule (enemy counts, cadence, difficulty).")]
        [SerializeField] LevelConfig levelConfig;
        [Tooltip("Archetype used when a group names none and for every procedural-fallback night.")]
        [SerializeField] EnemyDefinition defaultDefinition;
        [Tooltip("How close to a spawn position the NavMesh must be sampled for the enemy to appear.")]
        [SerializeField] float navSampleRadius = 4f;

        /// <summary>Enemies from the current wave still alive.</summary>
        public int AliveCount { get; private set; }
        /// <summary>Total enemies spawned in the current wave (includes dead ones).</summary>
        public int TotalSpawnedThisWave { get; private set; }
        /// <summary>True once every enemy in the current wave has been spawned.</summary>
        public bool SpawningComplete { get; private set; } = true;
        /// <summary>The wave definition used for the currently-running (or last-run) wave, for preview.</summary>
        public WaveDefinition? CurrentWaveDefinition { get; private set; }

        AttackDirectionSelector _selector;
        Coroutine _spawn;

        // Each element is one spawn group: a list of (worldPosition, archetype) pairs.
        // Groups are separated at runtime by a groupDelay pause.
        readonly List<List<(Vector3 pos, EnemyDefinition def)>> _spawnGroups = new();

        void Awake()
        {
            _selector = GetComponent<AttackDirectionSelector>();

            if (levelConfig == null) levelConfig = Resources.Load<LevelConfig>("LevelConfig");
            if (defaultDefinition == null) defaultDefinition = Resources.Load<EnemyDefinition>("Enemy_Basic");
        }

        /// <summary>
        /// Applied by <see cref="LevelLoader"/> in <c>Awake</c>. Replaces the serialized
        /// <see cref="LevelConfig"/> with the level's own wave schedule.
        /// </summary>
        public void Configure(LevelConfig config)
        {
            if (config != null) levelConfig = config;
        }

        /// <summary>
        /// Returns a read-only snapshot of the definition for the given 1-based wave number,
        /// or null when the wave falls past the authored list (procedural). Used by the preview HUD.
        /// </summary>
        public WaveDefinition? PeekWaveDefinition(int waveNumber)
        {
            if (levelConfig != null && levelConfig.TryGetWave(waveNumber, out WaveDefinition w))
                return w;
            return null;
        }

        /// <summary>Spawn the wave for the given 1-based wave number. Call after <see cref="AttackDirectionSelector.SelectForWave"/>.</summary>
        public void SpawnWave(int waveNumber)
        {
            StopAll();
            if (enemyPrefab == null)
            {
                SpawningComplete = true;
                return;
            }

            BuildSpawnGroups(waveNumber, out float interval, out float groupDelay, out DifficultyModifier mod);
            if (_spawnGroups.Count == 0)
            {
                SpawningComplete = true;
                return;
            }

            SpawningComplete = false;
            AliveCount = 0;
            TotalSpawnedThisWave = 0;
            _spawn = StartCoroutine(SpawnRoutine(interval, groupDelay, mod));
        }

        /// <summary>Stop spawning the remainder of the current wave; enemies already alive are unaffected.</summary>
        public void StopAll()
        {
            if (_spawn != null) StopCoroutine(_spawn);
            _spawn = null;
            _spawnGroups.Clear();
            SpawningComplete = true;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fills <see cref="_spawnGroups"/> with ordered spawn batches. Authored <see cref="WaveDefinition"/>
        /// groups are preserved as separate batches (separated by <paramref name="groupDelay"/>).
        /// When no authored definition exists a single procedural batch covers all active sides.
        /// Within each batch enemies are round-robin interleaved across active sides.
        /// </summary>
        void BuildSpawnGroups(int waveNumber, out float interval, out float groupDelay, out DifficultyModifier mod)
        {
            _spawnGroups.Clear();
            interval   = 0.6f;
            groupDelay = 0f;
            mod        = default;

            IReadOnlyList<AttackSide> activeSides = _selector != null
                ? _selector.ActiveSides
                : System.Array.Empty<AttackSide>();

            if (activeSides.Count == 0)
            {
                Debug.LogWarning("[WaveSpawner] No active sides selected. Did GameManager call SelectForWave first?");
                return;
            }

            if (levelConfig != null && levelConfig.TryGetWave(waveNumber, out WaveDefinition wave))
            {
                CurrentWaveDefinition = wave;
                interval   = wave.spawnInterval;
                groupDelay = wave.groupDelay;
                mod        = wave.difficultyModifier;

                bool hasGroups = wave.groups != null && wave.groups.Length > 0;
                if (hasGroups)
                {
                    // Each authored SpawnGroup becomes its own spawn batch, using interleaving
                    // across active sides if the group's side isn't active.
                    foreach (SpawnGroup g in wave.groups)
                    {
                        if (g.count <= 0) continue;
                        AttackSide side = activeSides.Contains(g.side) ? g.side : activeSides[0];
                        EnemyDefinition def = g.enemyDefinition != null ? g.enemyDefinition : defaultDefinition;
                        var batch = new List<(Vector3, EnemyDefinition)>(g.count);
                        for (int k = 0; k < g.count; k++)
                            batch.Add((_selector != null ? _selector.GetRandomSpawnPosition(side) : Vector3.zero, def));
                        _spawnGroups.Add(batch);
                    }
                }

                // Fall back to procedural if no authored groups had nonzero counts.
                if (_spawnGroups.Count == 0)
                    AddProceduralBatch(waveNumber, activeSides);
            }
            else
            {
                CurrentWaveDefinition = null;
                interval = levelConfig != null ? levelConfig.ProceduralSpawnInterval : 0.6f;
                AddProceduralBatch(waveNumber, activeSides);
            }
        }

        void AddProceduralBatch(int waveNumber, IReadOnlyList<AttackSide> activeSides)
        {
            int total = levelConfig != null
                ? levelConfig.ProceduralCount(waveNumber)
                : 5 + 3 * Mathf.Max(0, waveNumber - 1);

            int n = activeSides.Count;
            int baseEach = total / n;
            int extra = total % n;

            // Build one round-robin interleaved batch across all active sides.
            var sideCounts = new int[n];
            for (int i = 0; i < n; i++)
                sideCounts[i] = baseEach + (i < extra ? 1 : 0);

            var batch = new List<(Vector3, EnemyDefinition)>(total);
            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < n; i++)
                {
                    if (sideCounts[i] <= 0) continue;
                    batch.Add((_selector != null
                        ? _selector.GetRandomSpawnPosition(activeSides[i])
                        : Vector3.zero, defaultDefinition));
                    sideCounts[i]--;
                    any = true;
                }
            }

            if (batch.Count > 0) _spawnGroups.Add(batch);
        }

        IEnumerator SpawnRoutine(float interval, float groupDelay, DifficultyModifier mod)
        {
            var wait      = new WaitForSeconds(interval);
            var waitGroup = groupDelay > 0f ? new WaitForSeconds(groupDelay) : null;

            for (int g = 0; g < _spawnGroups.Count; g++)
            {
                if (g > 0 && waitGroup != null)
                    yield return waitGroup;

                var batch = _spawnGroups[g];
                for (int i = 0; i < batch.Count; i++)
                {
                    Enemy enemy = SpawnEnemy(batch[i].pos, batch[i].def, mod);
                    if (enemy != null)
                    {
                        enemy.Died += _ => AliveCount--;
                        AliveCount++;
                        TotalSpawnedThisWave++;
                    }
                    yield return wait;
                }
            }

            SpawningComplete = true;
        }

        Enemy SpawnEnemy(Vector3 worldPos, EnemyDefinition definition, DifficultyModifier mod)
        {
            if (enemyPrefab == null) return null;

            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                worldPos = hit.position;

            var go = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
            var enemy = go.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Init(hq);
                enemy.ApplyDefinition(definition);
                enemy.ApplyDifficultyModifier(mod);
            }
            return enemy;
        }
    }
}
