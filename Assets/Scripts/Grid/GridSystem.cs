using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    public enum CellState
    {
        Walkable,
        Blocked,
        PlayerBase,
        Building,
        Reserved,
    }

    /// <summary>
    /// Runtime owner of the build grid. Reads its dimensions from a <see cref="MapConfig"/> and
    /// provides world &lt;-&gt; cell conversions, occupancy tracking, and cell state queries.
    /// It is the sole spatial source of truth; no gameplay logic lives here.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class GridSystem : MonoBehaviour
    {
        public static GridSystem Instance { get; private set; }

        [SerializeField] MapConfig config;

        [Header("Debug")]
        [SerializeField] bool showGrid = true;
        [SerializeField] Color walkableColor = new Color(1f, 1f, 1f, 0.05f);
        [SerializeField] Color blockedColor = new Color(1f, 0f, 0f, 0.25f);
        [SerializeField] Color buildingColor = new Color(0f, 0.5f, 1f, 0.35f);
        [SerializeField] Color playerBaseColor = new Color(0f, 1f, 0.4f, 0.4f);
        [SerializeField] Color reservedColor = new Color(1f, 0.85f, 0f, 0.3f);

        readonly Dictionary<Vector2Int, CellState> _states = new();

        public MapConfig Config => config;
        public float CellSize => config != null ? config.CellSize : 1f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // Fallback for when the serialized scene reference is lost (the prototype scene saves
            // as binary, which can drop object references). The asset lives in a Resources folder.
            if (config == null)
                config = Resources.Load<MapConfig>("MapConfig");

            if (config == null)
                Debug.LogError("[GridSystem] MapConfig is not assigned and none found in Resources.", this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Conversions ──────────────────────────────────────────────────────

        /// <summary>World position → cell coordinate containing that point.</summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector2 min = config.WorldMin;
            float cs = config.CellSize;
            return new Vector2Int(
                Mathf.FloorToInt((world.x - min.x) / cs),
                Mathf.FloorToInt((world.z - min.y) / cs));
        }

        /// <summary>Cell coordinate → world-space centre of that cell (map Y preserved).</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            Vector2 min = config.WorldMin;
            float cs = config.CellSize;
            return new Vector3(
                min.x + (cell.x + 0.5f) * cs,
                config.Origin.y,
                min.y + (cell.y + 0.5f) * cs);
        }

        /// <summary>Snap an arbitrary world point to the nearest cell centre inside the map.</summary>
        public Vector3 Snap(Vector3 world) => CellToWorld(WorldToCell(world));

        public bool InBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < config.Width && cell.y < config.Height;

        // ── Cell state ───────────────────────────────────────────────────────

        public CellState GetState(Vector2Int cell) =>
            _states.TryGetValue(cell, out CellState s) ? s : CellState.Walkable;

        public void SetState(Vector2Int cell, CellState state)
        {
            if (!InBounds(cell)) return;
            if (state == CellState.Walkable)
                _states.Remove(cell);
            else
                _states[cell] = state;
        }

        /// <summary>True when a cell exists and an enemy can traverse it.</summary>
        public bool IsWalkable(Vector2Int cell)
        {
            if (!InBounds(cell)) return false;
            CellState s = GetState(cell);
            return s == CellState.Walkable || s == CellState.PlayerBase;
        }

        /// <summary>True when the cell is taken by any non-walkable state.</summary>
        public bool IsOccupied(Vector2Int cell)
        {
            if (!InBounds(cell)) return false;
            CellState s = GetState(cell);
            return s == CellState.Building || s == CellState.Blocked || s == CellState.Reserved;
        }

        // ── Footprint helpers ────────────────────────────────────────────────

        /// <summary>True when every cell of a footprint anchored at <paramref name="anchor"/> is walkable/free.</summary>
        public bool CanPlace(Vector2Int anchor, Vector2Int footprint)
        {
            foreach (Vector2Int c in Cells(anchor, footprint))
            {
                if (!InBounds(c)) return false;
                CellState s = GetState(c);
                if (s != CellState.Walkable) return false;
            }
            return true;
        }

        public void Occupy(Vector2Int anchor, Vector2Int footprint, CellState state = CellState.Building)
        {
            foreach (Vector2Int c in Cells(anchor, footprint))
                SetState(c, state);
        }

        public void Free(Vector2Int anchor, Vector2Int footprint)
        {
            foreach (Vector2Int c in Cells(anchor, footprint))
                SetState(c, CellState.Walkable);
        }

        static IEnumerable<Vector2Int> Cells(Vector2Int anchor, Vector2Int footprint)
        {
            int w = Mathf.Max(1, footprint.x);
            int h = Mathf.Max(1, footprint.y);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    yield return new Vector2Int(anchor.x + x, anchor.y + y);
        }

        // ── Debug visualisation ──────────────────────────────────────────────

        void OnDrawGizmos()
        {
            if (!showGrid || config == null) return;

            float cs = config.CellSize;
            float halfCs = cs * 0.5f;
            float y = config.Origin.y + 0.01f;

            for (int cx = 0; cx < config.Width; cx++)
            {
                for (int cy = 0; cy < config.Height; cy++)
                {
                    var cell = new Vector2Int(cx, cy);
                    CellState state = GetState(cell);

                    Gizmos.color = state switch
                    {
                        CellState.Blocked    => blockedColor,
                        CellState.Building   => buildingColor,
                        CellState.PlayerBase => playerBaseColor,
                        CellState.Reserved   => reservedColor,
                        _                    => walkableColor,
                    };

                    Vector3 centre = CellToWorld(cell);
                    centre.y = y;
                    Gizmos.DrawCube(centre, new Vector3(cs - 0.04f, 0.001f, cs - 0.04f));

                    // outline
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.15f);
                    DrawCellWire(centre, halfCs);
                }
            }
        }

        static void DrawCellWire(Vector3 centre, float half)
        {
            Vector3 a = centre + new Vector3(-half, 0f, -half);
            Vector3 b = centre + new Vector3( half, 0f, -half);
            Vector3 c = centre + new Vector3( half, 0f,  half);
            Vector3 d = centre + new Vector3(-half, 0f,  half);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
