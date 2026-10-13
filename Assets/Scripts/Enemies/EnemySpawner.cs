using UnityEngine;

namespace Survivor.Enemies
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyChaser enemyPrefab;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private ArenaSpawnArea spawnArea;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float spawnInterval = 2f;
        [SerializeField, Min(0.1f)] private float minimumSpawnInterval = 0.5f;
        [SerializeField, Min(0f)] private float intervalDecreasePerMinute = 0.2f;
        [SerializeField, Min(1)] private int maximumEnemies = 20;
        [SerializeField, Min(0)] private int additionalEnemiesPerMinute = 5;

        [Header("Enemy Scaling Per Minute")]
        [SerializeField, Min(0f)] private float speedIncrease = 0.1f;
        [SerializeField, Min(0f)] private float healthIncrease = 0.15f;

        [Header("Spawn Ring")]
        [SerializeField, Min(0f)] private float minimumSpawnDistance = 10f;
        [SerializeField, Min(0f)] private float maximumSpawnDistance = 14f;

        private float nextSpawnTime;
        private float elapsedTime;

        private void Start()
        {
            nextSpawnTime = Time.time + spawnInterval;
        }

        private void Update()
        {
            elapsedTime += Time.deltaTime;

            if (enemyPrefab == null || playerTarget == null || Time.time < nextSpawnTime)
            {
                return;
            }

            float elapsedMinutes = elapsedTime / 60f;
            float currentSpawnInterval = Mathf.Max(
                minimumSpawnInterval,
                spawnInterval - intervalDecreasePerMinute * elapsedMinutes);
            nextSpawnTime = Time.time + currentSpawnInterval;

            int completedMinutes = Mathf.FloorToInt(elapsedMinutes);
            int currentMaximumEnemies = maximumEnemies
                + additionalEnemiesPerMinute * completedMinutes;
            int enemyCount = FindObjectsByType<EnemyChaser>(FindObjectsSortMode.None).Length;
            if (enemyCount >= currentMaximumEnemies)
            {
                return;
            }

            SpawnEnemy(elapsedMinutes);
        }

        private void SpawnEnemy(float elapsedMinutes)
        {
            if (spawnArea == null)
            {
                Debug.LogError("Enemy Spawner needs an Arena Spawn Area reference.", this);
                enabled = false;
                return;
            }

            if (!spawnArea.TryGetPointInRing(
                    playerTarget.position,
                    minimumSpawnDistance,
                    maximumSpawnDistance,
                    out Vector3 spawnPosition))
            {
                return;
            }

            EnemyChaser enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            enemy.SetTarget(playerTarget);
            enemy.ApplySpeedMultiplier(1f + speedIncrease * elapsedMinutes);

            if (enemy.TryGetComponent(out EnemyHealth enemyHealth))
            {
                enemyHealth.ApplyHealthMultiplier(1f + healthIncrease * elapsedMinutes);
            }
        }
    }
}
