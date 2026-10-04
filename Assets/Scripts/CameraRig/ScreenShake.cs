using System.Collections;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Applies a quick positional shake to the camera rig. Attach to the CinemachineCamera object.
    /// IsoCameraController sets the base position each frame; this component adds a transient offset
    /// each LateUpdate so the two don't fight. Call <see cref="Instance"/>.<see cref="Shake"/>
    /// from anywhere.
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        public static ScreenShake Instance { get; private set; }

        [SerializeField] float defaultStrength = 0.25f;
        [SerializeField] float defaultDuration = 0.2f;

        Vector3 _offset;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        /// <summary>Trigger a camera shake with optional strength and duration overrides.</summary>
        public void Shake(float strength = -1f, float duration = -1f)
        {
            if (strength < 0f) strength = defaultStrength;
            if (duration < 0f) duration = defaultDuration;
            StopAllCoroutines();
            StartCoroutine(DoShake(strength, duration));
        }

        IEnumerator DoShake(float strength, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float fade = Mathf.Lerp(1f, 0f, t);   // linear decay
                _offset = Random.insideUnitSphere * strength * fade;
                _offset.y = 0f;
                elapsed += Time.deltaTime;
                yield return null;
            }
            _offset = Vector3.zero;
        }

        void LateUpdate()
        {
            if (_offset.sqrMagnitude > 0.0001f)
                transform.position += _offset;
        }
    }
}
