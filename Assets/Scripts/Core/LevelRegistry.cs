using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Ordered catalogue of every <see cref="LevelDefinition"/> in the game. Used by
    /// UI and progression systems to enumerate levels; the <see cref="LevelLoader"/>
    /// picks the active level from here by index.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelRegistry", menuName = "Nightwall/Level Registry")]
    public class LevelRegistry : ScriptableObject
    {
        [Tooltip("All levels in play order (index 0 = level 1).")]
        [SerializeField] List<LevelDefinition> levels = new();

        /// <summary>All registered levels in play order.</summary>
        public IReadOnlyList<LevelDefinition> Levels => levels;

        /// <summary>Total number of registered levels.</summary>
        public int Count => levels.Count;

        /// <summary>
        /// Returns the level at <paramref name="zeroBasedIndex"/>, or <c>null</c> if out of range.
        /// </summary>
        public LevelDefinition Get(int zeroBasedIndex) =>
            zeroBasedIndex >= 0 && zeroBasedIndex < levels.Count ? levels[zeroBasedIndex] : null;
    }
}
