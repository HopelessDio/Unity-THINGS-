using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 8f;
    public float lifetime = 3f;
    public int damage = 1;

    private Vector2 direction;
    private bool hasHit;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Initialize(Vector2 travelDirection)
    {
        direction = travelDirection.normalized;
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || !other.TryGetComponent(out EnemyHealth enemyHealth))
            return;

        hasHit = true;
        enemyHealth.TakeDamage(damage);
        Destroy(gameObject);
    }
}
