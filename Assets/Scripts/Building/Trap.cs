using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// A floor trap: a trigger volume that slows any enemy crossing it to a fraction of its speed,
    /// restoring the original speed on exit. Unlike a wall it does not block the path — it is a tool
    /// for shaping *where* and *how fast* the horde flows, buying the defences time. Placeholder
    /// mechanic for the prototype; no damage, no animation.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Trap : MonoBehaviour
    {
        [Range(0.05f, 1f)]
        [SerializeField] float slowMultiplier = 0.4f;

        // Remember each affected motor's full-speed value so overlapping traps don't stack or
        // leave an enemy permanently crippled.
        readonly Dictionary<NavAgentMotor, float> _baseSpeed = new();

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var motor = other.GetComponentInParent<NavAgentMotor>();
            if (motor == null || _baseSpeed.ContainsKey(motor)) return;
            _baseSpeed[motor] = motor.MoveSpeed;
            motor.MoveSpeed = motor.MoveSpeed * slowMultiplier;
            AudioManager.Instance?.PlayTrapActivation();
        }

        void OnTriggerExit(Collider other)
        {
            var motor = other.GetComponentInParent<NavAgentMotor>();
            if (motor == null || !_baseSpeed.TryGetValue(motor, out float speed)) return;
            motor.MoveSpeed = speed;
            _baseSpeed.Remove(motor);
        }
    }
}
