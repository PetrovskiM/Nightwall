using System.Collections;
using UnityEngine;

namespace Nightwall
{
    /// <summary>Spawns escalating waves of enemies from the spawn points toward the HQ.</summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] GameObject enemyPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform hq;
        [SerializeField] int baseCount = 5;
        [SerializeField] int countPerWave = 3;
        [SerializeField] float spawnInterval = 0.6f;
        [SerializeField] float timeBetweenWaves = 8f;
        [SerializeField] float waveGraceTimeout = 60f;
        [SerializeField] bool autoStart = true;

        int _wave;
        int _alive;
        Coroutine _loop;

        void Start()
        {
            if (autoStart) Begin();
        }

        public void Begin()
        {
            if (_loop == null) _loop = StartCoroutine(RunWaves());
        }

        public void StopAll()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        IEnumerator RunWaves()
        {
            while (true)
            {
                _wave++;
                if (GameManager.Instance != null) GameManager.Instance.SetWaveNumber(_wave);

                int count = baseCount + countPerWave * (_wave - 1);
                yield return SpawnWave(count);

                float t = 0f;
                while (_alive > 0 && t < waveGraceTimeout) { t += Time.deltaTime; yield return null; }
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        IEnumerator SpawnWave(int count)
        {
            if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0) yield break;

            for (int i = 0; i < count; i++)
            {
                Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
                var go = Instantiate(enemyPrefab, sp.position, Quaternion.identity);
                var enemy = go.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.Init(hq);
                    enemy.Died += _ => _alive--;
                    _alive++;
                }
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }
}
