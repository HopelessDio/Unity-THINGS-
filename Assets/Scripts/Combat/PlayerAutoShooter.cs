using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Survivor.Combat
{
    public sealed class PlayerAutoShooter : MonoBehaviour
    {
        [Header("Aiming")]
        [SerializeField] private Camera aimCamera;

        [Header("Projectile")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Min(0f)] private float spawnHeight = 0.5f;
        [SerializeField, Min(0f)] private float spawnDistance = 0.75f;

        [Header("Weapon")]
        [SerializeField, Min(0.05f)] private float attackInterval = 0.6f;

        private float nextAttackTime;

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (projectilePrefab == null ||
                aimCamera == null ||
                Mouse.current == null)
            {
                return;
            }

            if (!Mouse.current.leftButton.isPressed || Time.time < nextAttackTime)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Ray pointerRay = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(pointerRay, out float enter))
            {
                FireAt(pointerRay.GetPoint(enter));
                nextAttackTime = Time.time + attackInterval;
            }
        }

        private void FireAt(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            direction.Normalize();

            Vector3 spawnPosition = transform.position
                + Vector3.up * spawnHeight
                + direction * spawnDistance;

            Projectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            projectile.Initialize(direction);
        }
    }
}
