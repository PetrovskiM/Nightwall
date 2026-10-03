using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Shared identity/metadata for a placeable defensive structure (wall, reinforced wall, gate,
    /// ...). It carries only the designer-authored descriptors the building system needs to offer a
    /// structure for placement — a display name and a construction cost — while the concrete
    /// behaviour is composed from sibling components: <see cref="Health"/> (hit points),
    /// <see cref="Buildable"/> (grid footprint + lifetime) and, where present, a carving
    /// <see cref="UnityEngine.AI.NavMeshObstacle"/> (blocks the horde's path).
    ///
    /// Plain walls use this component directly; structures that need extra behaviour derive from it
    /// (see <see cref="Gate"/>). A new structure type is therefore just a new prefab with this
    /// component and authored values — no change to <see cref="BuildingPlacer"/> is required.
    /// </summary>
    [RequireComponent(typeof(Buildable))]
    public class DefensiveStructure : MonoBehaviour
    {
        [Tooltip("Short name shown in the build UI.")]
        [SerializeField] string displayName = "Structure";

        [Tooltip("Resource cost to construct. Tunable per structure; spent by the economy later.")]
        [SerializeField] int cost = 10;

        /// <summary>Human-readable name for the build UI.</summary>
        public string DisplayName => displayName;

        /// <summary>Construction cost of one instance of this structure.</summary>
        public int Cost => cost;
    }
}
