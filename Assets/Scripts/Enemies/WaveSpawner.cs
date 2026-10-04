using System;
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
    /// from <see cref="AttackDirectionSelector"/>, which distributes the budget across active sides
    /// using randomised positions along each edge. The "maze" emerges from NavMesh rerouting around
    /// whatever walls the player placed — no scripted lanes.
    /// </summary>
    [RequireComponent(typeof(AttackDirectionSelector))]
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform hq;
        [Tooltip("Per-level wave schedule (enemy counts, cadence).")]
        [SerializeField] LevelConfig levelConfig;
        [Tooltip("Archetype used when a group names none and for every procedural-fallback night.")]
        [SerializeField] EnemyDefinition defaultDefinition;
        [Tooltip("How close to a spawn position the NavMesh must be sampled for the enemy to appear.")]
        [SerializeField] float navSampleRadius = 4f;

        /// <summary>Enemies from the current wave still alive.</summary>
        public int AliveCount { get; private set; }
        /// <summary>True once every enemy in the current wave has been spawned.</summary>
        public bool SpawningComplete { get; private set; } = true;

        AttackDirectionSelector _selector;
        Coroutine _spawn;

        // Reused across waves; each entry is (worldPosition, archetype).
        readonly List<Vector3> _spawnPositions = new();
        readonly List<EnemyDefinition> _archetypes = new();

        void Awake()
        {
            _selector = GetComponent<AttackDirectionSelector>();

            if (levelConfig == null) levelConfig = Resources.Load<LevelConfig>("LevelConfig");
            if (defaultDefinition == null) defaultDefinition = Resources.Load<EnemyDefinition>("Enemy_Basic");
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

            BuildSpawnOrder(waveNumber, out float interval);
            if (_spawnPositions.Count == 0)
            {
                SpawningComplete = true;
                return;
            }

            SpawningComplete = false;
            _spawn = StartCoroutine(SpawnRoutine(interval));
        }

        /// <summary>Stop spawning the remainder of the current wave; enemies already alive are unaffected.</summary>
        public void StopAll()
        {
            if (_spawn != null) StopCoroutine(_spawn);
            _spawn = null;
            _spawnPositions.Clear();
            _archetypes.Clear();
            SpawningComplete = true;
        }

        // ── Internal ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fills <see cref="_spawnPositions"/> and <see cref="_archetypes"/> with a round-robin
        /// interleaved sequence across all active sides so the horde arrives from every direction
        /// simultaneously rather than one side draining before the next begins.
        /// </summary>
        void BuildSpawnOrder(int waveNumber, out float interval)
        {
            _spawnPositions.Clear();
            _archetypes.Clear();
            interval = 0.6f;

            IReadOnlyList<AttackSide> activeSides = _selector != null
                ? _selector.ActiveSides
                : System.Array.Empty<AttackSide>();

            if (activeSides.Count == 0)
            {
                Debug.LogWarning("[WaveSpawner] No active sides selected. Did GameManager call SelectForWave first?");
                return;
            }

            // Collect per-side (count, archetype) budget.
            var sideCounts = new Dictionary<AttackSide, int>();
            var sideArchetypes = new Dictionary<AttackSide, EnemyDefinition>();

            foreach (AttackSide s in activeSides)
            {
                sideCounts[s] = 0;
                sideArchetypes[s] = defaultDefinition;
            }

            if (levelConfig != null && levelConfig.TryGetWave(waveNumber, out WaveDefinition wave))
            {
                interval = wave.spawnInterval;
                if (wave.groups != null)
                {
                    foreach (SpawnGroup g in wave.groups)
                    {
                        // Authored groups that target an inactive side are redistributed to an
                        // active side (round-robin) so the total enemy count is always honoured.
                        AttackSide target = activeSides.Contains(g.side) ? g.side : activeSides[0];
                        sideCounts[target] += g.count;
                        if (g.enemyDefinition != null) sideArchetypes[target] = g.enemyDefinition;
                    }
                }

                // If no authored groups addressed active sides, fall back to procedural count.
                int totalAuthored = 0;
                foreach (int c in sideCounts.Values) totalAuthored += c;
                if (totalAuthored == 0)
                    DistributeProceduralCount(waveNumber, activeSides, sideCounts);
            }
            else
            {
                interval = levelConfig != null ? levelConfig.ProceduralSpawnInterval : 0.6f;
                DistributeProceduralCount(waveNumber, activeSides, sideCounts);
            }

            // Build round-robin interleaved order across all active sides.
            var pendingSides = new List<AttackSide>(activeSides);
            var pendingCounts = new List<int>();
            var pendingDefs = new List<EnemyDefinition>();
            foreach (AttackSide s in pendingSides)
            {
                pendingCounts.Add(sideCounts[s]);
                pendingDefs.Add(sideArchetypes[s]);
            }

            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < pendingSides.Count; i++)
                {
                    if (pendingCounts[i] <= 0) continue;
                    _spawnPositions.Add(_selector != null
                        ? _selector.GetRandomSpawnPosition(pendingSides[i])
                        : Vector3.zero);
                    _archetypes.Add(pendingDefs[i]);
                    pendingCounts[i]--;
                    any = true;
                }
            }
        }

        void DistributeProceduralCount(int waveNumber, IReadOnlyList<AttackSide> activeSides,
            Dictionary<AttackSide, int> sideCounts)
        {
            int total = levelConfig != null
                ? levelConfig.ProceduralCount(waveNumber)
                : 5 + 3 * Mathf.Max(0, waveNumber - 1);
            int n = activeSides.Count;
            int baseEach = total / n;
            int extra = total % n;
            for (int i = 0; i < n; i++)
                sideCounts[activeSides[i]] += baseEach + (i < extra ? 1 : 0);
        }

        IEnumerator SpawnRoutine(float interval)
        {
            var wait = new WaitForSeconds(interval);
            for (int i = 0; i < _spawnPositions.Count; i++)
            {
                Enemy enemy = SpawnEnemy(_spawnPositions[i], _archetypes[i]);
                if (enemy != null)
                {
                    enemy.Died += _ => AliveCount--;
                    AliveCount++;
                }
                yield return wait;
            }
            SpawningComplete = true;
        }

        Enemy SpawnEnemy(Vector3 worldPos, EnemyDefinition definition)
        {
            if (enemyPrefab == null) return null;

            // Snap to the nearest NavMesh position so the agent always starts on walkable ground.
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                worldPos = hit.position;

            var go = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
            var enemy = go.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Init(hq);
                enemy.ApplyDefinition(definition);
            }
            return enemy;
        }
    }
}
