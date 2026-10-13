using UnityEngine;

namespace Survivor.Enemies
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArenaSpawnArea : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float edgePadding = 1f;
        [SerializeField, Min(1)] private int placementAttempts = 20;
        [SerializeField] private LayerMask blockedLayers;
        [SerializeField, Min(0.1f)] private float obstacleClearance = 0.75f;

        private BoxCollider areaCollider;

        private void Awake()
        {
            areaCollider = GetComponent<BoxCollider>();
            areaCollider.isTrigger = true;
        }

        public bool TryGetPointInRing(
            Vector3 center,
            float minimumDistance,
            float maximumDistance,
            out Vector3 spawnPoint)
        {
            Bounds bounds = areaCollider.bounds;
            float minimum = Mathf.Min(minimumDistance, maximumDistance);
            float maximum = Mathf.Max(minimumDistance, maximumDistance);

            for (int attempt = 0; attempt < placementAttempts; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.01f)
                {
                    direction = Vector2.right;
                }

                float distance = Random.Range(minimum, maximum);
                Vector3 candidate = center + new Vector3(direction.x, 0f, direction.y) * distance;

                bool insideX = candidate.x >= bounds.min.x + edgePadding
                    && candidate.x <= bounds.max.x - edgePadding;
                bool insideZ = candidate.z >= bounds.min.z + edgePadding
                    && candidate.z <= bounds.max.z - edgePadding;

                bool blocked = Physics.CheckSphere(
                    candidate + Vector3.up * 0.5f,
                    obstacleClearance,
                    blockedLayers,
                    QueryTriggerInteraction.Collide);

                if (insideX && insideZ && !blocked)
                {
                    spawnPoint = candidate;
                    return true;
                }
            }

            spawnPoint = default;
            return false;
        }
    }
}
