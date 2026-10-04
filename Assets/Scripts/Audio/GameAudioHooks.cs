using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Subscribes to <see cref="GameManager"/> phase transitions and plays the matching audio cues.
    /// Lives on the Systems GameObject so it outlives individual actors.
    /// </summary>
    public class GameAudioHooks : MonoBehaviour
    {
        bool _nightEnteredThisDay;

        void Start()
        {
            // GameManager is also on Systems; subscribe after both Awakes have run.
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameState state)
        {
            var am = AudioManager.Instance;
            if (am == null) return;

            switch (state)
            {
                case GameState.Night:
                    _nightEnteredThisDay = true;
                    am.PlayWaveStart();
                    break;
                case GameState.Dawn:
                    if (_nightEnteredThisDay)
                    {
                        am.PlayWaveComplete();
                        _nightEnteredThisDay = false;
                    }
                    break;
            }
        }
    }
}
