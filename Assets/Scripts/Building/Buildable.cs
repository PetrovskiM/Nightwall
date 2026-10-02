using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A placed defensive structure (wall / tower). Carries health and, via a NavMeshObstacle
    /// with carving enabled on the prefab, forces enemies to path around it.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Buildable : MonoBehaviour
    {
        [SerializeField] Vector2Int footprint = new Vector2Int(1, 1);

        public Vector2Int Footprint => footprint;

        void Awake()
        {
            GetComponent<Health>().Died += _ => Destroy(gameObject);
        }
    }
}
