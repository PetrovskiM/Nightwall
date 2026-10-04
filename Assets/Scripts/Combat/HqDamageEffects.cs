using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Plays a screen shake and audio cue when the HQ takes damage.
    /// Attach alongside <see cref="Hq"/> on the HQ GameObject.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HqDamageEffects : MonoBehaviour
    {
        [SerializeField] float shakeStrength = 0.35f;
        [SerializeField] float shakeDuration = 0.25f;

        Health _health;

        void Awake() => _health = GetComponent<Health>();

        void OnEnable()  => _health.Damaged += OnDamaged;
        void OnDisable() => _health.Damaged -= OnDamaged;

        void OnDamaged(Health h)
        {
            AudioManager.Instance?.PlayBaseDamage();
            ScreenShake.Instance?.Shake(shakeStrength, shakeDuration);
        }
    }
}
