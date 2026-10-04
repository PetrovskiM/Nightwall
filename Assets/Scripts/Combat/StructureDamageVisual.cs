using System.Collections;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Tints a structure's renderer to show accumulated damage (healthy → damaged colour) and
    /// briefly flashes white on each hit. Uses <see cref="MaterialPropertyBlock"/> so no extra
    /// material instances are created — stays allocation-free and mobile-friendly.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class StructureDamageVisual : MonoBehaviour
    {
        [SerializeField] Color damagedColor = new Color(0.9f, 0.3f, 0.1f);
        [SerializeField] float flashDuration = 0.08f;

        Health _health;
        Renderer _renderer;
        MaterialPropertyBlock _block;
        Color _baseColor;
        bool _flashing;

        static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId      = Shader.PropertyToID("_Color");

        void Awake()
        {
            _health   = GetComponent<Health>();
            _renderer = GetComponentInChildren<Renderer>();
            _block    = new MaterialPropertyBlock();

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_block);
                // Prefer _BaseColor (URP Lit); fall back to _Color (Standard).
                _baseColor = _block.HasColor(BaseColorId) ? _block.GetColor(BaseColorId)
                           : _renderer.sharedMaterial != null ? _renderer.sharedMaterial.color
                           : Color.white;
            }
        }

        void OnEnable()
        {
            _health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            _health.Damaged -= OnDamaged;
        }

        void OnDamaged(Health h)
        {
            AudioManager.Instance?.PlayWallHit();
            ApplyDamageColor(h.Normalized);
            if (!_flashing) StartCoroutine(HitFlash());
        }

        void ApplyDamageColor(float healthFraction)
        {
            if (_renderer == null) return;
            Color c = Color.Lerp(damagedColor, _baseColor, healthFraction);
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_block);
        }

        IEnumerator HitFlash()
        {
            _flashing = true;
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, Color.white);
                _block.SetColor(ColorId, Color.white);
                _renderer.SetPropertyBlock(_block);
            }
            yield return new WaitForSeconds(flashDuration);
            // Restore damage-tinted color after the flash.
            ApplyDamageColor(_health.Normalized);
            _flashing = false;
        }
    }
}
