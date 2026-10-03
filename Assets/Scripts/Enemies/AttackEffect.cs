using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A deliberately cheap placeholder "attack" tell: a quick squash-and-recover punch on the
    /// visual so a player can read that an enemy is striking a wall, without any animation rig. One
    /// responsibility — play the punch; <see cref="Enemy"/> decides when to trigger it. The punch is
    /// driven by a timer in <see cref="Update"/> (no coroutine allocations) and is safe because the
    /// agent is halted while attacking, so scaling the transform doesn't fight locomotion.
    /// </summary>
    public class AttackEffect : MonoBehaviour
    {
        [Tooltip("How long one punch takes to recover to rest scale.")]
        [SerializeField] float duration = 0.18f;
        [Tooltip("Peak squash: the visual briefly flattens and widens by this fraction.")]
        [SerializeField] float squash = 0.25f;

        Vector3 _restScale;
        float _timer;

        void Awake() => _restScale = transform.localScale;

        /// <summary>Kick off one squash punch from the start of the recovery curve.</summary>
        public void Play() => _timer = duration;

        void Update()
        {
            if (_timer <= 0f) return;

            _timer -= Time.deltaTime;
            // t: 1 at the moment of impact, easing to 0 as it recovers to rest.
            float t = duration <= 0f ? 0f : Mathf.Clamp01(_timer / duration);
            float s = squash * t;
            transform.localScale = new Vector3(
                _restScale.x * (1f + s),
                _restScale.y * (1f - s),
                _restScale.z * (1f + s));

            if (_timer <= 0f) transform.localScale = _restScale;
        }
    }
}
