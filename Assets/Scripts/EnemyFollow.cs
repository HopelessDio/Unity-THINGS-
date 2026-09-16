using UnityEngine;

public class EnemyFollow : MonoBehaviour
{
    public float moveSpeed = 2f;
    public Transform player;

    void Update()
    {
        Vector2 direction = player.position - transform.position;
        direction = direction.normalized;

        transform.Translate(direction * moveSpeed * Time.deltaTime);
    }
}