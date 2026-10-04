using System.Collections;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Plays a brief flash and audio cue when this enemy's <see cref="Health"/> reaches zero.
    /// Attach alongside <see cref="Enemy"/> on the enemy prefab.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyDeathEffect : MonoBehaviour
    {
        [SerializeField] float flashDuration = 0.05f;

        Health _health;
        Renderer _renderer;
        MaterialPropertyBlock _block;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId     = Shader.PropertyToID("_Color");

        void Awake()
        {
            _health   = GetComponent<Health>();
            _renderer = GetComponentInChildren<Renderer>();
            _block    = new MaterialPropertyBlock();
        }

        void OnEnable()  => _health.Died += OnDied;
        void OnDisable() => _health.Died -= OnDied;

        void OnDied(Health h)
        {
            AudioManager.Instance?.PlayEnemyDeath();
            if (_renderer != null && gameObject.activeInHierarchy)
                StartCoroutine(DeathFlash());
        }

        IEnumerator DeathFlash()
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.white);
            _block.SetColor(ColorId, Color.white);
            _renderer.SetPropertyBlock(_block);
            yield return new WaitForSeconds(flashDuration);
            // GameObject is destroyed by Enemy after Died fires; no colour restore needed.
        }
    }
}
