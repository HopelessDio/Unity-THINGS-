using Survivor.Enemies;
using Survivor.Player;
using UnityEngine;

namespace Survivor.Environment
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class VoidKillZone : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlayerHealth playerHealth))
            {
                playerHealth.Kill();
                return;
            }

            if (other.TryGetComponent(out EnemyHealth enemyHealth))
            {
                enemyHealth.KillWithoutScore();
            }
        }
    }
}
