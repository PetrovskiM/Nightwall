using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Decides which map sides are attacked each night and provides randomised spawn positions
    /// along those sides. The selection is hidden from the player during the day and revealed only
    /// when <see cref="SelectForWave"/> is called at nightfall by <see cref="GameManager"/>.
    ///
    /// Each side has <see cref="spawnCellsPerSide"/> discrete world-space positions spread along
    /// the edge (inset from the corners). The selector picks 1–4 active sides per night, scaling
    /// the possible count by wave number via two <see cref="AnimationCurve"/>s so early nights are
    /// single-direction while later nights can assault from all four sides simultaneously.
    ///
    /// Debug gizmos draw every side region in a dim tint and the active sides in a vivid colour
    /// so designers can verify the selection without running the game.
    /// </summary>
    [RequireComponent(typeof(WaveSpawner))]
    public class AttackDirectionSelector : MonoBehaviour
    {
        [SerializeField] MapConfig map;

        [Header("Spawn zone geometry")]
        [Tooltip("How many discrete spawn positions to distribute along each edge.")]
        [SerializeField, Min(1)] int spawnCellsPerSide = 6;
        [Tooltip("World-unit gap kept from each corner so spawns land on the NavMesh, not outside it.")]
        [SerializeField, Min(0f)] float edgeInset = 3f;

        [Header("Difficulty scaling")]
        [Tooltip("Minimum active sides as a function of wave number. Values are clamped to [1,4].")]
        [SerializeField] AnimationCurve minSidesCurve = AnimationCurve.Linear(1, 1, 10, 1);
        [Tooltip("Maximum active sides as a function of wave number. Values are clamped to [1,4].")]
        [SerializeField] AnimationCurve maxSidesCurve = AnimationCurve.Linear(1, 1, 10, 4);

        [Header("Debug")]
        [SerializeField] bool showDebugGizmos = true;

        // Per-side pool of world-space spawn positions, computed once in Awake from MapConfig.
        readonly Dictionary<AttackSide, Vector3[]> _positions = new();

        /// <summary>The sides selected for the current night. Empty until <see cref="SelectForWave"/> runs.</summary>
        public IReadOnlyList<AttackSide> ActiveSides => _activeSides;
        readonly List<AttackSide> _activeSides = new();

        void Awake()
        {
            if (map == null) map = Resources.Load<MapConfig>("MapConfig");
            if (map == null)
            {
                Debug.LogError("[AttackDirectionSelector] MapConfig not assigned and none found in Resources.", this);
                return;
            }
            BuildPositions();
        }

        /// <summary>
        /// Call at the start of each night. Picks 1–4 active sides based on the wave number and
        /// the configured difficulty curves. Does NOT reveal the selection to the player — callers
        /// (e.g. a future UI) must opt-in to reading <see cref="ActiveSides"/> after this call.
        /// </summary>
        public void SelectForWave(int waveNumber)
        {
            int lo = Mathf.Clamp(Mathf.RoundToInt(minSidesCurve.Evaluate(waveNumber)), 1, 4);
            int hi = Mathf.Clamp(Mathf.RoundToInt(maxSidesCurve.Evaluate(waveNumber)), lo, 4);
            int count = Random.Range(lo, hi + 1);

            // Shuffle all four sides, then take the first `count`.
            var pool = new List<AttackSide> { AttackSide.North, AttackSide.East, AttackSide.South, AttackSide.West };
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            _activeSides.Clear();
            for (int i = 0; i < count; i++) _activeSides.Add(pool[i]);

            Debug.Log($"[AttackDirectionSelector] Wave {waveNumber}: {count} active side(s) — {string.Join(", ", _activeSides)}");
        }

        /// <summary>Return a random world-space spawn position on the given side.</summary>
        public Vector3 GetRandomSpawnPosition(AttackSide side)
        {
            if (!_positions.TryGetValue(side, out Vector3[] pts) || pts.Length == 0)
                return map != null ? map.Origin : Vector3.zero;
            return pts[Random.Range(0, pts.Length)];
        }

        // ── Internal ─────────────────────────────────────────────────────────

        void BuildPositions()
        {
            Vector2 wMin = map.WorldMin;
            Vector2 wMax = map.WorldMax;
            float y = map.Origin.y;

            _positions[AttackSide.North] = BuildEdge(
                new Vector3(wMin.x + edgeInset, y, wMax.y),
                new Vector3(wMax.x - edgeInset, y, wMax.y));

            _positions[AttackSide.South] = BuildEdge(
                new Vector3(wMin.x + edgeInset, y, wMin.y),
                new Vector3(wMax.x - edgeInset, y, wMin.y));

            _positions[AttackSide.East] = BuildEdge(
                new Vector3(wMax.x, y, wMin.y + edgeInset),
                new Vector3(wMax.x, y, wMax.y - edgeInset));

            _positions[AttackSide.West] = BuildEdge(
                new Vector3(wMin.x, y, wMin.y + edgeInset),
                new Vector3(wMin.x, y, wMax.y - edgeInset));
        }

        Vector3[] BuildEdge(Vector3 from, Vector3 to)
        {
            var pts = new Vector3[spawnCellsPerSide];
            for (int i = 0; i < spawnCellsPerSide; i++)
            {
                float t = spawnCellsPerSide == 1 ? 0.5f : (float)i / (spawnCellsPerSide - 1);
                pts[i] = Vector3.Lerp(from, to, t);
            }
            return pts;
        }

        // ── Gizmos ──────────────────────────────────────────────────────────

        static readonly Color[] _sideColors =
        {
            new Color(0.25f, 0.70f, 1.00f, 1f), // North  — blue
            new Color(1.00f, 0.75f, 0.10f, 1f), // East   — amber
            new Color(0.30f, 0.90f, 0.40f, 1f), // South  — green
            new Color(0.90f, 0.35f, 0.90f, 1f), // West   — purple
        };

        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            if (map == null) return;

            // Ensure positions are computed in edit mode too.
            if (_positions.Count == 0) BuildPositions();

            var allSides = new[] { AttackSide.North, AttackSide.East, AttackSide.South, AttackSide.West };
            foreach (AttackSide side in allSides)
            {
                bool active = _activeSides.Contains(side);
                Color baseColor = _sideColors[(int)side];
                Color sphereColor = active ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.25f);
                Color lineColor = active ? baseColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.12f);

                if (!_positions.TryGetValue(side, out Vector3[] pts)) continue;

                Gizmos.color = sphereColor;
                foreach (Vector3 p in pts)
                    Gizmos.DrawWireSphere(p, active ? 1.8f : 1.0f);

                // Connect adjacent positions with a line to show the zone.
                Gizmos.color = lineColor;
                for (int i = 0; i < pts.Length - 1; i++)
                    Gizmos.DrawLine(pts[i], pts[i + 1]);

                // Draw a label-like pillar above the first active position so the side is obvious.
                if (active && pts.Length > 0)
                {
                    Vector3 mid = pts[pts.Length / 2];
                    Gizmos.color = baseColor;
                    Gizmos.DrawLine(mid, mid + Vector3.up * 6f);
                }
            }
        }
    }
}
