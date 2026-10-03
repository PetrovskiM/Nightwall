using UnityEngine;
using UnityEngine.AI;

namespace Nightwall
{
    /// <summary>
    /// A single place the horde can pour in from. One per entrance; the <see cref="WaveSpawner"/>
    /// owns a set of these and decides which are active each wave. A spawn point only produces an
    /// enemy and points it at the base — the enemy's own NavMesh pathing then picks the appropriate
    /// route in, so different entrances naturally yield attacks from different directions.
    ///
    /// Draws itself as a gizmo so designers can see every entrance (and its line to the base) in the
    /// Scene view without pressing Play.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("Colour used for this entrance's debug gizmo, so multiple points are tell-apart-able.")]
        [SerializeField] Color gizmoColor = new Color(0.9f, 0.3f, 0.2f, 1f);
        [Tooltip("Radius an enemy is snapped onto the NavMesh within when it appears here.")]
        [SerializeField] float navSampleRadius = 4f;

        /// <summary>Spawn one enemy at this point, snapped to the mesh, and aim it at the base.</summary>
        /// <returns>The spawned <see cref="Enemy"/>, or null if the prefab is missing or had no Enemy.</returns>
        public Enemy Spawn(GameObject enemyPrefab, Transform hq)
        {
            if (enemyPrefab == null) return null;

            Vector3 pos = transform.position;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                pos = hit.position;

            var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
            var enemy = go.GetComponent<Enemy>();
            if (enemy != null) enemy.Init(hq);
            return enemy;
        }

        /// <summary>Gizmo tint, exposed so the spawner can draw matching route lines to the base.</summary>
        public Color GizmoColor => gizmoColor;

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p, 1.5f);
            Gizmos.DrawLine(p, p + Vector3.up * 3f);
        }
    }
}
