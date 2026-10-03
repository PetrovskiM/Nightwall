using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Draws the build grid as thin lines in the Game view — Unity gizmos (the ones
    /// <see cref="GridSystem"/> draws) only appear in the Scene view. Builds a single
    /// line-topology mesh from the <see cref="MapConfig"/> once, so it costs one draw call.
    /// Hidden by default; <see cref="BuildingPlacer"/> shows it while in build mode.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GridOverlay : MonoBehaviour
    {
        [SerializeField] MapConfig config;
        [Tooltip("Height above the ground plane to avoid z-fighting.")]
        [SerializeField] float yOffset = 0.02f;

        MeshRenderer _renderer;

        void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();

            if (config == null) config = Resources.Load<MapConfig>("MapConfig");
            if (config == null)
            {
                Debug.LogError("[GridOverlay] MapConfig is not assigned and none found in Resources.", this);
                enabled = false;
                return;
            }

            GetComponent<MeshFilter>().mesh = BuildMesh();
            SetVisible(false);
        }

        /// <summary>Toggles the overlay's visibility (called by the building system).</summary>
        public void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
        }

        Mesh BuildMesh()
        {
            int w = config.Width;
            int h = config.Height;
            float cs = config.CellSize;
            Vector2 min = config.WorldMin;
            float y = config.Origin.y + yOffset;

            int vCount = (w + 1) * 2 + (h + 1) * 2;
            var verts = new Vector3[vCount];
            var indices = new int[vCount];
            int vi = 0;

            for (int x = 0; x <= w; x++)
            {
                float wx = min.x + x * cs;
                verts[vi]     = new Vector3(wx, y, min.y);
                verts[vi + 1] = new Vector3(wx, y, min.y + h * cs);
                vi += 2;
            }
            for (int z = 0; z <= h; z++)
            {
                float wz = min.y + z * cs;
                verts[vi]     = new Vector3(min.x, y, wz);
                verts[vi + 1] = new Vector3(min.x + w * cs, y, wz);
                vi += 2;
            }
            for (int i = 0; i < vCount; i++) indices[i] = i;

            var mesh = new Mesh { name = "GridOverlay" };
            mesh.vertices = verts;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
