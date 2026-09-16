using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5000000f;

    void Update()
    {
        Vector2 movement = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            movement.y += 1;

        if (Keyboard.current.sKey.isPressed)
            movement.y -= 1;

        if (Keyboard.current.aKey.isPressed)
            movement.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            movement.x += 1;

        movement = movement.normalized;

        transform.Translate(movement * moveSpeed * Time.deltaTime);
    }
}