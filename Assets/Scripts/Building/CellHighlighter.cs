using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A single flat quad that highlights the grid cell currently targeted for construction, tinted
    /// green (valid) or red (invalid). It gives clear, touch-friendly placement feedback distinct
    /// from the translucent build ghost — the player can see exactly which cell a tap will affect.
    /// Driven by <see cref="BuildingPlacer"/> via <see cref="Show"/>/<see cref="Hide"/>; owns no
    /// gameplay state of its own.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class CellHighlighter : MonoBehaviour
    {
        [Tooltip("Height above the ground plane to avoid z-fighting with the grid overlay.")]
        [SerializeField] float yOffset = 0.03f;
        [Tooltip("Inset so the highlight sits just inside the cell borders.")]
        [SerializeField] float inset = 0.06f;
        [SerializeField] Color validTint = new Color(0.2f, 0.95f, 0.3f, 0.35f);
        [SerializeField] Color invalidTint = new Color(1f, 0.25f, 0.25f, 0.4f);

        MeshRenderer _renderer;
        Material _material;
        bool _built;

        void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            BuildQuad();
            Hide();
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        /// <summary>Places the highlight over <paramref name="worldCenter"/> for a cell spanning
        /// <paramref name="footprint"/> cells, tinted by validity.</summary>
        public void Show(Vector3 worldCenter, Vector2Int footprint, float cellSize, bool valid)
        {
            if (!_built) BuildQuad();
            float sx = Mathf.Max(1, footprint.x) * cellSize;
            float sz = Mathf.Max(1, footprint.y) * cellSize;
            transform.position = new Vector3(worldCenter.x, worldCenter.y + yOffset, worldCenter.z);
            transform.localScale = new Vector3(sx - inset * 2f, 1f, sz - inset * 2f);
            if (_material != null)
                SetColor(valid ? validTint : invalidTint);
            if (!_renderer.enabled) _renderer.enabled = true;
        }

        /// <summary>Hides the highlight (not building, or no valid target cell).</summary>
        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        void SetColor(Color c)
        {
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", c);
            if (_material.HasProperty("_Color")) _material.SetColor("_Color", c);
        }

        void BuildQuad()
        {
            // Unit quad on the XZ plane centred at the origin; scaled per-cell in Show.
            var mesh = new Mesh { name = "CellHighlight" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3( 0.5f, 0f,  0.5f), new Vector3( 0.5f, 0f, -0.5f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().mesh = mesh;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _material = new Material(shader);
                EnableTransparency(_material);
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
            }
            _built = true;
        }

        static void EnableTransparency(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // URP transparent
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
