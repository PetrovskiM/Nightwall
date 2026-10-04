using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Central audio hook registry. Assign AudioClips in the Inspector; leave slots empty to
    /// silence those events. All sounds play 2D (UI-style) and are lightweight for mobile.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Structure")]
        [SerializeField] AudioClip wallHit;
        [SerializeField] AudioClip wallDestruction;

        [Header("Trap")]
        [SerializeField] AudioClip trapActivation;

        [Header("Enemy")]
        [SerializeField] AudioClip enemyDeath;

        [Header("Base")]
        [SerializeField] AudioClip baseDamage;

        [Header("Wave")]
        [SerializeField] AudioClip waveStart;
        [SerializeField] AudioClip waveComplete;

        AudioSource _source;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.spatialBlend = 0f; // 2D
            _source.playOnAwake = false;
        }

        public void PlayWallHit()         => Play(wallHit, 0.6f);
        public void PlayWallDestruction() => Play(wallDestruction);
        public void PlayTrapActivation()  => Play(trapActivation, 0.7f);
        public void PlayEnemyDeath()      => Play(enemyDeath, 0.5f);
        public void PlayBaseDamage()      => Play(baseDamage);
        public void PlayWaveStart()       => Play(waveStart);
        public void PlayWaveComplete()    => Play(waveComplete);

        void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip, volume);
        }
    }
}
