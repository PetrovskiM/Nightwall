using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Pans an orthographic isometric Cinemachine camera across the ground plane
    /// (WASD / arrows / screen-edge) and zooms by changing the orthographic size. Pan limits are
    /// taken from the <see cref="MapConfig"/> (plus a margin) so the view always matches the map
    /// without hand-tuned bounds. Attach to the CinemachineCamera object.
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
        [SerializeField] float minZoom = 6f;
        [SerializeField] float maxZoom = 40f;

        CinemachineCamera _vcam;
        Vector3 _right;
        Vector3 _forward;
        Vector2 _boundsMin;
        Vector2 _boundsMax;

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
        }

        // Touch state for pan and pinch.
        Vector2 _prevSingleTouch;
        float _prevPinchDist;
        bool _isPinching;

        void Update()
        {
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                HandleTouch();
            }
            else
            {
                HandleKeyboardMouse();
            }
        }

        void HandleTouch()
        {
            var touches = Touchscreen.current.touches;
            int activeCount = 0;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].isInProgress) activeCount++;

            if (activeCount == 1)
            {
                _isPinching = false;
                var t0 = touches[0];
                Vector2 pos = t0.position.ReadValue();
                if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    _prevSingleTouch = pos;
                }
                else if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    Vector2 delta = pos - _prevSingleTouch;
                    // Invert: drag finger right moves camera right (world moves left under finger).
                    PanByScreenDelta(-delta);
                    _prevSingleTouch = pos;
                }
            }
            else if (activeCount >= 2)
            {
                Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
                int found = 0;
                for (int i = 0; i < touches.Count && found < 2; i++)
                {
                    if (touches[i].isInProgress) { if (found == 0) p0 = touches[i].position.ReadValue(); else p1 = touches[i].position.ReadValue(); found++; }
                }
                float dist = Vector2.Distance(p0, p1);
                if (!_isPinching) { _prevPinchDist = dist; _isPinching = true; }
                float pinchDelta = _prevPinchDist - dist;
                if (Mathf.Abs(pinchDelta) > 0.5f)
                {
                    LensSettings lens = _vcam.Lens;
                    lens.OrthographicSize = Mathf.Clamp(
                        lens.OrthographicSize + pinchDelta * zoomSpeed * 0.02f, minZoom, maxZoom);
                    _vcam.Lens = lens;
                }
                _prevPinchDist = dist;
            }
            else
            {
                _isPinching = false;
            }
        }

        void HandleKeyboardMouse()
        {
            _isPinching = false;
            Vector2 move = ReadKeyboardEdge();
            if (move.sqrMagnitude > 0.0001f)
            {
                Vector3 delta = (_right * move.x + _forward * move.y).normalized * panSpeed * Time.deltaTime;
                transform.position = Clamp(transform.position + delta);
            }

            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                LensSettings lens = _vcam.Lens;
                lens.OrthographicSize = Mathf.Clamp(
                    lens.OrthographicSize - scroll * zoomSpeed * 0.01f, minZoom, maxZoom);
                _vcam.Lens = lens;
            }
        }

        /// <summary>Converts a screen-space pixel delta into a world-space camera pan.</summary>
        void PanByScreenDelta(Vector2 screenDelta)
        {
            // Scale delta so one pixel matches one pixel of world movement at current zoom.
            float unitsPerPixel = (_vcam.Lens.OrthographicSize * 2f) / Screen.height;
            Vector3 worldDelta = (_right * screenDelta.x + _forward * screenDelta.y) * unitsPerPixel;
            transform.position = Clamp(transform.position + worldDelta);
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
