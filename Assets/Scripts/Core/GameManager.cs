using System;
using System.Collections;
using UnityEngine;

namespace Nightwall
{
    /// <summary>The phases of the core gameplay loop.</summary>
    public enum GameState { Day, Night, Dawn, GameOver }

    /// <summary>
    /// Owns the day/night gameplay loop and run-wide state. Drives phase transitions on
    /// configurable timers, tells the <see cref="WaveSpawner"/> when to unleash a night's horde,
    /// and ends the run when the HQ falls. Everything else reads state from here; nothing writes
    /// back (one-directional flow: gameplay → state → UI).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] Hq hq;
        [SerializeField] WaveSpawner waveSpawner;
        [SerializeField] AttackDirectionSelector attackDirectionSelector;

        [Header("Phase durations (seconds)")]
        [Tooltip("How long the player has to build before the horde arrives.")]
        [SerializeField] float dayDuration = 30f;
        [Tooltip("Safety cap: the night ends even if some enemies are still alive.")]
        [SerializeField] float nightMaxDuration = 90f;
        [Tooltip("Short breather after a wave is resolved before the next day begins.")]
        [SerializeField] float dawnDuration = 4f;

        /// <summary>Raised whenever the phase changes. Argument is the new state.</summary>
        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Day;
        /// <summary>1-based day counter; also the wave number spawned each night.</summary>
        public int Day { get; private set; } = 1;
        /// <summary>Seconds left in the current timed phase (0 when the phase has no countdown).</summary>
        public float PhaseTimeRemaining { get; private set; }
        /// <summary>True only during the Day phase, when the player may build and remove.</summary>
        public bool CanBuild => State == GameState.Day;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start() => StartCoroutine(RunLoop());

        /// <summary>Day → Night → Dawn, forever, until the HQ is destroyed.</summary>
        IEnumerator RunLoop()
        {
            while (State != GameState.GameOver)
            {
                yield return RunPhase(GameState.Day, dayDuration);
                if (State == GameState.GameOver) yield break;

                yield return RunNight();
                if (State == GameState.GameOver) yield break;

                yield return RunPhase(GameState.Dawn, dawnDuration);
                if (State == GameState.GameOver) yield break;

                Day++;
            }
        }

        /// <summary>A plain timed phase: enter, count down, exit.</summary>
        IEnumerator RunPhase(GameState state, float duration)
        {
            Enter(state);
            PhaseTimeRemaining = duration;
            while (PhaseTimeRemaining > 0f && State != GameState.GameOver)
            {
                PhaseTimeRemaining -= Time.deltaTime;
                yield return null;
            }
            PhaseTimeRemaining = 0f;
        }

        /// <summary>
        /// Night: spawn this day's wave, then wait until the horde is cleared or the safety cap
        /// elapses. Stops any remaining spawning on the way out so Dawn starts quiet.
        /// </summary>
        IEnumerator RunNight()
        {
            Enter(GameState.Night);
            PhaseTimeRemaining = nightMaxDuration;

            // Select attack sides first (hidden during day, revealed now at nightfall).
            if (attackDirectionSelector != null) attackDirectionSelector.SelectForWave(Day);

            if (waveSpawner != null) waveSpawner.SpawnWave(Day);

            while (State != GameState.GameOver && PhaseTimeRemaining > 0f)
            {
                bool cleared = waveSpawner == null ||
                               (waveSpawner.SpawningComplete && waveSpawner.AliveCount == 0);
                if (cleared) break;
                PhaseTimeRemaining -= Time.deltaTime;
                yield return null;
            }
            PhaseTimeRemaining = 0f;
            if (waveSpawner != null) waveSpawner.StopAll();
        }

        void Enter(GameState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Applied by <see cref="LevelLoader"/> in <c>Awake</c>, before <c>Start</c> runs.
        /// Overrides the serialized phase durations with values from the level definition.
        /// </summary>
        public void Configure(LevelDefinition level)
        {
            dayDuration      = level.DayDuration;
            nightMaxDuration = level.NightMaxDuration;
            dawnDuration     = level.DawnDuration;
        }

        /// <summary>Called by the <see cref="Hq"/> when its <see cref="Health"/> reaches zero.</summary>
        public void OnHqDestroyed()
        {
            if (State == GameState.GameOver) return;
            Enter(GameState.GameOver);
            PhaseTimeRemaining = 0f;
            Debug.Log("[Nightwall] HQ destroyed — Game Over.");
            if (waveSpawner != null) waveSpawner.StopAll();
        }
    }
}
