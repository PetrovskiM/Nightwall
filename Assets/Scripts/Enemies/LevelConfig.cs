using System;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Multiplicative stat scalars applied to every enemy spawned in a wave. Multipliers default to
    /// zero (struct default), which the spawner treats as 1 (identity). Use values above 1 to make
    /// a wave harder, below 1 to make it easier.
    /// </summary>
    [Serializable]
    public struct DifficultyModifier
    {
        [Tooltip("Multiplier on every enemy's max HP for this wave. 0 or 1 = no change.")]
        [Min(0f)] public float healthMultiplier;
        [Tooltip("Multiplier on every enemy's move speed for this wave. 0 or 1 = no change.")]
        [Min(0f)] public float speedMultiplier;
        [Tooltip("Multiplier on every enemy's wall DPS for this wave. 0 or 1 = no change.")]
        [Min(0f)] public float damageMultiplier;

        /// <summary>True when all multipliers are effectively 1 (identity / unset).</summary>
        public bool IsIdentity =>
            Effective(healthMultiplier) == 1f &&
            Effective(speedMultiplier)  == 1f &&
            Effective(damageMultiplier) == 1f;

        /// <summary>Returns the multiplier, treating 0 as the identity value 1.</summary>
        public static float Effective(float m) => m <= 0f ? 1f : m;
    }

    /// <summary>
    /// How many enemies pour in from one attack side during a wave. The <see cref="AttackSide"/>
    /// is used as an authored hint only: <see cref="AttackDirectionSelector"/> always makes the
    /// final runtime decision about which sides are active each night, overriding any authored list.
    /// Author groups here mainly to control per-side enemy counts and archetypes.
    /// </summary>
    [Serializable]
    public struct SpawnGroup
    {
        [Tooltip("Which map edge this group comes from (runtime selection may override this).")]
        public AttackSide side;
        [Tooltip("How many enemies this side contributes to the wave.")]
        [Min(0)] public int count;
        [Tooltip("Which archetype this group fields. Leave empty to use the spawner's default.")]
        public EnemyDefinition enemyDefinition;
    }

    /// <summary>One authored night: enemy counts per side, spawn cadence, and difficulty scaling.</summary>
    [Serializable]
    public struct WaveDefinition
    {
        [Tooltip("Seconds between individual enemy spawns within a group.")]
        [Min(0.01f)] public float spawnInterval;
        [Tooltip("Extra seconds of silence between consecutive spawn groups. 0 = groups back-to-back.")]
        [Min(0f)] public float groupDelay;
        [Tooltip("Optional stat multipliers applied to every enemy spawned in this wave.")]
        public DifficultyModifier difficultyModifier;
        [Tooltip("Per-side groups for this wave. Sides not listed contribute 0 enemies.")]
        public SpawnGroup[] groups;
    }

    /// <summary>
    /// Designer-facing schedule for a level's nights: an ordered list of authored
    /// <see cref="WaveDefinition"/>s. Nights beyond the authored list fall back to a procedural
    /// ramp (<see cref="proceduralBaseCount"/> + <see cref="proceduralCountPerWave"/>) spread across
    /// every spawn point, so the loop survives indefinitely without hand-authoring every night.
    /// Shared data, so it lives in a <see cref="ScriptableObject"/> per the project's config rule.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Nightwall/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [Header("Authored waves (night 1 = element 0)")]
        [SerializeField] WaveDefinition[] waves;

        [Header("Procedural fallback (nights past the authored list)")]
        [Min(0)] [SerializeField] int proceduralBaseCount = 6;
        [Min(0)] [SerializeField] int proceduralCountPerWave = 3;
        [Min(0.01f)] [SerializeField] float proceduralSpawnInterval = 0.5f;

        /// <summary>Number of hand-authored waves.</summary>
        public int AuthoredWaveCount => waves != null ? waves.Length : 0;

        /// <summary>The authored wave for a 1-based number, or null when it falls past the list.</summary>
        public bool TryGetWave(int waveNumber, out WaveDefinition wave)
        {
            int i = waveNumber - 1;
            if (waves != null && i >= 0 && i < waves.Length)
            {
                wave = waves[i];
                return true;
            }
            wave = default;
            return false;
        }

        /// <summary>
        /// The total enemy budget for a procedural night, spread evenly across the given number of
        /// spawn points by the caller. Scales with the wave number so later nights are harder.
        /// </summary>
        public int ProceduralCount(int waveNumber) =>
            proceduralBaseCount + proceduralCountPerWave * Mathf.Max(0, waveNumber - 1);

        public float ProceduralSpawnInterval => proceduralSpawnInterval;
    }
}
