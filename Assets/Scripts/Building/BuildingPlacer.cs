using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Wall placement and removal.
    ///
    /// Placement: B toggles build mode, 1/2 pick structure, R rotates, Esc cancels.
    ///   Mouse: left-click places; ghost follows cursor with green (valid) / red (invalid) tint.
    ///   Touch: single tap places; two-finger tap cancels build mode.
    ///
    /// Removal: right-click (mouse) or long-press (touch, future) on any placed building.
    ///   Works regardless of build mode.
    ///
    /// UI buttons call <see cref="ActivateForIndex"/> directly.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        [SerializeField] List<GameObject> buildables = new();
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] LayerMask buildingMask;
        [SerializeField] Material ghostMaterial;
        [SerializeField] Color validTint   = new Color(0.2f, 0.9f, 0.2f, 0.5f);
        [SerializeField] Color invalidTint = new Color(1f,   0.3f, 0.3f, 0.5f);

        public bool IsActive { get; private set; }

        /// <summary>The prefabs this placer can build, in hotkey/UI order. Read-only view for UI.</summary>
        public IReadOnlyList<GameObject> Buildables => buildables;

        /// <summary>Index of the currently selected buildable (valid only while <see cref="IsActive"/>).</summary>
        public int SelectedIndex => _index;

        int _index;
        float _yaw;
        GameObject _ghost;
        Material _ghostInstance;
        Camera _cam;
        bool _validPlacement;

        // Touch tracking: ignore taps that were part of a pan drag.
        Vector2 _touchDownPos;
        const float TapMoveTolerance = 20f;

        readonly Collider[] _enemyHits = new Collider[8];

        void Awake() => _cam = Camera.main;

        void Update()
        {
            if (_cam == null) _cam = Camera.main;

            // Building is a daytime activity. When night falls (or the run ends) disable all
            // placement/removal input and cancel any build mode left open from the day.
            if (GameManager.Instance != null && !GameManager.Instance.CanBuild)
            {
                if (IsActive) { IsActive = false; ClearGhost(); }
                return;
            }

            HandleRemoveInput();

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) Toggle();
            if (!IsActive) { HandleGateInput(); return; }

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SelectIndex(0);
                if (kb.digit2Key.wasPressedThisFrame) SelectIndex(1);
                if (kb.digit3Key.wasPressedThisFrame) SelectIndex(2);
                if (kb.digit4Key.wasPressedThisFrame) SelectIndex(3);
                if (kb.rKey.wasPressedThisFrame) _yaw += 90f;
                if (kb.escapeKey.wasPressedThisFrame) { Toggle(); return; }
            }

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                HandleTouch();
                if (_ghost != null) UpdateGhostAtPosition(ScreenToGroundPoint(
                    Touchscreen.current.touches[0].position.ReadValue()));
            }
            else
            {
                UpdateGhost();
                // Held button paints across cells: after a cell is placed it becomes occupied,
                // so _validPlacement flips false there and only new empty cells get walls.
                if (_ghost != null && _validPlacement &&
                    Mouse.current != null && Mouse.current.leftButton.isPressed)
                    Place();
            }
        }

        // ── Input handlers ────────────────────────────────────────────────────

        void HandleRemoveInput()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                TryRemove(Mouse.current.position.ReadValue());
        }

        /// <summary>
        /// Left-click on a placed gate (while not in build mode) opens or closes it. Lets the player
        /// manage gates during the day without entering placement mode.
        /// </summary>
        void HandleGateInput()
        {
            if (_cam == null) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, buildingMask))
                hit.collider.GetComponentInParent<Gate>()?.Toggle();
        }

        void HandleTouch()
        {
            var touches = Touchscreen.current.touches;
            int activeCount = 0;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].isInProgress) activeCount++;

            if (activeCount >= 2) { Toggle(); return; }

            var t0 = touches[0];
            var phase = t0.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                _touchDownPos = t0.position.ReadValue();
            else if (phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                Vector2 up = t0.position.ReadValue();
                if (Vector2.Distance(up, _touchDownPos) < TapMoveTolerance && _ghost != null && _validPlacement)
                    Place();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Enters build mode for the given buildable index. Called by UI buttons.</summary>
        public void ActivateForIndex(int index)
        {
            _index = Mathf.Clamp(index, 0, buildables.Count - 1);
            if (!IsActive) Toggle();
            else BuildGhost();
        }

        public void Toggle()
        {
            IsActive = !IsActive;
            if (IsActive) BuildGhost();
            else ClearGhost();
        }

        // ── Placement ─────────────────────────────────────────────────────────

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
            // The obstacle isn't a MonoBehaviour/Collider; leaving it on would carve the NavMesh
            // and shove the horde around as the ghost tracks the cursor.
            foreach (var ob in _ghost.GetComponentsInChildren<NavMeshObstacle>()) ob.enabled = false;

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
            Vector3? worldPos = ScreenToGroundPoint(Mouse.current.position.ReadValue());
            if (worldPos.HasValue) UpdateGhostAtPosition(worldPos);
        }

        void UpdateGhostAtPosition(Vector3? worldPos)
        {
            if (_ghost == null || !worldPos.HasValue) return;

            GridSystem grid = GridSystem.Instance;
            Vector2Int footprint = CurrentFootprint();
            if (grid != null)
            {
                Vector2Int cell = grid.WorldToCell(worldPos.Value);
                _ghost.transform.position = grid.CellToWorld(cell);
                _validPlacement = grid.CanPlace(cell, footprint) &&
                                  !CellHasEnemy(_ghost.transform.position);
            }
            else
            {
                _ghost.transform.position = worldPos.Value;
                _validPlacement = true;
            }
            _ghost.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            Tint(_validPlacement ? validTint : invalidTint);
        }

        void Place()
        {
            if (buildables.Count == 0 || buildables[_index] == null) return;

            GridSystem grid = GridSystem.Instance;
            Vector2Int footprint = CurrentFootprint();
            if (grid != null)
            {
                Vector2Int cell = grid.WorldToCell(_ghost.transform.position);
                if (!grid.CanPlace(cell, footprint)) return;        // guard against a stale valid flag
                if (CellHasEnemy(_ghost.transform.position)) return; // never trap/overlap an enemy
                grid.Occupy(cell, footprint);                       // claim the cell now, no 1-frame gap
            }

            Instantiate(buildables[_index], _ghost.transform.position, _ghost.transform.rotation)
                .name = buildables[_index].name;
        }

        // ── Removal ───────────────────────────────────────────────────────────

        /// <summary>
        /// Destroys a placed building under <paramref name="screenPos"/>.
        /// Decoupled from build mode so touch can call it the same way.
        /// </summary>
        public void TryRemove(Vector2 screenPos)
        {
            if (_cam == null) return;
            Ray ray = _cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, buildingMask))
            {
                var buildable = hit.collider.GetComponentInParent<Buildable>();
                if (buildable != null) Destroy(buildable.gameObject);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        Vector3? ScreenToGroundPoint(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask) ? hit.point : (Vector3?)null;
        }

        Vector2Int CurrentFootprint()
        {
            var b = buildables[_index] != null ? buildables[_index].GetComponent<Buildable>() : null;
            return b != null ? b.Footprint : new Vector2Int(1, 1);
        }

        /// <summary>True when a live enemy is standing on the target cell — can't build on it.</summary>
        bool CellHasEnemy(Vector3 cellCenter)
        {
            float cs = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            var half = new Vector3(cs * 0.45f, 1.5f, cs * 0.45f);
            int n = Physics.OverlapBoxNonAlloc(cellCenter + Vector3.up, half, _enemyHits,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (_enemyHits[i] != null && _enemyHits[i].GetComponentInParent<Enemy>() != null)
                    return true;
            return false;
        }

        void Tint(Color color)
        {
            if (_ghostInstance == null) return;
            if (_ghostInstance.HasProperty("_BaseColor")) _ghostInstance.SetColor("_BaseColor", color);
            if (_ghostInstance.HasProperty("_Color")) _ghostInstance.SetColor("_Color", color);
        }
    }
}
