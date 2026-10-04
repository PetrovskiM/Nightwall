using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Designer-facing definition for one level. Holds every knob a level needs:
    /// timing, grid dimensions, starting economy, available structures, wave schedule
    /// and win condition. The <see cref="LevelLoader"/> reads this at runtime and
    /// configures all scene systems accordingly before the first <c>Start()</c> runs.
    ///
    /// Grid dimensions and HQ position are authoritative descriptors; the SceneBuilder
    /// uses them when baking the scene so they stay consistent.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "Nightwall/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Metadata")]
        [Tooltip("Display name shown in the UI and logs.")]
        [SerializeField] string displayName = "Level";
        [Tooltip("1-based ordering index used by LevelRegistry.")]
        [Min(1)] [SerializeField] int levelNumber = 1;

        [Header("Grid")]
        [Tooltip("Playable grid width in cells. Kept consistent with the baked NavMesh scene.")]
        [Min(10)] [SerializeField] int gridWidth = 60;
        [Tooltip("Playable grid height in cells. Kept consistent with the baked NavMesh scene.")]
        [Min(10)] [SerializeField] int gridHeight = 60;
        [Tooltip("HQ position in grid coordinates (0,0 = south-west corner). Centre of the grid by default.")]
        [SerializeField] Vector2Int hqGridPosition;

        [Header("Economy")]
        [Tooltip("Materials the player starts with on day 1.")]
        [Min(0)] [SerializeField] int startingMaterials = 100;

        [Header("Phase durations (seconds)")]
        [Tooltip("How long the player has to build before the night begins.")]
        [Min(1f)] [SerializeField] float dayDuration = 30f;
        [Tooltip("Safety cap: night ends even if some enemies are still alive.")]
        [Min(5f)] [SerializeField] float nightMaxDuration = 90f;
        [Tooltip("Brief breather between a cleared night and the next day.")]
        [Min(1f)] [SerializeField] float dawnDuration = 4f;

        [Header("Waves")]
        [Tooltip("Total number of authored waves. 0 = run the procedural fallback indefinitely " +
                 "(ignored when WinCondition is Infinite).")]
        [Min(0)] [SerializeField] int numberOfWaves = 5;
        [Tooltip("Wave schedule (authored nights + procedural fallback). Each level owns its own " +
                 "LevelConfig so difficulty curves are independent.")]
        [SerializeField] LevelConfig waveConfig;

        [Header("Available Structures")]
        [Tooltip("Prefabs the BuildingPlacer exposes to the player. Ordered as they appear in the " +
                 "build bar. Levels can restrict the kit (e.g. tutorial only has Wall).")]
        [SerializeField] List<GameObject> availableStructures = new();

        [Header("Win Condition")]
        [SerializeField] WinConditionType winCondition = WinConditionType.SurviveAllWaves;

        // ── Public API ────────────────────────────────────────────────────────

        public string DisplayName       => displayName;
        public int    LevelNumber       => levelNumber;
        public int    GridWidth         => gridWidth;
        public int    GridHeight        => gridHeight;
        public Vector2Int HqGridPosition => hqGridPosition;
        public int    StartingMaterials => startingMaterials;
        public float  DayDuration       => dayDuration;
        public float  NightMaxDuration  => nightMaxDuration;
        public float  DawnDuration      => dawnDuration;
        public int    NumberOfWaves     => numberOfWaves;
        public LevelConfig WaveConfig   => waveConfig;
        public IReadOnlyList<GameObject> AvailableStructures => availableStructures;
        public WinConditionType WinCondition => winCondition;
    }
}
