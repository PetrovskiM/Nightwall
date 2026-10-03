using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Build mode: B toggles it, 1/2 pick a structure, R rotates, left-click places on the
    /// grid-snapped ground point, Esc cancels. The ghost preview snaps to cell centres via the
    /// <see cref="GridSystem"/> and turns red when the target cells are out of bounds or already
    /// occupied; placement is refused there.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        [SerializeField] List<GameObject> buildables = new();
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] Material ghostMaterial;
        [SerializeField] Color validTint = new Color(0.4f, 0.9f, 1f, 0.5f);
        [SerializeField] Color invalidTint = new Color(1f, 0.3f, 0.3f, 0.5f);

        public bool IsActive { get; private set; }

        int _index;
        float _yaw;
        GameObject _ghost;
        Material _ghostInstance;
        Camera _cam;
        bool _validPlacement;

        void Awake() => _cam = Camera.main;

        void Update()
        {
            if (_cam == null) _cam = Camera.main;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) Toggle();
            if (!IsActive) return;

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SelectIndex(0);
                if (kb.digit2Key.wasPressedThisFrame) SelectIndex(1);
                if (kb.rKey.wasPressedThisFrame) _yaw += 90f;
                if (kb.escapeKey.wasPressedThisFrame) { Toggle(); return; }
            }

            UpdateGhost();

            if (_ghost != null && _validPlacement &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                Place();
        }

        public void Toggle()
        {
            IsActive = !IsActive;
            if (IsActive) BuildGhost();
            else ClearGhost();
        }

        void SelectIndex(int i)
        {
            if (i < 0 || i >= buildables.Count) return;
            _index = i;
            BuildGhost();
        }

        void BuildGhost()
        {
            ClearGhost();
            if (buildables.Count == 0 || buildables[_index] == null) return;
            _ghost = Instantiate(buildables[_index]);
            _ghost.name = "BuildGhost";
            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            foreach (var col in _ghost.GetComponentsInChildren<Collider>()) col.enabled = false;

            if (ghostMaterial != null)
            {
                _ghostInstance = new Material(ghostMaterial);
                foreach (var r in _ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _ghostInstance;
            }
        }

        void ClearGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            if (_ghostInstance != null) Destroy(_ghostInstance);
            _ghost = null;
            _ghostInstance = null;
        }

        void UpdateGhost()
        {
            if (_ghost == null || Mouse.current == null) return;

            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask)) return;

            GridSystem grid = GridSystem.Instance;
            Vector2Int footprint = CurrentFootprint();
            if (grid != null)
            {
                Vector2Int cell = grid.WorldToCell(hit.point);
                _ghost.transform.position = grid.CellToWorld(cell);
                _validPlacement = grid.CanPlace(cell, footprint);
            }
            else
            {
                _ghost.transform.position = hit.point;
                _validPlacement = true;
            }
            _ghost.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            Tint(_validPlacement ? validTint : invalidTint);
        }

        void Place()
        {
            if (buildables.Count == 0 || buildables[_index] == null) return;
            var go = Instantiate(buildables[_index], _ghost.transform.position, _ghost.transform.rotation);
            go.name = buildables[_index].name;
        }

        Vector2Int CurrentFootprint()
        {
            var b = buildables[_index] != null ? buildables[_index].GetComponent<Buildable>() : null;
            return b != null ? b.Footprint : new Vector2Int(1, 1);
        }

        void Tint(Color color)
        {
            if (_ghostInstance == null) return;
            if (_ghostInstance.HasProperty("_BaseColor")) _ghostInstance.SetColor("_BaseColor", color);
            if (_ghostInstance.HasProperty("_Color")) _ghostInstance.SetColor("_Color", color);
        }
    }
}
