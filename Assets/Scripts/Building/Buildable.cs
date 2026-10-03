using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A placed defensive structure (wall, trap, ...). Walls carry a carving
    /// <see cref="UnityEngine.AI.NavMeshObstacle"/> on the prefab so the horde reroutes around them;
    /// this component just ties the structure's lifetime to the grid — it reserves its footprint
    /// cells while alive and frees them when destroyed, so nothing can be stacked on top of it.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Buildable : MonoBehaviour
    {
        [SerializeField] Vector2Int footprint = new Vector2Int(1, 1);

        public Vector2Int Footprint => footprint;

        Vector2Int _anchor;
        bool _registered;

        void Start()
        {
            if (GridSystem.Instance != null)
            {
                _anchor = GridSystem.Instance.WorldToCell(transform.position);
                GridSystem.Instance.Occupy(_anchor, footprint);
                _registered = true;
            }

            GetComponent<Health>().Died += _ => Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_registered && GridSystem.Instance != null)
                GridSystem.Instance.Free(_anchor, footprint);
        }
    }
}
