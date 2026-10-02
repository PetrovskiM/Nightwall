using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Build mode: B toggles it, 1/2 pick a structure, R rotates, left-click places on the
    /// grid-snapped ground point, Esc cancels. Shows a translucent ghost preview.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        [SerializeField] List<GameObject> buildables = new();
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] Material ghostMaterial;

        public bool IsActive { get; private set; }

        int _index;
        float _yaw;
        GameObject _ghost;
        Camera _cam;

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

            if (_ghost != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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
                foreach (var r in _ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;
        }

        void ClearGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        void UpdateGhost()
        {
            if (_ghost == null || Mouse.current == null) return;
            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            {
                _ghost.transform.position = GridUtil.Snap(hit.point);
                _ghost.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
        }

        void Place()
        {
            if (buildables.Count == 0 || buildables[_index] == null) return;
            var go = Instantiate(buildables[_index], _ghost.transform.position, _ghost.transform.rotation);
            go.name = buildables[_index].name;
        }
    }
}
