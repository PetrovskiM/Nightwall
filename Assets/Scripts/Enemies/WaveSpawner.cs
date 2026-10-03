using System.Collections;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Spawns a single night's wave of enemies on demand. The <see cref="GameManager"/> owns the
    /// day/night loop and calls <see cref="SpawnWave"/> at nightfall; this component only produces
    /// the horde and tracks how many of it remain alive so the night can resolve.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform hq;
        [SerializeField] int baseCount = 5;
        [SerializeField] int countPerWave = 3;
        [SerializeField] float spawnInterval = 0.6f;

        /// <summary>Enemies from the current wave still alive.</summary>
        public int AliveCount { get; private set; }
        /// <summary>True once every enemy in the current wave has been spawned.</summary>
        public bool SpawningComplete { get; private set; } = true;

        Coroutine _spawn;

        /// <summary>Spawn the wave for the given 1-based wave number (scales with the number).</summary>
        public void SpawnWave(int waveNumber)
        {
            StopAll();
            int count = baseCount + countPerWave * Mathf.Max(0, waveNumber - 1);
            SpawningComplete = false;
            _spawn = StartCoroutine(SpawnRoutine(count));
        }

        /// <summary>
        /// Stop spawning the remainder of the current wave. Enemies already on the field live on;
        /// the night normally ends only once they are all dead.
        /// </summary>
        public void StopAll()
        {
            if (_spawn != null) StopCoroutine(_spawn);
            _spawn = null;
            SpawningComplete = true;
        }

        IEnumerator SpawnRoutine(int count)
        {
            if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                SpawningComplete = true;
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
                var go = Instantiate(enemyPrefab, sp.position, Quaternion.identity);
                var enemy = go.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.Init(hq);
                    enemy.Died += _ => AliveCount--;
                    AliveCount++;
                }
                yield return new WaitForSeconds(spawnInterval);
            }
            SpawningComplete = true;
        }
    }
}
