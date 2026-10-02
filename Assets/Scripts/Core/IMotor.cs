namespace Nightwall
{
    /// <summary>
    /// Shared locomotion contract so systems can tune movement speed without knowing whether the
    /// mover is a NavMesh agent or a directly-steered body. (Adapted from the LowPolyArena project.)
    /// </summary>
    public interface IMotor
    {
        /// <summary>Desired movement speed in world units per second.</summary>
        float MoveSpeed { get; set; }
    }
}
