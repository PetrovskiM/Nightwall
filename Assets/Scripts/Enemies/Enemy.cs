using System;
using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// Objective-driven enemy: it always heads for the HQ. Player-built walls are NavMesh obstacles
    /// (carving), so the agent reroutes around them automatically — the maze, and the "longer path =
    /// more survival time" trade-off, emerge from pathfinding rather than scripted lanes.
    ///
    /// The go-around-vs-destroy decision is settled by the agent's own path: while a complete route
    /// to the HQ exists the enemy simply follows it (it navigates around walls); only when it is
    /// genuinely walled out — no complete path — does it stop at the nearest blocking structure ahead
    /// and chew through it, reopening a route the instant the wall falls. This is the project's hard
    /// rule: breach ONLY when no path exists. Locomotion is delegated to <see cref="NavAgentMotor"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NavAgentMotor))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        [Header("Objective (the base)")]
        [SerializeField] float hqAttackRange = 3f;
        [SerializeField] float hqDamagePerSecond = 10f;

        [Header("Wall combat")]
        [Tooltip("Reach at which the enemy stops and strikes a blocking structure ahead of it.")]
        [SerializeField] float attackRange = 2.5f;
        [Tooltip("Sustained damage dealt to a wall while attacking. Per-hit damage = this × cooldown.")]
        [SerializeField] float wallDamagePerSecond = 15f;
        [Tooltip("Seconds between strikes (attack cadence). DPS is preserved regardless of this.")]
        [SerializeField] float attackCooldown = 1f;
        [Tooltip("Layers treated as breachable structures (walls/towers). Set to the Building layer.")]
        [SerializeField] LayerMask structureMask = ~0;

        [Tooltip("How often the enemy re-evaluates its route to the base.")]
        [SerializeField] float repathInterval = 0.5f;

        public event Action<Enemy> Died;

        NavMeshAgent _agent;
        NavAgentMotor _motor;
        Health _health;
        AttackEffect _attackEffect;
        Transform _hq;
        Health _hqHealth;
        float _repathTimer;
        float _attackTimer;
        bool _walledOut;
        Vector3 _hqNavPoint;
        readonly Collider[] _hits = new Collider[8];

        // The wall currently being chewed. Tracked so we can repath the instant it dies instead of
        // waiting out the repath interval, and so the subscription is cleaned up symmetrically.
        Health _targetWall;
        Action<Health> _onTargetWallDied;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _motor = GetComponent<NavAgentMotor>();
            if (_motor == null) _motor = gameObject.AddComponent<NavAgentMotor>();
            _health = GetComponent<Health>();
            _attackEffect = GetComponent<AttackEffect>();

            // Default the breach mask to the Building layer if it was left as "Everything",
            // so the enemy only ever chews on structures — never units or the ground.
            if (structureMask.value == ~0)
            {
                int building = LayerMask.NameToLayer("Building");
                if (building >= 0) structureMask = 1 << building;
            }

            _onTargetWallDied = _ => _repathTimer = 0f; // wall fell: recompute the route immediately

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

            // Head for an on-mesh point by the HQ, not the HQ centre (which sits inside a blocker
            // and is off-mesh — a bad destination makes partial-path following misbehave).
            _hqNavPoint = _hq != null ? _hq.position : transform.position;
            if (_hq != null && NavMesh.SamplePosition(_hq.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                _hqNavPoint = hit.position;

            _motor.SetDestination(_hqNavPoint);
            // Desync repaths so the whole horde doesn't recompute on the same frame.
            _repathTimer = UnityEngine.Random.Range(0f, repathInterval);
        }

        void OnDestroy() => ClearTargetWall();

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
                ClearTargetWall();
                _motor.Stop();
                TryAttack(_hqHealth, hqDamagePerSecond);
                return;
            }

            // Keep the objective current; carving walls make the path reroute itself.
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                _walledOut = ComputeWalledOut();       // judge the current, settled path first
                _motor.SetDestination(_hqNavPoint);    // then request a fresh one
            }

            // Only break walls when there is genuinely NO complete route to the base.
            if (_walledOut)
            {
                Health blocker = FindBlockerTowardHq();
                if (blocker != null)
                {
                    TrackTargetWall(blocker);
                    _motor.Stop();                      // stop at attack range, don't shove the wall
                    TryAttack(blocker, wallDamagePerSecond);
                    return;
                }
            }

            ClearTargetWall();
            _motor.Resume();
        }

        /// <summary>
        /// "Is the base unreachable?" judged from the agent's OWN path — the one it actually
        /// follows — so the breach decision can't disagree with where the agent is really going.
        /// A complete path means not walled out. A partial path only counts as walled out when its
        /// farthest point still lands well short of the HQ; a partial path that gets close (e.g.
        /// right up to the HQ blocker) is fine and the enemy keeps closing in to attack.
        /// </summary>
        bool ComputeWalledOut()
        {
            if (!_agent.isOnNavMesh || _agent.pathPending) return _walledOut; // keep last while pending
            if (_agent.pathStatus == NavMeshPathStatus.PathComplete) return false;

            Vector3[] corners = _agent.path.corners;
            if (corners.Length == 0) return true;

            Vector3 end = corners[corners.Length - 1];
            float margin = hqAttackRange + 2f;
            var flatEnd = new Vector2(end.x, end.z);
            var flatHq = new Vector2(_hq.position.x, _hq.position.z);
            return (flatEnd - flatHq).sqrMagnitude > margin * margin;
        }

        /// <summary>Nearest live structure within attack range that lies ahead toward the HQ, or null.</summary>
        Health FindBlockerTowardHq()
        {
            Vector3 pos = transform.position;
            Vector3 toHq = _hq.position - pos; toHq.y = 0f;
            bool haveDir = toHq.sqrMagnitude > 0.0001f;
            if (haveDir) toHq.Normalize();

            int n = Physics.OverlapSphereNonAlloc(pos, attackRange, _hits, structureMask, QueryTriggerInteraction.Ignore);
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

        /// <summary>
        /// Apply damage on the attack cadence. Per strike we deal <paramref name="damagePerSecond"/>
        /// × <see cref="attackCooldown"/>, so the authored DPS holds regardless of the cadence, and
        /// a wall takes a predictable time to fall however often the strike "animation" plays.
        /// </summary>
        void TryAttack(Health target, float damagePerSecond)
        {
            if (target == null || target.IsDead) return;
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                _attackTimer = attackCooldown;
                target.TakeDamage(damagePerSecond * attackCooldown);
                _attackEffect?.Play();
            }
        }

        /// <summary>Subscribe to the wall we're attacking so its death triggers an immediate repath.</summary>
        void TrackTargetWall(Health wall)
        {
            if (_targetWall == wall) return;
            ClearTargetWall();
            _targetWall = wall;
            if (_targetWall != null) _targetWall.Died += _onTargetWallDied;
        }

        void ClearTargetWall()
        {
            if (_targetWall != null) _targetWall.Died -= _onTargetWallDied;
            _targetWall = null;
        }
    }
}
