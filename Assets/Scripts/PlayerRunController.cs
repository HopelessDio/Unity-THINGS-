using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRunController : MonoBehaviour
{
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public Sprite idleSprite;
    public PlayerAim playerAim;

    private int currentAnimation;
    private bool externalAnimationLocked;
    private static readonly int RunAnimation = Animator.StringToHash("PlayerRun");
    private static readonly int StrafeAnimation = Animator.StringToHash("PlayerStrafe");
    private static readonly int DiagonalAnimation = Animator.StringToHash("PlayerDiagonal");

    void Awake()
    {
        if (playerAim == null)
            playerAim = GetComponentInParent<PlayerAim>();
    }

    void Update()
    {
        if (externalAnimationLocked)
            return;

        if (Keyboard.current == null)
            return;

        Vector2 movement = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            movement.y += 1f;
        if (Keyboard.current.sKey.isPressed)
            movement.y -= 1f;
        if (Keyboard.current.aKey.isPressed)
            movement.x -= 1f;
        if (Keyboard.current.dKey.isPressed)
            movement.x += 1f;

        bool isRunning = movement != Vector2.zero;

        animator.enabled = isRunning;

        if (!isRunning)
        {
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.flipX = false;
            currentAnimation = 0;
            return;
        }

        movement.Normalize();

        Vector2 aimDirection = playerAim != null ? playerAim.AimDirection : Vector2.down;
        Vector2 aimRight = new Vector2(-aimDirection.y, aimDirection.x);
        float forwardMovement = Vector2.Dot(movement, aimDirection);
        float sidewaysMovement = Vector2.Dot(movement, aimRight);

        // When the player moves both forward/backward and sideways relative to
        // the gun direction, use the diagonal run. The same artwork is mirrored
        // automatically for movement toward the left side of the gun.
        bool isDiagonal = Mathf.Abs(forwardMovement) > 0.35f &&
                          Mathf.Abs(sidewaysMovement) > 0.35f;
        bool isStrafing = !isDiagonal &&
                          Mathf.Abs(sidewaysMovement) > Mathf.Abs(forwardMovement);

        int nextAnimation = isDiagonal
            ? DiagonalAnimation
            : isStrafing
                ? StrafeAnimation
                : RunAnimation;

        if (nextAnimation != currentAnimation)
        {
            animator.Play(nextAnimation, 0, 0f);
            currentAnimation = nextAnimation;
        }

        spriteRenderer.flipX = (isStrafing || isDiagonal) && sidewaysMovement < 0f;
    }

    public void SetExternalAnimationLock(bool locked)
    {
        externalAnimationLocked = locked;
        currentAnimation = 0;

        if (locked)
            animator.enabled = true;
        else
            animator.speed = 1f;
    }
}
