using UnityEngine;

public class EnemyFollow : MonoBehaviour
{
    public float moveSpeed = 2f;
    [Tooltip("How close the enemy can get to the player, in world units.")]
    public float stoppingDistance = 0.9f;
    public Transform player;

    void Update()
    {
        if (player == null)
            return;

        Vector2 direction = player.position - transform.position;
        float distance = direction.magnitude;

        // Keep the enemy at the edge of the player's space. Its trigger collider
        // still overlaps the player there, so EnemyContactDamage can keep firing.
        if (distance <= stoppingDistance)
            return;

        transform.Translate(direction / distance * moveSpeed * Time.deltaTime);
    }
}
