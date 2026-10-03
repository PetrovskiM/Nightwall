using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Runtime owner of the build grid. Reads its dimensions from a <see cref="MapConfig"/> and
    /// provides the conversions every placement system needs — world &lt;-&gt; cell, snapping to cell
    /// centres, bounds tests — plus cell occupancy so two structures can't share a footprint.
    /// It holds no gameplay logic of its own; it is the spatial source of truth.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        public static GridSystem Instance { get; private set; }

        [SerializeField] MapConfig config;

        readonly HashSet<Vector2Int> _occupied = new();

        public MapConfig Config => config;
        public float CellSize => config != null ? config.CellSize : 1f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Conversions ----------

        /// <summary>World position -> the cell coordinate that contains it.</summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector2 min = config.WorldMin;
            float cs = config.CellSize;
            return new Vector2Int(
                Mathf.FloorToInt((world.x - min.x) / cs),
                Mathf.FloorToInt((world.z - min.y) / cs));
        }

        /// <summary>Cell coordinate -> the world position of that cell's centre (keeps map Y).</summary>
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

        // ---------- Occupancy ----------

        public bool IsOccupied(Vector2Int cell) => _occupied.Contains(cell);

        /// <summary>True when every cell of a footprint anchored at <paramref name="anchor"/> is free and in-bounds.</summary>
        public bool CanPlace(Vector2Int anchor, Vector2Int footprint)
        {
            foreach (Vector2Int c in Cells(anchor, footprint))
                if (!InBounds(c) || _occupied.Contains(c)) return false;
            return true;
        }

        public void Occupy(Vector2Int anchor, Vector2Int footprint)
        {
            foreach (Vector2Int c in Cells(anchor, footprint)) _occupied.Add(c);
        }

        public void Free(Vector2Int anchor, Vector2Int footprint)
        {
            foreach (Vector2Int c in Cells(anchor, footprint)) _occupied.Remove(c);
        }

        static IEnumerable<Vector2Int> Cells(Vector2Int anchor, Vector2Int footprint)
        {
            int w = Mathf.Max(1, footprint.x);
            int h = Mathf.Max(1, footprint.y);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    yield return new Vector2Int(anchor.x + x, anchor.y + y);
        }
    }
}
