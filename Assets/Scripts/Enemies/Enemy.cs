using System;
using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// Objective-driven enemy: it always heads for the HQ. Player-built walls are NavMesh
    /// obstacles (carving), so the agent reroutes around them automatically — the maze, and the
    /// "longer path = more survival time" trade-off, emerge from pathfinding rather than scripted
    /// lanes. The enemy attacks a structure ONLY when it is genuinely walled out: when no complete
    /// path to the HQ exists it breaches the nearest blocker toward the base to reopen a route.
    /// Locomotion is delegated to <see cref="NavAgentMotor"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NavAgentMotor))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        [Header("Objective (the base)")]
        [SerializeField] float hqAttackRange = 3f;
        [SerializeField] float hqDamage = 10f;

        [Header("Breaching (only when no path exists)")]
        [Tooltip("When fully walled out, the nearest blocking structure toward the HQ within this radius is attacked.")]
        [SerializeField] float breachRange = 2.5f;
        [SerializeField] float breachDamage = 15f;
        [SerializeField] float attackInterval = 1f;
        [Tooltip("Layers treated as breachable structures (walls/towers/HQ). Set to the Building layer.")]
        [SerializeField] LayerMask structureMask = ~0;

        [SerializeField] float repathInterval = 0.5f;

        public event Action<Enemy> Died;

        NavMeshAgent _agent;
        NavAgentMotor _motor;
        Health _health;
        Transform _hq;
        Health _hqHealth;
        float _repathTimer;
        float _attackTimer;
        bool _walledOut;
        NavMeshPath _path;
        readonly Collider[] _hits = new Collider[8];

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _motor = GetComponent<NavAgentMotor>();
            if (_motor == null) _motor = gameObject.AddComponent<NavAgentMotor>();
            _health = GetComponent<Health>();

            // Default the breach mask to the Building layer if it was left as "Everything",
            // so the enemy only ever chews on structures — never units or the ground.
            if (structureMask.value == ~0)
            {
                int building = LayerMask.NameToLayer("Building");
                if (building >= 0) structureMask = 1 << building;
            }

            _health.Died += _ =>
            {
                Died?.Invoke(this);
                Destroy(gameObject);
            };
        }

        public void Init(Transform hq)
        {
            _hq = hq;
            _hqHealth = hq != null ? hq.GetComponent<Health>() : null;
        }

        void Start()
        {
            _motor.WarpToNavMesh();
            if (_hq != null) _motor.SetDestination(_hq.position);
        }

        void Update()
        {
            if (_hq == null) return;

            // A freshly-placed wall can carve the navmesh out from under the agent; warp it back
            // on so it keeps pathing instead of freezing (SetDestination no-ops while off-mesh).
            if (!_agent.isOnNavMesh)
            {
                _motor.WarpToNavMesh();
                if (!_agent.isOnNavMesh) return;
            }

            // At the base? Attack it.
            if ((_hq.position - transform.position).sqrMagnitude <= hqAttackRange * hqAttackRange)
            {
                _motor.Stop();
                TryAttack(_hqHealth, hqDamage);
                return;
            }

            // Keep the objective current; carving walls make the path reroute itself.
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                _motor.SetDestination(_hq.position);
                _walledOut = ComputeWalledOut();
            }

            // Only break walls when there is genuinely NO complete route to the base.
            if (_walledOut)
            {
                Health blocker = FindBlockerTowardHq();
                if (blocker != null)
                {
                    _motor.Stop();
                    TryAttack(blocker, breachDamage);
                    return;
                }
            }

            _motor.Resume();
        }

        /// <summary>
        /// Authoritative "is the base reachable?" test. Computes a fresh path to the nearest
        /// navmesh point by the HQ — the agent's own <c>pathStatus</c> is stale for a frame or two
        /// right after a wall carves, which made the enemy stop and breach instead of rerouting.
        /// </summary>
        bool ComputeWalledOut()
        {
            if (!_agent.isOnNavMesh) return false;
            _path ??= new NavMeshPath();

            Vector3 target = _hq.position;
            if (NavMesh.SamplePosition(_hq.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                target = hit.position;

            _agent.CalculatePath(target, _path);
            return _path.status != NavMeshPathStatus.PathComplete;
        }

        /// <summary>Nearest live structure within breach range that lies ahead toward the HQ, or null.</summary>
        Health FindBlockerTowardHq()
        {
            Vector3 pos = transform.position;
            Vector3 toHq = _hq.position - pos; toHq.y = 0f;
            bool haveDir = toHq.sqrMagnitude > 0.0001f;
            if (haveDir) toHq.Normalize();

            int n = Physics.OverlapSphereNonAlloc(pos, breachRange, _hits, structureMask, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i];
                if (c == null) continue;
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead || h == _health) continue;
                if (h.GetComponent<Enemy>() != null) continue;

                // Direction to the nearest point on the structure (handles wide walls correctly).
                Vector3 to = c.ClosestPoint(pos) - pos; to.y = 0f;
                float d = to.sqrMagnitude;
                if (haveDir && d > 0.0001f && Vector3.Dot(toHq, to.normalized) < 0f) continue; // only ahead
                if (d < bestSqr) { bestSqr = d; best = h; }
            }
            return best;
        }

        void TryAttack(Health target, float damage)
        {
            if (target == null || target.IsDead) return;
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                _attackTimer = attackInterval;
                target.TakeDamage(damage);
            }
        }
    }
}
