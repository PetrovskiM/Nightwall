using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// Wall placement and removal. All pointer input is read through <see cref="InputService"/>, so
    /// this class is identical on phone, tablet and desktop and never touches a mouse/touch API.
    ///
    /// Placement: B toggles build mode, 1-4 pick structure, R rotates, Esc cancels.
    ///   Tap (or left-click)      → place on the targeted cell; the ghost + cell highlight show
    ///                              green (valid) / red (invalid) feedback as it tracks the pointer.
    ///   Hold the pointer down    → paint walls across empty cells (desktop convenience).
    ///
    /// Removal / contextual: long-press (touch) or right-click (desktop) on a placed building removes
    ///   it; on a gate (outside build mode) it toggles the gate. Works regardless of build mode.
    ///
    /// Placement is suppressed while a two-finger camera gesture is active, so panning never builds.
    /// UI buttons call <see cref="ActivateForIndex"/> directly.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        [SerializeField] List<GameObject> buildables = new();
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] LayerMask buildingMask;
        [SerializeField] Material ghostMaterial;
        [SerializeField] CellHighlighter cellHighlighter;
        [SerializeField] Color validTint   = new Color(0.2f, 0.9f, 0.2f, 0.5f);
        [SerializeField] Color invalidTint = new Color(1f,   0.3f, 0.3f, 0.5f);

        public bool IsActive { get; private set; }

        /// <summary>Raised whenever build mode turns on (true) or off (false). Lets structures react
        /// (e.g. switch to an x-ray look so the player can see the cells behind them).</summary>
        public event System.Action<bool> BuildModeChanged;

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

        readonly Collider[] _enemyHits = new Collider[8];

        void Awake() => _cam = Camera.main;

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            InputService input = InputService.Instance;
            if (input == null) return;

            // Building is a daytime activity. When night falls (or the run ends) disable all
            // placement/removal input and cancel any build mode left open from the day.
            if (GameManager.Instance != null && !GameManager.Instance.CanBuild)
            {
                if (IsActive)
                {
                    IsActive = false;
                    ClearGhost();
                    if (cellHighlighter != null) cellHighlighter.Hide();
                    BuildModeChanged?.Invoke(false);
                }
                return;
            }

            // Long-press / right-click removes a structure anywhere, regardless of build mode.
            if (input.Hold) TryRemove(input.HoldPosition);

            HandleKeyboardHotkeys();

            if (!IsActive)
            {
                if (cellHighlighter != null) cellHighlighter.Hide();
                HandleGateInput(input);
                return;
            }

            // Track the ghost + highlight to the pointer's cell every frame.
            if (input.HasPointer)
                UpdateGhostAtPosition(ScreenToGroundPoint(input.PointerPosition));

            // Never build while the player is mid-gesture (two-finger pan/pinch/twist).
            if (input.IsGesturing) return;

            // A committed tap places once; a held pointer paints across empty cells (occupied cells
            // flip _validPlacement false, so only new cells receive walls).
            if (_ghost != null && _validPlacement && (input.Tap || input.PrimaryHeld))
                Place();
        }

        // ── Input handlers ────────────────────────────────────────────────────

        void HandleKeyboardHotkeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.bKey.wasPressedThisFrame) Toggle();
            if (!IsActive) return;
            if (kb.digit1Key.wasPressedThisFrame) SelectIndex(0);
            if (kb.digit2Key.wasPressedThisFrame) SelectIndex(1);
            if (kb.digit3Key.wasPressedThisFrame) SelectIndex(2);
            if (kb.digit4Key.wasPressedThisFrame) SelectIndex(3);
            if (kb.rKey.wasPressedThisFrame) _yaw += 90f;
            if (kb.escapeKey.wasPressedThisFrame) Toggle();
        }

        /// <summary>
        /// A tap on a placed gate (while not in build mode) opens or closes it. Lets the player
        /// manage gates during the day without entering placement mode.
        /// </summary>
        void HandleGateInput(InputService input)
        {
            if (_cam == null || !input.Tap) return;
            Ray ray = _cam.ScreenPointToRay(input.TapPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, buildingMask))
                hit.collider.GetComponentInParent<Gate>()?.Toggle();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Enters build mode for the given buildable index. Called by UI buttons.</summary>
        /// <summary>
        /// Applied by <see cref="LevelLoader"/> in <c>Awake</c>. Replaces the serialized
        /// buildables list so levels can restrict or expand the build palette.
        /// </summary>
        public void Configure(System.Collections.Generic.IReadOnlyList<GameObject> structures)
        {
            buildables.Clear();
            foreach (var s in structures)
                if (s != null) buildables.Add(s);
            _index = 0;
        }

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
            BuildModeChanged?.Invoke(IsActive);
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
            if (cellHighlighter != null) cellHighlighter.Hide();
        }

        void UpdateGhostAtPosition(Vector3? worldPos)
        {
            if (_ghost == null) return;
            if (!worldPos.HasValue)
            {
                // Pointer isn't over the ground (e.g. off the map) — nothing to target.
                if (cellHighlighter != null) cellHighlighter.Hide();
                _validPlacement = false;
                return;
            }

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
            // Can't afford the selected structure → show the invalid (red) feedback.
            if (MaterialBank.Instance != null && !MaterialBank.Instance.CanAfford(CurrentCost()))
                _validPlacement = false;

            _ghost.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            Tint(_validPlacement ? validTint : invalidTint);

            if (cellHighlighter != null)
                cellHighlighter.Show(_ghost.transform.position, footprint,
                    grid != null ? grid.CellSize : 1f, _validPlacement);
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
            }

            // Charge for the structure once the spot is confirmed buildable; bail if unaffordable
            // (the ghost already shows red in that case).
            if (MaterialBank.Instance != null && !MaterialBank.Instance.TrySpend(CurrentCost())) return;

            if (grid != null)
            {
                Vector2Int cell = grid.WorldToCell(_ghost.transform.position);
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

        /// <summary>Material cost of the currently selected structure (0 when it carries no metadata).</summary>
        int CurrentCost()
        {
            if (buildables.Count == 0 || buildables[_index] == null) return 0;
            var s = buildables[_index].GetComponent<DefensiveStructure>();
            return s != null ? s.Cost : 0;
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
