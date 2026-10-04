using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A single per-area traversal cost override in a <see cref="NavCostProfile"/>.
    /// The area index maps to a NavMesh area defined in Unity's Navigation settings:
    ///   0 = Walkable (built-in)   1 = Not Walkable (built-in)   2 = Jump (built-in)
    ///   3+ = project-defined custom areas (e.g. "NearWall", "Slow", "OpenGround").
    /// Set a high multiplier (e.g. 5) to make the archetype avoid that area, a low one
    /// (&lt;1, min 0.01) to make it prefer it. Cost 1 = Unity default.
    /// </summary>
    [System.Serializable]
    public struct NavAreaCostOverride
    {
        [Tooltip("NavMesh area index (0=Walkable, 1=NotWalkable, 2=Jump; 3+ are project custom areas).")]
        [Range(0, 31)] public int areaIndex;

        [Tooltip("Traversal cost multiplier. >1 = avoid this area, <1 = prefer it, 1 = Unity default.")]
        [Min(0.01f)] public float costMultiplier;
    }

    /// <summary>
    /// Designer-facing stat block for one enemy archetype (Basic, Runner, Brute, Swarm, ...). The
    /// shared <see cref="Enemy"/> component reads all its tunables from one of these at spawn time
    /// via <see cref="Enemy.ApplyDefinition"/>, so new archetypes are authored as data assets rather
    /// than new C# classes — per the project rule that tunables live in configs, not code.
    ///
    /// Only stats are held here; behaviour (path, breach-when-walled-out, trap slowing) is identical
    /// across archetypes and stays in <see cref="Enemy"/>. No special abilities yet.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Nightwall/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Tooltip("Human-readable archetype name, for debug/UI.")]
        [SerializeField] string displayName = "Enemy";

        [Header("Survivability")]
        [Tooltip("Hit points. Runners are frail; brutes are tanks.")]
        [Min(1f)] [SerializeField] float maxHealth = 40f;

        [Header("Locomotion")]
        [Tooltip("NavMesh movement speed. Runners are fast, brutes slow.")]
        [Min(0f)] [SerializeField] float moveSpeed = 3.5f;

        [Header("Wall combat")]
        [Tooltip("Sustained damage to a blocking structure while walled out. Brutes excel here.")]
        [Min(0f)] [SerializeField] float wallDamagePerSecond = 15f;
        [Tooltip("Seconds between strikes (attack cadence). DPS is preserved regardless of this.")]
        [Min(0.05f)] [SerializeField] float attackCooldown = 1f;

        [Header("Presentation")]
        [Tooltip("Uniform scale multiplier applied to the body and NavMesh agent footprint.")]
        [Min(0.1f)] [SerializeField] float size = 1f;
        [Tooltip("Optional body mesh instantiated as a child. When null, the base prefab body is " +
                 "kept and tinted with Body Color instead.")]
        [SerializeField] GameObject visualPrefab;
        [Tooltip("Placeholder tint for the base body when no Visual Prefab is supplied — keeps the " +
                 "archetypes tell-apart-able at phone scale.")]
        [SerializeField] Color bodyColor = new Color(0.9f, 0.25f, 0.25f);

        [Header("Navigation preferences")]
        [Tooltip("Per-area traversal cost overrides for this archetype. Leave empty for default " +
                 "Unity NavMesh costs (shortest geometric path). Add entries to make the archetype " +
                 "prefer or avoid custom NavMesh areas — e.g. a high cost on a 'NearWall' area " +
                 "steers Brutes onto open ground; a low cost on 'Fast' area makes Runners hug it. " +
                 "Requires matching NavMesh areas set up in the Navigation window and NavMeshModifier " +
                 "volumes placed in the scene.")]
        [SerializeField] NavAreaCostOverride[] navCostOverrides = System.Array.Empty<NavAreaCostOverride>();

        [Header("Economy")]
        [Tooltip("Point/budget value this archetype costs a wave to field (for spawn budgeting).")]
        [Min(0)] [SerializeField] int spawnCost = 1;

        /// <summary>Human-readable archetype name.</summary>
        public string DisplayName => displayName;
        /// <summary>Hit points for this archetype.</summary>
        public float MaxHealth => maxHealth;
        /// <summary>NavMesh movement speed.</summary>
        public float MoveSpeed => moveSpeed;
        /// <summary>Sustained wall damage while breaching.</summary>
        public float WallDamagePerSecond => wallDamagePerSecond;
        /// <summary>Seconds between strikes.</summary>
        public float AttackCooldown => attackCooldown;
        /// <summary>Uniform body/agent scale multiplier.</summary>
        public float Size => size;
        /// <summary>Optional child body mesh, or null to keep and tint the base body.</summary>
        public GameObject VisualPrefab => visualPrefab;
        /// <summary>Placeholder body tint used when <see cref="VisualPrefab"/> is null.</summary>
        public Color BodyColor => bodyColor;
        /// <summary>Spawn budget value for this archetype.</summary>
        public int SpawnCost => spawnCost;

        /// <summary>
        /// Per-area NavMesh traversal cost overrides for this archetype. Empty means use Unity's
        /// default costs (cost=1 for all walkable areas). Non-empty entries are applied to a
        /// <see cref="UnityEngine.AI.NavMeshQueryFilter"/> so paths are computed per-agent rather
        /// than relying on the global NavMesh area costs.
        /// </summary>
        public NavAreaCostOverride[] NavCostOverrides => navCostOverrides;

        /// <summary>True if this archetype has any non-default area cost overrides.</summary>
        public bool HasNavCostOverrides => navCostOverrides != null && navCostOverrides.Length > 0;
    }
}
