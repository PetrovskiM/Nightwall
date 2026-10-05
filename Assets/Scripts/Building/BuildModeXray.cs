using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// While build mode is active, renders this structure (HQ, wall, gate, trap…) as a translucent
    /// see-through silhouette so the player can read the grid cells behind it and plan placement in
    /// the isometric view. Restores the solid materials the moment build mode ends.
    ///
    /// Purely visual — it swaps its own renderers' materials and owns no gameplay state. It listens to
    /// <see cref="BuildingPlacer.BuildModeChanged"/> rather than polling, and applies the current state
    /// on <see cref="Start"/> so structures built mid-build-mode appear see-through immediately.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildModeXray : MonoBehaviour
    {
        [Tooltip("Opacity of the structure while build mode is active (0 = invisible, 1 = solid).")]
        [Range(0.05f, 1f)]
        [SerializeField] float xrayAlpha = 0.2f;

        Renderer[] _renderers;
        Material[][] _solidMaterials;   // original shared materials per renderer, to restore.
        Material[] _xrayMaterials;      // one see-through instance per renderer, tinted from its colour.
        BuildingPlacer _placer;
        bool _xrayActive;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _solidMaterials = new Material[_renderers.Length][];
            _xrayMaterials = new Material[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                _solidMaterials[i] = _renderers[i].sharedMaterials;
                _xrayMaterials[i] = MakeXrayMaterial(_renderers[i].sharedMaterial);
            }
        }

        // Resolve the placer late (the binary prototype scene can drop serialized references) and
        // sync to whatever build state is current, so a just-placed structure matches its neighbours.
        void Start()
        {
            _placer = FindFirstObjectByType<BuildingPlacer>();
            if (_placer == null) return;
            _placer.BuildModeChanged += OnBuildModeChanged;
            Apply(_placer.IsActive);
        }

        void OnDestroy()
        {
            if (_placer != null) _placer.BuildModeChanged -= OnBuildModeChanged;
            if (_xrayMaterials != null)
                foreach (var m in _xrayMaterials)
                    if (m != null) Destroy(m);
        }

        void OnBuildModeChanged(bool buildActive) => Apply(buildActive);

        void Apply(bool xray)
        {
            if (_xrayActive == xray || _renderers == null) return;
            _xrayActive = xray;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (xray) _renderers[i].sharedMaterial = _xrayMaterials[i];
                else _renderers[i].sharedMaterials = _solidMaterials[i];
            }
        }

        /// <summary>Builds a transparent URP material tinted to the structure's own base colour, with
        /// depth-write off so geometry behind it shows through.</summary>
        Material MakeXrayMaterial(Material source)
        {
            Color tint = Color.white;
            if (source != null)
            {
                if (source.HasProperty("_BaseColor")) tint = source.GetColor("_BaseColor");
                else if (source.HasProperty("_Color")) tint = source.GetColor("_Color");
            }
            tint.a = xrayAlpha;

            // Unlit transparent is reliable at runtime (URP Lit does not fully switch to transparent
            // when reconfigured via script, which left the structures looking solid).
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "BuildModeXray" };

            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }
    }
}
