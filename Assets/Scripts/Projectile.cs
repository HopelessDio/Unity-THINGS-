using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 8f;
    public Vector2 direction;
    void Update()
{
    transform.Translate(direction * speed * Time.deltaTime);
}
}
