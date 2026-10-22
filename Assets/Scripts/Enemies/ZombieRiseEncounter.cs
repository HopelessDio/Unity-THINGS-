using Survivor.Player;
using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>Stare -> authored-pose rise -> existing Humanoid combat prefab.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class ZombieRiseEncounter : MonoBehaviour
    {
        [SerializeField, Min(0)] private float stareDuration = 1f;
        [SerializeField, Min(.2f)] private float riseDuration = 1.6f;
        [SerializeField] private ZombieNoticePlayer notice;
        [SerializeField] private FeedingZombiePose feeding;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private GameObject combatEnemy;
        [SerializeField] private Transform[] bones;
        [SerializeField] private Vector3[] standingPositions;
        [SerializeField] private Quaternion[] standingRotations;
        private Vector3[] startPositions;
        private Quaternion[] startRotations;
        private Quaternion startFacing, endFacing;
        private float stareTime, riseTime;
        private AudioClip growl;
        public bool IsRising { get; private set; }
        public GameObject CombatEnemy => combatEnemy;
        public float RiseProgress => Mathf.Clamp01(riseTime / riseDuration);

        public void Configure(ZombieNoticePlayer reaction, FeedingZombiePose loop, PlayerHealth target,
            GameObject enemy, Transform[] staged, Vector3[] positions, Quaternion[] rotations)
        {
            notice = reaction; feeding = loop; player = target; combatEnemy = enemy;
            bones = staged; standingPositions = positions; standingRotations = rotations;
        }
        private void LateUpdate()
        {
            if (player == null || player.IsDead || Time.timeScale <= 0) return;
            if (!IsRising)
            {
                if (notice == null || !notice.IsWatching) return;
                stareTime += Time.deltaTime;
                if (stareTime < stareDuration) return;
                // Disabling the feeding loop restores its rest pose: capture first,
                // then reapply the captured pose before rendering the first rise frame.
                startPositions = new Vector3[bones.Length]; startRotations = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                { startPositions[i] = bones[i].localPosition; startRotations[i] = bones[i].localRotation; }
                startFacing = transform.rotation;
                Vector3 toward = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
                endFacing = toward.sqrMagnitude > .001f ? Quaternion.LookRotation(toward) : startFacing;
                notice.enabled = false; feeding.enabled = false;
                IsRising = true;
                PlayPlaceholderGrowl();
            }
            riseTime += Time.deltaTime;
            float blend = Mathf.SmoothStep(0, 1, RiseProgress);
            transform.rotation = Quaternion.Slerp(startFacing, endFacing, blend);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = Vector3.Lerp(startPositions[i], standingPositions[i], blend);
                bones[i].localRotation = Quaternion.Slerp(startRotations[i], standingRotations[i], blend);
            }
            if (RiseProgress < 1) return;
            combatEnemy.transform.SetPositionAndRotation(transform.position, transform.rotation);
            combatEnemy.GetComponent<AshboundZombie>().SetPlayerTarget(player);
            combatEnemy.SetActive(true);
            var animator = combatEnemy.GetComponentInChildren<Animator>();
            animator.Rebind(); animator.Update(0); // Never display one frame of the bind/T pose.
            gameObject.SetActive(false); // Retain staging object; Retry reloads its original active state.
        }
        private void PlayPlaceholderGrowl()
        {
            // Original synthesized placeholder, replaceable with recorded creature audio later.
            const int rate = 22050, length = 26460;
            var samples = new float[length]; uint seed = 7461; float noise = 0, phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                seed = seed * 1664525u + 1013904223u;
                noise = Mathf.Lerp(noise, ((seed >> 8) / 16777215f) * 2 - 1, .12f);
                phase += (68 - 24 * t / 1.2f + 3 * Mathf.Sin(t * 19)) * Mathf.PI * 2 / rate;
                float envelope = Mathf.Clamp01(t / .09f) * Mathf.Clamp01((1.2f - t) / .3f);
                samples[i] = (Mathf.Sin(phase) * .35f + Mathf.Sin(phase * 2.03f) * .12f + noise * .6f) * envelope;
            }
            growl = AudioClip.Create("Zombie rise - synthesized placeholder", length, 1, rate, false);
            growl.SetData(samples, 0);
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = .6f; source.volume = .4f;
            source.minDistance = 2; source.maxDistance = 18; source.clip = growl; source.Play();
        }
        private void OnDestroy() { if (growl != null) Destroy(growl); }
    }
}
