using System;
using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// Navigates toward the HQ. When blocked by (or adjacent to) a wall or the HQ, it stops and
    /// attacks the nearest damageable target in range until the path clears.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        [SerializeField] float attackRange = 3f;
        [SerializeField] float attackDamage = 10f;
        [SerializeField] float attackInterval = 1f;
        [SerializeField] float repathInterval = 0.5f;

        public event Action<Enemy> Died;

        NavMeshAgent _agent;
        Health _health;
        Transform _hq;
        float _attackTimer;
        float _repathTimer;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _health.Died += _ =>
            {
                Died?.Invoke(this);
                Destroy(gameObject);
            };
        }

        public void Init(Transform hq) => _hq = hq;

        void Update()
        {
            if (_hq == null) return;

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                if (_agent.isOnNavMesh) _agent.SetDestination(_hq.position);
            }

            _attackTimer -= Time.deltaTime;
            Health target = FindTargetInRange();
            if (target != null)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = attackInterval;
                    target.TakeDamage(attackDamage);
                }
            }
            else if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }

        Health FindTargetInRange()
        {
            Health best = null;
            float bestSq = attackRange * attackRange;
            foreach (var col in Physics.OverlapSphere(transform.position, attackRange))
            {
                Health h = col.GetComponentInParent<Health>();
                if (h == null || h == _health || h.IsDead) continue;
                if (h.GetComponent<Enemy>() != null) continue; // ignore other enemies
                float d = (h.transform.position - transform.position).sqrMagnitude;
                if (d <= bestSq) { bestSq = d; best = h; }
            }
            return best;
        }
    }
}
