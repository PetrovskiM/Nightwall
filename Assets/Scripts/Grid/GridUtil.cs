using UnityEngine;

namespace Nightwall
{
    /// <summary>Helpers for snapping world positions onto the build grid.</summary>
    public static class GridUtil
    {
        public const float CellSize = 1f;

        public static Vector3 Snap(Vector3 world, float cell = CellSize)
        {
            return new Vector3(
                Mathf.Round(world.x / cell) * cell,
                world.y,
                Mathf.Round(world.z / cell) * cell);
        }
    }
}
