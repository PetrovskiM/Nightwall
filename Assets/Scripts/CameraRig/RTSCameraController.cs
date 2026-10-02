using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Pans a Cinemachine camera across the ground plane (WASD / arrows / screen-edge) and
    /// zooms by changing the orthographic size. Attach to the CinemachineCamera object.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] float panSpeed = 22f;
        [SerializeField] bool edgePan = true;
        [SerializeField] int edgeBorder = 12;

        [Header("Zoom")]
        [SerializeField] float zoomSpeed = 5f;
        [SerializeField] float minZoom = 6f;
        [SerializeField] float maxZoom = 30f;

        [Header("Bounds (world XZ)")]
        [SerializeField] Vector2 boundsMin = new Vector2(-40, -40);
        [SerializeField] Vector2 boundsMax = new Vector2(40, 40);

        CinemachineCamera _vcam;
        Vector3 _right;
        Vector3 _forward;

        void Awake()
        {
            _vcam = GetComponent<CinemachineCamera>();
            // Build a ground-plane movement basis from the camera's yaw so panning feels screen-relative.
            Quaternion yawOnly = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            _right = yawOnly * Vector3.right;
            _forward = yawOnly * Vector3.forward;
        }

        void Update()
        {
            Vector2 move = ReadMove();
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

        Vector2 ReadMove()
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
            p.x = Mathf.Clamp(p.x, boundsMin.x, boundsMax.x);
            p.z = Mathf.Clamp(p.z, boundsMin.y, boundsMax.y);
            return p;
        }
    }
}
