using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Pans an orthographic isometric Cinemachine camera across the ground plane and zooms by
    /// changing the orthographic size. All gestures come from <see cref="InputService"/>: two-finger
    /// drag pans, pinch (or scroll wheel) zooms, and an optional two-finger twist can rotate the rig.
    /// Keyboard (WASD / arrows) and screen-edge panning stay available for Editor development. Pan
    /// limits are taken from the <see cref="MapConfig"/> (plus a margin) so the view always matches
    /// the map without hand-tuned bounds. Attach to the CinemachineCamera object.
    ///
    /// Input feeds a <i>target</i> position and orthographic size; the rig eases toward them with
    /// <see cref="Mathf.SmoothDamp"/> each frame. Smoothing times are kept short so the camera stays
    /// responsive for spotting breaches and incoming hordes rather than feeling cinematic.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class IsoCameraController : MonoBehaviour
    {
        [Header("Map bounds source")]
        [SerializeField] MapConfig map;
        [Tooltip("Extra world units the camera may pan beyond the map edge.")]
        [SerializeField] float boundsMargin = 6f;

        [Header("Pan")]
        [SerializeField] float panSpeed = 22f;
        [SerializeField] bool edgePan = true;
        [SerializeField] int edgeBorder = 12;

        [Header("Zoom")]
        [SerializeField] float zoomSpeed = 5f;
        [Tooltip("Closest view — tight enough to read a single gate/trap and nearby wall joints.")]
        [SerializeField] float minZoom = 6f;
        [Tooltip("Widest view — far enough to see the whole maze and approaching hordes at once.")]
        [SerializeField] float maxZoom = 40f;

        [Header("Smoothing (responsive, not cinematic)")]
        [Tooltip("Seconds for pan to settle. Small = snappy. Zero disables pan smoothing.")]
        [SerializeField] float panSmoothTime = 0.08f;
        [Tooltip("Seconds for zoom to settle. Small = snappy. Zero disables zoom smoothing.")]
        [SerializeField] float zoomSmoothTime = 0.12f;

        [Header("Rotation (optional)")]
        [Tooltip("Allow two-finger twist to rotate the iso rig. Off by default: a fixed angle keeps " +
                 "the isometric view readable and the pan/edge controls predictable.")]
        [SerializeField] bool enableTwistRotation = false;
        [SerializeField] float twistSpeed = 1f;

        CinemachineCamera _vcam;
        Vector3 _right;
        Vector3 _forward;
        Vector2 _boundsMin;
        Vector2 _boundsMax;

        // Input writes these targets; the rig eases toward them in LateUpdate.
        Vector3 _targetPosition;
        float _targetZoom;
        Vector3 _panVelocity;   // SmoothDamp state (position)
        float _zoomVelocity;    // SmoothDamp state (orthographic size)

        void Awake()
        {
            _vcam = GetComponent<CinemachineCamera>();

            // Build a ground-plane movement basis from the camera's yaw so panning feels screen-relative.
            Quaternion yawOnly = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _right = yawOnly * Vector3.right;
            _forward = yawOnly * Vector3.forward;

            if (map != null)
            {
                _boundsMin = map.WorldMin - Vector2.one * boundsMargin;
                _boundsMax = map.WorldMax + Vector2.one * boundsMargin;
            }
            else
            {
                _boundsMin = new Vector2(-40f, -40f);
                _boundsMax = new Vector2(40f, 40f);
            }

            _targetPosition = Clamp(transform.position);
            _targetZoom = Mathf.Clamp(_vcam.Lens.OrthographicSize, minZoom, maxZoom);
        }

        void Update()
        {
            InputService input = InputService.Instance;
            if (input == null) return;

            // Gestures (touch two-finger drag / pinch / twist, and scroll wheel) from the abstraction.
            if (input.PanDelta != Vector2.zero)
                // Drag finger right → world moves left under the finger, so invert the pan.
                PanByScreenDelta(-input.PanDelta);

            if (Mathf.Abs(input.PinchDelta) > 0.01f)
                // Pinch apart (positive) zooms in → reduce the target orthographic size.
                _targetZoom = Mathf.Clamp(
                    _targetZoom - input.PinchDelta * zoomSpeed * 0.02f, minZoom, maxZoom);

            if (enableTwistRotation && Mathf.Abs(input.TwistDelta) > 0.01f)
                Rotate(input.TwistDelta * twistSpeed);

            // Keyboard / screen-edge panning stays for Editor development.
            Vector2 move = ReadKeyboardEdge();
            if (move.sqrMagnitude > 0.0001f)
            {
                // Keyboard/edge speed tracks zoom so a pixel of travel feels the same at any scale.
                float zoomScale = _targetZoom / maxZoom;
                Vector3 delta = (_right * move.x + _forward * move.y).normalized
                                * panSpeed * zoomScale * Time.deltaTime;
                _targetPosition = Clamp(_targetPosition + delta);
            }
        }

        /// <summary>Eases the rig toward the input-driven target position and zoom. Runs after all
        /// input in <c>Update</c> so a frame's gestures are folded into one smoothed step.</summary>
        void LateUpdate()
        {
            transform.position = panSmoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, _targetPosition, ref _panVelocity, panSmoothTime)
                : _targetPosition;

            LensSettings lens = _vcam.Lens;
            lens.OrthographicSize = zoomSmoothTime > 0f
                ? Mathf.SmoothDamp(lens.OrthographicSize, _targetZoom, ref _zoomVelocity, zoomSmoothTime)
                : _targetZoom;
            _vcam.Lens = lens;
        }

        /// <summary>Converts a screen-space pixel delta into a world-space pan of the camera target.</summary>
        void PanByScreenDelta(Vector2 screenDelta)
        {
            // Scale delta so one pixel matches one pixel of world movement at current zoom.
            float unitsPerPixel = (_targetZoom * 2f) / Screen.height;
            Vector3 worldDelta = (_right * screenDelta.x + _forward * screenDelta.y) * unitsPerPixel;
            _targetPosition = Clamp(_targetPosition + worldDelta);
        }

        /// <summary>Rotates the rig about the vertical axis and rebuilds the screen-relative pan basis
        /// so WASD/edge panning still follows the new orientation.</summary>
        void Rotate(float degrees)
        {
            transform.RotateAround(transform.position, Vector3.up, degrees);
            _targetPosition = transform.position; // rotate pivots about the rig, so keep the target in sync
            Quaternion yawOnly = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _right = yawOnly * Vector3.right;
            _forward = yawOnly * Vector3.forward;
        }

        Vector2 ReadKeyboardEdge()
        {
            Vector2 v = Vector2.zero;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
            }

            if (edgePan && v == Vector2.zero && Mouse.current != null)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                if (m.x <= edgeBorder) v.x -= 1;
                else if (m.x >= Screen.width - edgeBorder) v.x += 1;
                if (m.y <= edgeBorder) v.y -= 1;
                else if (m.y >= Screen.height - edgeBorder) v.y += 1;
            }
            return v;
        }

        Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, _boundsMin.x, _boundsMax.x);
            p.z = Mathf.Clamp(p.z, _boundsMin.y, _boundsMax.y);
            return p;
        }
    }
}
