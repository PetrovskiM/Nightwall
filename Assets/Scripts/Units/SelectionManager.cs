using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Left-click / drag-box to select units, right-click to issue a NavMesh move order.
    /// Yields control while the build placer is active so clicks don't conflict.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        [SerializeField] LayerMask selectableMask = ~0;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] BuildingPlacer buildingPlacer;
        [SerializeField] float clickThreshold = 8f;

        readonly List<Selectable> _selected = new();
        Camera _cam;
        Vector2 _dragStart;
        bool _dragging;

        void Awake() => _cam = Camera.main;

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || Mouse.current == null) return;
            if (buildingPlacer != null && buildingPlacer.IsActive) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                _dragStart = Mouse.current.position.ReadValue();
                _dragging = true;
            }
            else if (_dragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                Select(_dragStart, Mouse.current.position.ReadValue());
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
                IssueMove();
        }

        void Select(Vector2 a, Vector2 b)
        {
            ClearSelection();

            if (Vector2.Distance(a, b) < clickThreshold)
            {
                Ray ray = _cam.ScreenPointToRay(a);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f, selectableMask))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();
                    if (sel != null) Add(sel);
                }
                return;
            }

            Rect box = RectFrom(a, b);
            foreach (var sel in FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                Vector3 sp = _cam.WorldToScreenPoint(sel.transform.position);
                if (sp.z > 0f && box.Contains(new Vector2(sp.x, sp.y)))
                    Add(sel);
            }
        }

        void IssueMove()
        {
            if (_selected.Count == 0) return;
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask)) return;

            int i = 0;
            foreach (var s in _selected)
            {
                if (s == null) continue;
                var unit = s.GetComponent<UnitController>();
                if (unit != null) unit.MoveTo(hit.point + Formation(i++, _selected.Count));
            }
        }

        void Add(Selectable s)
        {
            s.SetSelected(true);
            _selected.Add(s);
        }

        void ClearSelection()
        {
            foreach (var s in _selected) if (s != null) s.SetSelected(false);
            _selected.Clear();
        }

        static Vector3 Formation(int index, int count)
        {
            if (count <= 1) return Vector3.zero;
            int perRow = Mathf.CeilToInt(Mathf.Sqrt(count));
            int row = index / perRow;
            int col = index % perRow;
            return new Vector3((col - perRow / 2f) * 1.5f, 0f, (row - perRow / 2f) * 1.5f);
        }

        static Rect RectFrom(Vector2 a, Vector2 b) =>
            new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
