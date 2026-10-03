using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Authoritative, designer-facing description of the playable map: a flat grid of
    /// <see cref="width"/> x <see cref="height"/> cells, each <see cref="cellSize"/> world units
    /// across, centred on <see cref="origin"/>. Every system that needs the map's size — the grid,
    /// the terrain builder, the camera bounds, the building placer — reads it from here instead of
    /// hardcoding numbers, so the whole prototype rescales by editing one asset.
    /// </summary>
    [CreateAssetMenu(fileName = "MapConfig", menuName = "Nightwall/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [Header("Grid (in cells)")]
        [Min(1)] [SerializeField] int width = 60;
        [Min(1)] [SerializeField] int height = 60;

        [Header("Cell")]
        [Min(0.1f)] [SerializeField] float cellSize = 1f;

        [Header("World placement")]
        [Tooltip("World-space centre of the map on the ground plane.")]
        [SerializeField] Vector3 origin = Vector3.zero;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;

        /// <summary>Total playable size in world units (X = width, Z = height).</summary>
        public Vector2 WorldSize => new Vector2(width * cellSize, height * cellSize);

        /// <summary>Min world-space corner on the XZ plane (south-west).</summary>
        public Vector2 WorldMin => new Vector2(
            origin.x - width * cellSize * 0.5f,
            origin.z - height * cellSize * 0.5f);

        /// <summary>Max world-space corner on the XZ plane (north-east).</summary>
        public Vector2 WorldMax => new Vector2(
            origin.x + width * cellSize * 0.5f,
            origin.z + height * cellSize * 0.5f);
    }
}
