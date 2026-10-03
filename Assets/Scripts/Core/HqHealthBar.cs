using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A self-contained placeholder health bar floating above the base. It reads <b>only</b> from
    /// the sibling <see cref="Health"/> (via its <see cref="Health.Damaged"/>/<see cref="Health.Died"/>
    /// events) and billboards toward the camera — it never touches the enemy AI or building systems,
    /// keeping the base display one-directional (gameplay state → UI). The bar and its unlit
    /// materials are generated at runtime so no canvas/prefab wiring is needed; this is a prototype
    /// readout, to be replaced by real mobile UI later.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HqHealthBar : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Metres above the object's pivot to float the bar.")]
        [SerializeField] float heightOffset = 3.5f;
        [SerializeField] Vector2 size = new Vector2(5f, 0.5f);

        [Header("Colours")]
        [SerializeField] Color backgroundColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        [SerializeField] Color fillColor = new Color(0.25f, 0.85f, 1f, 1f);
        [Tooltip("Fill colour when the base is nearly destroyed (blended by remaining health).")]
        [SerializeField] Color lowColor = new Color(1f, 0.3f, 0.25f, 1f);

        Health _health;
        Transform _root;
        Transform _fill;
        Material _fillMat;
        Material _bgMat;
        Camera _cam;

        void Awake() => _health = GetComponent<Health>();

        void Start()
        {
            _cam = Camera.main;
            BuildBar();
            Refresh();
        }

        void OnEnable()
        {
            // May run before Start (first enable) — guard until the bar exists, then Start refreshes.
            if (_health == null) _health = GetComponent<Health>();
            _health.Damaged += OnHealthChanged;
            _health.Died += OnHealthChanged;
        }

        void OnDisable()
        {
            _health.Damaged -= OnHealthChanged;
            _health.Died -= OnHealthChanged;
        }

        void OnDestroy()
        {
            if (_fillMat != null) Destroy(_fillMat);
            if (_bgMat != null) Destroy(_bgMat);
        }

        void LateUpdate()
        {
            if (_root == null) return;
            if (_cam == null) _cam = Camera.main;

            _root.position = transform.position + Vector3.up * heightOffset;
            // Face the camera but stay upright so the bar reads cleanly in the iso view.
            if (_cam != null) _root.rotation = _cam.transform.rotation;
        }

        void OnHealthChanged(Health _) => Refresh();

        /// <summary>Resize and recolour the fill from the current normalized health (left-anchored).</summary>
        void Refresh()
        {
            if (_fill == null) return;

            float t = Mathf.Clamp01(_health.Normalized);
            _fill.localScale = new Vector3(size.x * t, size.y, 1f);
            // Quads pivot at their centre; shift left so the bar drains from the right.
            _fill.localPosition = new Vector3(-size.x * 0.5f * (1f - t), 0f, -0.01f);

            if (_fillMat != null) SetColor(_fillMat, Color.Lerp(lowColor, fillColor, t));
        }

        void BuildBar()
        {
            _bgMat = MakeUnlitMaterial(backgroundColor);
            _fillMat = MakeUnlitMaterial(fillColor);

            var rootGo = new GameObject("HealthBar");
            _root = rootGo.transform;

            var bg = CreateQuad("Background", _root, _bgMat);
            bg.localScale = new Vector3(size.x, size.y, 1f);
            bg.localPosition = Vector3.zero;

            var fillGo = CreateQuad("Fill", _root, _fillMat);
            _fill = fillGo;
        }

        Transform CreateQuad(string name, Transform parent, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);

            // A health bar is pure display: strip the collider so it never participates in raycasts
            // (building removal, enemy breach queries) or physics.
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            return quad.transform;
        }

        static Material MakeUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);

            // Transparent so the background's alpha reads as a subtle frame.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            SetColor(mat, color);
            return mat;
        }

        static void SetColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }
}
