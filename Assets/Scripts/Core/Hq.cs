using UnityEngine;

namespace Nightwall
{
    /// <summary>The headquarters the player defends. Losing it ends the run.</summary>
    [RequireComponent(typeof(Health))]
    public class Hq : MonoBehaviour
    {
        Health _health;
        Vector2Int _anchor;
        Vector2Int _footprint;
        bool _registered;

        void Awake()
        {
            _health = GetComponent<Health>();
            _health.Died += _ =>
            {
                if (GameManager.Instance != null) GameManager.Instance.OnHqDestroyed();
            };
        }

        void Start()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            // Derive cell footprint from the HQ's world-space XZ scale.
            Vector3 scale = transform.localScale;
            int w = Mathf.Max(1, Mathf.RoundToInt(scale.x / grid.CellSize));
            int h = Mathf.Max(1, Mathf.RoundToInt(scale.z / grid.CellSize));
            _footprint = new Vector2Int(w, h);

            // Min corner of the footprint on the XZ plane.
            Vector3 min = transform.position - new Vector3(scale.x * 0.5f, 0f, scale.z * 0.5f);
            _anchor = grid.WorldToCell(min);

            grid.Occupy(_anchor, _footprint, CellState.PlayerBase);
            _registered = true;
        }

        void OnDestroy()
        {
            if (_registered && GridSystem.Instance != null)
                GridSystem.Instance.Free(_anchor, _footprint);
        }
    }
}
