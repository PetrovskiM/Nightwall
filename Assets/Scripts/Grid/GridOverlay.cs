using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Draws the build grid as thin flat quads in the Game view — Unity gizmos (the ones
    /// <see cref="GridSystem"/> draws) only appear in the Scene view. Builds a single mesh from the
    /// <see cref="MapConfig"/> once, so it costs one draw call. Quads (not line topology) give a
    /// controllable, reliably-rendered width.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GridOverlay : MonoBehaviour
    {
        [SerializeField] MapConfig config;
        [Tooltip("Width of each grid line in world units.")]
        [SerializeField] float lineWidth = 0.04f;
        [Tooltip("Height above the ground plane to avoid z-fighting.")]
        [SerializeField] float yOffset = 0.02f;

        MeshRenderer _renderer;
        BuildingPlacer _placer;
        bool _visible;

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
            // Start hidden. Set the renderer directly — SetVisible(false) here would no-op because
            // _visible already defaults to false, leaving the renderer on.
            if (_renderer != null) _renderer.enabled = false;
        }

        // Self-wire rather than hold a serialized reference the binary scene can drop.
        void Start() => _placer = FindFirstObjectByType<BuildingPlacer>();

        void Update() => SetVisible(_placer != null && _placer.IsActive);

        /// <summary>Shows the overlay only while something is building.</summary>
        public void SetVisible(bool visible)
        {
            if (visible == _visible) return;
            _visible = visible;
            if (_renderer != null) _renderer.enabled = visible;
        }

        Mesh BuildMesh()
        {
            int w = config.Width;
            int h = config.Height;
            float cs = config.CellSize;
            Vector2 min = config.WorldMin;
            float y = config.Origin.y + yOffset;
            float hw = lineWidth * 0.5f;

            float xMin = min.x, xMax = min.x + w * cs;
            float zMin = min.y, zMax = min.y + h * cs;

            int lines = (w + 1) + (h + 1);
            var verts = new Vector3[lines * 4];
            var tris = new int[lines * 6];
            int vi = 0, ti = 0;

            for (int x = 0; x <= w; x++)
            {
                float wx = min.x + x * cs;
                AddQuad(verts, tris, ref vi, ref ti,
                    new Vector3(wx - hw, y, zMin), new Vector3(wx + hw, y, zMin),
                    new Vector3(wx + hw, y, zMax), new Vector3(wx - hw, y, zMax));
            }
            for (int z = 0; z <= h; z++)
            {
                float wz = min.y + z * cs;
                AddQuad(verts, tris, ref vi, ref ti,
                    new Vector3(xMin, y, wz - hw), new Vector3(xMax, y, wz - hw),
                    new Vector3(xMax, y, wz + hw), new Vector3(xMin, y, wz + hw));
            }

            var mesh = new Mesh { name = "GridOverlay" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddQuad(Vector3[] v, int[] t, ref int vi, ref int ti,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = vi;
            v[vi++] = a; v[vi++] = b; v[vi++] = c; v[vi++] = d;
            t[ti++] = s;     t[ti++] = s + 2; t[ti++] = s + 1;
            t[ti++] = s;     t[ti++] = s + 3; t[ti++] = s + 2;
        }
    }
}
