using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 8f;
    public float lifetime = 3f;
    public int damage = 1;

    [Header("Blue Fire Look")]
    public Color coreColor = new Color(0.3f, 0.9f, 1f, 1f);
    public Color flameColor = new Color(1f, 0.38f, 0.04f, 1f);
    public Color emberColor = new Color(1f, 0.05f, 0.02f, 1f);
    public Vector2 visualSize = new Vector2(0.34f, 0.11f);
    public float trailDuration = 0.14f;

    private Vector2 direction;
    private bool hasHit;
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private SpriteRenderer bulletRenderer;
    private SpriteRenderer glowRenderer;
    private SpriteRenderer emberRenderer;
    private TrailRenderer trailRenderer;
    private float flickerOffset;
    private static Material sharedTrailMaterial;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();

        if (body != null)
        {
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        transform.localScale = Vector3.one;
        flickerOffset = Random.Range(0f, Mathf.PI * 2f);
        CreateBulletVisual(sourceRenderer);
        CreateBulletGlow();
        CreateTrail();
    }

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    public void Initialize(Vector2 travelDirection)
    {
        direction = travelDirection.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        trailRenderer?.Clear();
    }

    void FixedUpdate()
    {
        if (hasHit || direction == Vector2.zero)
            return;

        Vector2 nextPosition = (Vector2)transform.position + direction * speed * Time.fixedDeltaTime;
        if (body != null)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    void Update()
    {
        if (hasHit || bulletRenderer == null)
            return;

        float flicker = 0.5f + 0.5f * Mathf.Sin(Time.time * 55f + flickerOffset);
        bulletRenderer.transform.localScale = new Vector3(
            visualSize.x * (1f + flicker * 0.1f),
            visualSize.y * (1f - flicker * 0.08f),
            1f);
        bulletRenderer.color = Color.Lerp(coreColor, Color.white, flicker * 0.32f);

        if (glowRenderer != null)
        {
            glowRenderer.transform.localScale = new Vector3(
                1.45f + flicker * 0.2f,
                1.7f + flicker * 0.25f,
                1f);
            glowRenderer.color = new Color(flameColor.r, flameColor.g, flameColor.b, 0.38f + flicker * 0.18f);
        }

        if (emberRenderer != null)
            emberRenderer.color = new Color(emberColor.r, emberColor.g, emberColor.b, 0.2f + flicker * 0.12f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || !other.TryGetComponent(out EnemyHealth enemyHealth))
            return;

        hasHit = true;
        enemyHealth.TakeDamage(damage, direction);
        StartCoroutine(FinishImpact());
    }

    void CreateBulletVisual(SpriteRenderer sourceRenderer)
    {
        if (sourceRenderer == null)
            return;

        GameObject visualObject = new GameObject("Bullet Visual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localScale = new Vector3(visualSize.x, visualSize.y, 1f);

        bulletRenderer = visualObject.AddComponent<SpriteRenderer>();
        bulletRenderer.sprite = sourceRenderer.sprite;
        bulletRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        bulletRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        bulletRenderer.color = coreColor;
        bulletRenderer.sortingOrder = 12;
        sourceRenderer.enabled = false;

        if (projectileCollider is CircleCollider2D circleCollider)
            circleCollider.radius = 0.12f;
    }

    void CreateBulletGlow()
    {
        if (bulletRenderer == null)
            return;

        GameObject glowObject = new GameObject("Bullet Glow");
        glowObject.transform.SetParent(bulletRenderer.transform, false);
        glowObject.transform.localScale = new Vector3(1.5f, 1.8f, 1f);

        glowRenderer = glowObject.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = bulletRenderer.sprite;
        glowRenderer.sharedMaterial = bulletRenderer.sharedMaterial;
        glowRenderer.color = new Color(flameColor.r, flameColor.g, flameColor.b, 0.5f);
        glowRenderer.sortingLayerID = bulletRenderer.sortingLayerID;
        glowRenderer.sortingOrder = bulletRenderer.sortingOrder - 1;

        GameObject emberObject = new GameObject("Red Ember Glow");
        emberObject.transform.SetParent(bulletRenderer.transform, false);
        emberObject.transform.localScale = new Vector3(2.15f, 2.4f, 1f);

        emberRenderer = emberObject.AddComponent<SpriteRenderer>();
        emberRenderer.sprite = bulletRenderer.sprite;
        emberRenderer.sharedMaterial = bulletRenderer.sharedMaterial;
        emberRenderer.color = new Color(emberColor.r, emberColor.g, emberColor.b, 0.28f);
        emberRenderer.sortingLayerID = bulletRenderer.sortingLayerID;
        emberRenderer.sortingOrder = bulletRenderer.sortingOrder - 2;
    }

    void CreateTrail()
    {
        GameObject trailObject = new GameObject("Bullet Trail");
        trailObject.transform.SetParent(transform, false);

        trailRenderer = trailObject.AddComponent<TrailRenderer>();
        trailRenderer.time = trailDuration;
        trailRenderer.minVertexDistance = 0.025f;
        trailRenderer.startWidth = 0.13f;
        trailRenderer.endWidth = 0f;
        trailRenderer.alignment = LineAlignment.View;
        trailRenderer.textureMode = LineTextureMode.Stretch;
        trailRenderer.numCapVertices = 3;
        trailRenderer.numCornerVertices = 2;

        if (sharedTrailMaterial == null)
        {
            Shader trailShader = Shader.Find("Sprites/Default");
            if (trailShader != null)
                sharedTrailMaterial = new Material(trailShader);
        }

        if (sharedTrailMaterial != null)
            trailRenderer.sharedMaterial = sharedTrailMaterial;

        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[]
            {
                new GradientColorKey(coreColor, 0f),
                new GradientColorKey(flameColor, 0.38f),
                new GradientColorKey(emberColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.65f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        trailRenderer.colorGradient = trailGradient;

        if (bulletRenderer != null)
        {
            trailRenderer.sortingLayerID = bulletRenderer.sortingLayerID;
            trailRenderer.sortingOrder = bulletRenderer.sortingOrder - 2;
        }
    }

    IEnumerator FinishImpact()
    {
        if (projectileCollider != null)
            projectileCollider.enabled = false;
        if (body != null)
            body.simulated = false;
        if (bulletRenderer != null)
            bulletRenderer.enabled = false;
        if (glowRenderer != null)
            glowRenderer.enabled = false;
        if (emberRenderer != null)
            emberRenderer.enabled = false;
        if (trailRenderer != null)
            trailRenderer.emitting = false;

        yield return new WaitForSeconds(trailDuration);
        Destroy(gameObject);
    }
}
