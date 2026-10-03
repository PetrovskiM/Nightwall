using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Spawns a single night's wave from one or more <see cref="SpawnPoint"/> entrances around the
    /// map. The <see cref="GameManager"/> owns the day/night loop and calls <see cref="SpawnWave"/>
    /// at nightfall; this component only produces the horde, decides which entrances are active and
    /// how many each contributes (from the <see cref="LevelConfig"/>), and tracks how many enemies
    /// remain alive so the night can resolve.
    ///
    /// Routing is not scripted here: each entrance drops its enemies at a different edge and the
    /// enemy's own NavMesh pathing closes on the base, so a multi-entrance wave is felt as pressure
    /// from several directions at once.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [Tooltip("Every entrance on this map, in a stable order. Waves reference them by index.")]
        [SerializeField] SpawnPoint[] spawnPoints;
        [SerializeField] Transform hq;
        [Tooltip("Per-level wave schedule (which entrances, how many each, cadence).")]
        [SerializeField] LevelConfig levelConfig;

        /// <summary>Enemies from the current wave still alive.</summary>
        public int AliveCount { get; private set; }
        /// <summary>True once every enemy in the current wave has been spawned.</summary>
        public bool SpawningComplete { get; private set; } = true;

        Coroutine _spawn;
        // Reused across waves so round-robin interleaving allocates nothing per spawn.
        readonly List<SpawnPoint> _order = new List<SpawnPoint>();

        void Awake()
        {
            // Recover the schedule from Resources if the (binary) scene dropped the reference.
            if (levelConfig == null) levelConfig = Resources.Load<LevelConfig>("LevelConfig");
        }

        /// <summary>Spawn the wave for the given 1-based wave number.</summary>
        public void SpawnWave(int waveNumber)
        {
            StopAll();
            if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                SpawningComplete = true;
                return;
            }

            BuildSpawnOrder(waveNumber, out float interval);
            if (_order.Count == 0)
            {
                SpawningComplete = true;
                return;
            }

            SpawningComplete = false;
            _spawn = StartCoroutine(SpawnRoutine(interval));
        }

        /// <summary>
        /// Stop spawning the remainder of the current wave. Enemies already on the field live on;
        /// the night normally ends only once they are all dead.
        /// </summary>
        public void StopAll()
        {
            if (_spawn != null) StopCoroutine(_spawn);
            _spawn = null;
            _order.Clear();
            SpawningComplete = true;
        }

        /// <summary>
        /// Fill <see cref="_order"/> with the exact sequence of entrances to spawn from this wave,
        /// round-robin-interleaved across the active ones so the horde arrives from every active
        /// direction at once rather than one entrance fully draining before the next begins.
        /// </summary>
        void BuildSpawnOrder(int waveNumber, out float interval)
        {
            _order.Clear();

            // Per-entrance remaining counts for this wave.
            int[] remaining = new int[spawnPoints.Length];
            interval = 0.6f;

            if (levelConfig != null && levelConfig.TryGetWave(waveNumber, out WaveDefinition wave))
            {
                interval = wave.spawnInterval;
                if (wave.groups != null)
                {
                    foreach (SpawnGroup g in wave.groups)
                    {
                        if (g.spawnPointIndex < 0 || g.spawnPointIndex >= spawnPoints.Length) continue;
                        if (spawnPoints[g.spawnPointIndex] == null) continue;
                        remaining[g.spawnPointIndex] += Mathf.Max(0, g.count);
                    }
                }
            }
            else
            {
                // Procedural fallback: spread the budget evenly across every valid entrance.
                int valid = 0;
                for (int i = 0; i < spawnPoints.Length; i++) if (spawnPoints[i] != null) valid++;
                if (valid == 0) return;

                int total = levelConfig != null
                    ? levelConfig.ProceduralCount(waveNumber)
                    : 5 + 3 * Mathf.Max(0, waveNumber - 1);
                interval = levelConfig != null ? levelConfig.ProceduralSpawnInterval : 0.6f;

                int baseEach = total / valid;
                int extra = total % valid; // distribute the remainder to the first few entrances
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (spawnPoints[i] == null) continue;
                    remaining[i] = baseEach + (extra-- > 0 ? 1 : 0);
                }
            }

            // Round-robin the remaining counts into a single interleaved order.
            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    if (remaining[i] <= 0) continue;
                    _order.Add(spawnPoints[i]);
                    remaining[i]--;
                    any = true;
                }
            }
        }

        IEnumerator SpawnRoutine(float interval)
        {
            var wait = new WaitForSeconds(interval);
            for (int i = 0; i < _order.Count; i++)
            {
                SpawnPoint sp = _order[i];
                Enemy enemy = sp != null ? sp.Spawn(enemyPrefab, hq) : null;
                if (enemy != null)
                {
                    enemy.Died += _ => AliveCount--;
                    AliveCount++;
                }
                yield return wait;
            }
            SpawningComplete = true;
        }
    }
}
