using UnityEngine;

namespace Nightwall
{
    public enum GamePhase { Prep, Wave, GameOver }

    /// <summary>Owns high-level run state: the current phase and wave counter.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] Hq hq;
        [SerializeField] WaveSpawner waveSpawner;

        public GamePhase Phase { get; private set; } = GamePhase.Prep;
        public int Wave { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>Kick off the wave loop (the spawner may also auto-start itself).</summary>
        public void StartWaves()
        {
            if (Phase == GamePhase.GameOver) return;
            Phase = GamePhase.Wave;
            if (waveSpawner != null) waveSpawner.Begin();
        }

        public void SetWaveNumber(int n) => Wave = n;

        public void OnHqDestroyed()
        {
            if (Phase == GamePhase.GameOver) return;
            Phase = GamePhase.GameOver;
            Debug.Log("[Nightwall] HQ destroyed — Game Over.");
            if (waveSpawner != null) waveSpawner.StopAll();
        }
    }
}
