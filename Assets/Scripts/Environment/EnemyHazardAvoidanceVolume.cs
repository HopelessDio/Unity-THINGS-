using Survivor.Enemies;
using UnityEngine;

namespace Survivor.Environment
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EnemyHazardAvoidanceVolume : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float escapeSpeed = 8f;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.TryGetComponent(out EnemyChaser enemy))
            {
                enemy.EscapeHazard(transform.position, escapeSpeed);
            }
        }
    }
}
