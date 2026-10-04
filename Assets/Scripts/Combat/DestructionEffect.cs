using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Spawns a small burst of physics debris when the structure's <see cref="Health"/> reaches
    /// zero. Uses plain Unity primitives — no particle assets required — so it works out of the box
    /// in the prototype. Debris auto-destroys after a short lifetime.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DestructionEffect : MonoBehaviour
    {
        [SerializeField] int debrisCount = 5;
        [SerializeField] float debrisSize = 0.18f;
        [SerializeField] float launchForce = 4f;
        [SerializeField] float debrisLifetime = 1.5f;
        [SerializeField] Color debrisColor = new Color(0.55f, 0.55f, 0.6f);

        Health _health;

        void Awake() => _health = GetComponent<Health>();

        void OnEnable()  => _health.Died += OnDied;
        void OnDisable() => _health.Died -= OnDied;

        void OnDied(Health h)
        {
            AudioManager.Instance?.PlayWallDestruction();
            SpawnDebris();
        }

        void SpawnDebris()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            for (int i = 0; i < debrisCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = origin + Random.insideUnitSphere * 0.4f;
                go.transform.localScale = Vector3.one * debrisSize;
                go.transform.rotation = Random.rotation;
                go.layer = 0; // Default — debris should not interact with Building layer

                // Tint debris to match the structure.
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", debrisColor);
                    block.SetColor("_Color", debrisColor);
                    r.SetPropertyBlock(block);
                }

                // Remove the box collider so debris doesn't interfere with NavMesh or placement.
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rb = go.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.insideUnitSphere * launchForce + Vector3.up * launchForce * 0.5f;
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                rb.useGravity = true;
                rb.isKinematic = false;

                Destroy(go, debrisLifetime);
            }
        }
    }
}
