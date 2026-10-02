using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>A player-controlled unit that moves via the NavMesh.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitController : MonoBehaviour
    {
        NavMeshAgent _agent;

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public void MoveTo(Vector3 destination)
        {
            if (_agent != null && _agent.isOnNavMesh)
                _agent.SetDestination(destination);
        }
    }
}
