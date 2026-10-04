using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// NavMesh-driven locomotion: a thin wrapper over <see cref="NavMeshAgent"/> that paths to a
    /// destination and follows terrain automatically. One responsibility — move the agent; the AI
    /// (<see cref="Enemy"/>) decides where and when. Adapted from the LowPolyArena project.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NavAgentMotor : MonoBehaviour, IMotor
    {
        [SerializeField] float moveSpeed = 3.5f;

        NavMeshAgent _agent;
        // Reused across filtered path requests to avoid allocating a new NavMeshPath each call.
        readonly NavMeshPath _filteredPath = new NavMeshPath();

        public float MoveSpeed
        {
            get => moveSpeed;
            set { moveSpeed = Mathf.Max(0f, value); if (_agent != null) _agent.speed = moveSpeed; }
        }

        /// <summary>True once the agent is placed on a baked NavMesh and can path.</summary>
        public bool IsOnNavMesh => _agent != null && _agent.isOnNavMesh;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
            _agent.updateRotation = true;
            _agent.updateUpAxis = false;
            // Randomise avoidance priority so agents don't lock into symmetric standoffs (equal
            // priority makes NavMesh agents freeze against each other and clump). A spread lets the
            // crowd resolve who yields, keeping the column flowing.
            _agent.avoidancePriority = Random.Range(20, 80);
        }

        /// <summary>Path toward a world-space point. No-op until the agent is on the NavMesh.</summary>
        public void SetDestination(Vector3 worldPos)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (_agent.isStopped) _agent.isStopped = false;
            _agent.SetDestination(worldPos);
        }

        /// <summary>
        /// Path toward <paramref name="worldPos"/> using a <see cref="NavMeshQueryFilter"/> so
        /// per-agent area costs are honoured (e.g. a Brute that avoids "NearWall" areas). Falls back
        /// to <see cref="SetDestination"/> when path calculation fails. No-op while off NavMesh.
        /// </summary>
        public void SetDestinationFiltered(Vector3 worldPos, NavMeshQueryFilter filter)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (_agent.isStopped) _agent.isStopped = false;

            _filteredPath.ClearCorners();
            if (NavMesh.CalculatePath(transform.position, worldPos, filter, _filteredPath))
                _agent.SetPath(_filteredPath);
            else
                _agent.SetDestination(worldPos); // graceful fallback: no path found for filter
        }

        /// <summary>Halt in place (e.g. while attacking a blocking structure).</summary>
        public void Stop()
        {
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        /// <summary>Resume following the current path after a <see cref="Stop"/>.</summary>
        public void Resume()
        {
            if (_agent != null && _agent.isOnNavMesh && _agent.isStopped) _agent.isStopped = false;
        }

        /// <summary>
        /// Snap onto the nearest NavMesh if the agent spawned slightly off it (e.g. a spawn point
        /// placed above the ground). Safe to call once movement is expected.
        /// </summary>
        public void WarpToNavMesh(float maxDistance = 5f)
        {
            if (_agent == null || _agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }

        /// <summary>
        /// Instantly displace the agent to <paramref name="worldPosition"/>, snapped to the nearest
        /// NavMesh point so it stays pathable — for knockback traps. A plain transform move would be
        /// overridden by the agent, so this goes through <see cref="NavMeshAgent.Warp"/>.
        /// </summary>
        public void Warp(Vector3 worldPosition, float maxSnapDistance = 3f)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, maxSnapDistance, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }
    }
}
