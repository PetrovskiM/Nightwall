using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Applies a <see cref="LevelDefinition"/> to every scene system before the first
    /// <c>Start()</c> runs. Wire in the <c>GameSystems</c> object; set
    /// <see cref="currentLevel"/> to the desired level. The loader runs in <c>Awake()</c>
    /// so all receivers can still use their own <c>Start()</c> normally.
    ///
    /// Dependency: executes before <see cref="GameManager"/>, <see cref="WaveSpawner"/>
    /// and <see cref="BuildingPlacer"/> — ensure Script Execution Order or placement order
    /// in the GameObject keeps <c>LevelLoader.Awake</c> first (it is added first in the scene).
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [SerializeField] LevelDefinition currentLevel;

        [Header("Scene systems (auto-found if left empty)")]
        [SerializeField] GameManager gameManager;
        [SerializeField] WaveSpawner waveSpawner;
        [SerializeField] BuildingPlacer buildingPlacer;

        void Awake()
        {
            if (currentLevel == null)
            {
                Debug.LogError("[LevelLoader] No LevelDefinition assigned — using scene defaults.");
                return;
            }

            // Auto-find scene systems when the references were not pre-wired.
            if (gameManager == null)    gameManager    = FindFirstObjectByType<GameManager>();
            if (waveSpawner == null)    waveSpawner    = FindFirstObjectByType<WaveSpawner>();
            if (buildingPlacer == null) buildingPlacer = FindFirstObjectByType<BuildingPlacer>();

            if (gameManager    != null) gameManager.Configure(currentLevel);
            if (waveSpawner    != null) waveSpawner.Configure(currentLevel.WaveConfig);
            if (buildingPlacer != null) buildingPlacer.Configure(currentLevel.AvailableStructures);

            Debug.Log($"[LevelLoader] Loaded '{currentLevel.DisplayName}' " +
                      $"(day {currentLevel.DayDuration}s / night {currentLevel.NightMaxDuration}s / " +
                      $"{currentLevel.NumberOfWaves} waves / {currentLevel.WinCondition}).");
        }

        /// <summary>The active level definition (read-only after <c>Awake</c>).</summary>
        public LevelDefinition CurrentLevel => currentLevel;
    }
}
