using System;
using UnityEngine;

namespace Nightwall
{
    /// <summary>Shared hit-point container used by the HQ, buildings, units and enemies.</summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Normalized => maxHealth <= 0f ? 0f : Current / maxHealth;

        public event Action<Health> Damaged;
        public event Action<Health> Died;

        void Awake() => Current = maxHealth;

        public void SetMax(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            if (refill) Current = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(this);
            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + Mathf.Max(0f, amount));
        }
    }
}
