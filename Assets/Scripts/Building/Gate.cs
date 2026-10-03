using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// A gate: a defensive structure the player can open and close. When closed it carves the
    /// NavMesh like a wall, so the horde must route around it (or breach it when walled out). When
    /// open the carving obstacle is lifted and the gate sinks aside, re-opening the lane through it.
    ///
    /// The player toggles a gate freely during the day; at night the gate is forced to its
    /// configured <see cref="openAtNight"/> state and ignores input, then restores the player's
    /// daytime choice when the next day begins. Owns its own open/closed state only — it never
    /// mutates other systems (it reads the phase from <see cref="GameManager"/> via an event).
    /// </summary>
    [RequireComponent(typeof(NavMeshObstacle))]
    public class Gate : DefensiveStructure
    {
        [Tooltip("State the gate is forced into once night falls.")]
        [SerializeField] bool openAtNight = false;

        [Tooltip("How far the gate sinks when open, so the lane reads as clear.")]
        [SerializeField] float openDropDistance = 1.5f;

        NavMeshObstacle _obstacle;
        Vector3 _closedLocalPos;
        bool _isOpen;
        bool _desiredDayState;

        /// <summary>True while the gate is open (passable, not carving the NavMesh).</summary>
        public bool IsOpen => _isOpen;

        void Awake()
        {
            _obstacle = GetComponent<NavMeshObstacle>();
            _closedLocalPos = transform.localPosition;
            // Gates start closed; the player opens them deliberately.
            ApplyState(false);
        }

        void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateChanged -= OnPhaseChanged;
        }

        /// <summary>Flips the gate open/closed. Only honoured while the player may build (daytime).</summary>
        public void Toggle()
        {
            if (GameManager.Instance != null && !GameManager.Instance.CanBuild) return;
            _desiredDayState = !_isOpen;
            ApplyState(_desiredDayState);
        }

        void OnPhaseChanged(GameState state)
        {
            // Night forces the configured state; day restores the player's last daytime choice.
            if (state == GameState.Night) ApplyState(openAtNight);
            else if (state == GameState.Day) ApplyState(_desiredDayState);
        }

        /// <summary>Applies an open/closed state: lift/restore carving and slide the visual aside.</summary>
        void ApplyState(bool open)
        {
            _isOpen = open;
            if (_obstacle != null) _obstacle.enabled = !open;
            transform.localPosition = open
                ? _closedLocalPos + Vector3.down * openDropDistance
                : _closedLocalPos;
        }
    }
}
