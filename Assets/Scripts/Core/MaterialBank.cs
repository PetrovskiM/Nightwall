using System;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// The run's material economy — the single source of truth for how many build materials the
    /// player holds. Structures are paid for through <see cref="TrySpend"/> at placement time, and
    /// the bank grants income for every night survived. UI reads <see cref="Balance"/> (or subscribes
    /// to <see cref="BalanceChanged"/>) and never writes back; gameplay spends through the public
    /// entry points only (one-directional flow). Tunable amounts live in serialized fields, not code.
    /// </summary>
    public class MaterialBank : MonoBehaviour
    {
        public static MaterialBank Instance { get; private set; }

        [Tooltip("Materials the player starts the run with (covers the first day's building).")]
        [SerializeField] int startingMaterials = 150;

        [Tooltip("Materials granted each time a night is survived (awarded at dawn).")]
        [SerializeField] int incomePerNight = 60;

        /// <summary>Current material balance.</summary>
        public int Balance { get; private set; }

        /// <summary>Raised whenever the balance changes. Argument is the new balance.</summary>
        public event Action<int> BalanceChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Balance = startingMaterials;
        }

        // Registering with another singleton's event is a cross-object hook-up, so it belongs in
        // Start (after every Awake has run and GameManager.Instance is guaranteed to exist).
        void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged += OnStateChanged;
            else
                Debug.LogError("[MaterialBank] No GameManager found; nightly income disabled.");
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>True when the balance can cover <paramref name="cost"/>.</summary>
        public bool CanAfford(int cost) => Balance >= cost;

        /// <summary>
        /// Deducts <paramref name="cost"/> if affordable and returns true; otherwise leaves the
        /// balance untouched and returns false. A zero/negative cost spends nothing but succeeds.
        /// </summary>
        public bool TrySpend(int cost)
        {
            if (cost <= 0) return true;
            if (!CanAfford(cost)) return false;
            Balance -= cost;
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        /// <summary>Adds materials to the balance (income, refunds, rewards).</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            BalanceChanged?.Invoke(Balance);
        }

        /// <summary>Grant the nightly income when a wave is survived (the Dawn phase).</summary>
        void OnStateChanged(GameState state)
        {
            if (state == GameState.Dawn) Add(incomePerNight);
        }
    }
}
