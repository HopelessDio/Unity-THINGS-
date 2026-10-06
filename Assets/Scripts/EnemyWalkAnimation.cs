using UnityEngine;

public class EnemyWalkAnimation : MonoBehaviour
{
    public Transform visual;
    public float bobSpeed = 7f;
    public float bobHeight = 0.08f;
    public float swayAngle = 6f;
    public float squashAmount = 0.05f;
    public float artworkAngleOffset = 90f;

    private EnemyFollow enemyFollow;
    private Vector3 startingLocalPosition;
    private Vector3 startingLocalScale;
    private float animationOffset;

    void Awake()
    {
        // This component lives on EnemyVisual, while EnemyFollow lives on the
        // parent Enemy object.
        enemyFollow = GetComponentInParent<EnemyFollow>();
        animationOffset = Random.Range(0f, Mathf.PI * 2f);

        if (visual != null)
        {
            startingLocalPosition = visual.localPosition;
            startingLocalScale = visual.localScale;
        }
    }

    void Update()
    {
        if (visual == null)
            return;

        float walkCycle = Mathf.Sin(Time.time * bobSpeed + animationOffset);
        float step = Mathf.Abs(walkCycle);

        visual.localPosition = startingLocalPosition + Vector3.up * step * bobHeight;
        visual.localScale = new Vector3(
            startingLocalScale.x * (1f + step * squashAmount),
            startingLocalScale.y * (1f - step * squashAmount),
            startingLocalScale.z);

        // A freshly spawned enemy receives its player target immediately after
        // Awake, so safely wait if either reference is not ready yet.
        if (enemyFollow == null || enemyFollow.player == null)
            return;

        Vector2 direction = enemyFollow.player.position - transform.position;
        float facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + artworkAngleOffset;
        visual.rotation = Quaternion.Euler(0f, 0f, facingAngle + walkCycle * swayAngle);
    }
}
