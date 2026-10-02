using UnityEngine;

namespace Nightwall
{
    /// <summary>The headquarters the player defends. Losing it ends the run.</summary>
    [RequireComponent(typeof(Health))]
    public class Hq : MonoBehaviour
    {
        Health _health;

        void Awake()
        {
            _health = GetComponent<Health>();
            _health.Died += _ =>
            {
                if (GameManager.Instance != null) GameManager.Instance.OnHqDestroyed();
            };
        }
    }
}
