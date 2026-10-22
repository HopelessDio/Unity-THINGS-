using Survivor.Player;
using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>One-shot visual reaction after entry. Does not enable movement or combat.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class ZombieNoticePlayer : MonoBehaviour
    {
        [SerializeField, Min(0)] private float noticeDelay = 4f;
        [SerializeField, Min(.1f)] private float turnDuration = 1.4f;
        [SerializeField] private FeedingZombiePose feeding;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private Transform spine, chest, neck, head;
        [SerializeField] private Quaternion headForwardOffset;
        private float elapsed, frozenTime;
        private float continuousYaw, trackingYaw, yawVelocity;
        private Quaternion trackingHead;
        public bool HasNoticed { get; private set; }
        public bool IsWatching => HasNoticed && elapsed >= noticeDelay + turnDuration;
        public Vector3 GazeDirection => head.rotation * Quaternion.Inverse(headForwardOffset) * Vector3.forward;
        public void ReactNow() => elapsed = noticeDelay;

        public void Configure(FeedingZombiePose loop, Transform back, Transform upperBack, Transform neckBone,
            Transform skull, Quaternion neutralOffset, PlayerHealth target)
        {
            feeding = loop; spine = back; chest = upperBack; neck = neckBone;
            head = skull; headForwardOffset = neutralOffset; player = target;
        }
        private void OnEnable() { elapsed = 0; HasNoticed = false; yawVelocity = trackingYaw = continuousYaw = 0; }
        private void Start() { if (player == null) player = FindAnyObjectByType<PlayerHealth>(); }
        private void LateUpdate() => TickReaction(Time.deltaTime, Time.time);
        private void TickReaction(float deltaTime, float feedingTime)
        {
            if (feeding == null || head == null || player == null || player.IsDead || Time.timeScale <= 0) return;
            if (deltaTime <= 0) return;
            elapsed += deltaTime;
            if (elapsed < noticeDelay) return;
            if (!HasNoticed)
            {
                HasNoticed = true; frozenTime = feedingTime; feeding.Hold(frozenTime);
                trackingHead = head.rotation;
                Vector3 initial = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
                continuousYaw = initial.sqrMagnitude > .0001f ? Vector3.SignedAngle(transform.forward, initial, Vector3.up) : 0;
            }
            // Rebuild the frozen feeding pose before every sample: no rotation drift.
            feeding.Evaluate(frozenTime);
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - noticeDelay) / turnDuration));
            Vector3 target = player.transform.position + Vector3.up * .55f;
            Vector3 flat = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (flat.sqrMagnitude < .0001f) return;
            float rawYaw = Vector3.SignedAngle(transform.forward, flat, Vector3.up);
            // Unwrap the +/-180 seam: crossing behind must keep the same shoulder.
            continuousYaw += Mathf.DeltaAngle(continuousYaw, rawYaw);
            // When the player comes back in front, allow a smooth return to neutral
            // rather than retaining turns from a complete circle around the actor.
            // Keep a 30-degree hysteresis band behind the actor. Only change
            // shoulder once the target has moved beyond the safe look-back arc;
            // SmoothDamp still carries that transition through the front safely.
            if (Mathf.Abs(rawYaw) < 90 || Mathf.Abs(continuousYaw) > 195) continuousYaw = rawYaw;
            trackingYaw = Mathf.SmoothDamp(trackingYaw, Mathf.Clamp(continuousYaw, -170, 170),
                ref yawVelocity, .22f, 140f, deltaTime);
            float yaw = trackingYaw;
            // Share the over-shoulder turn through the upper body rather than twisting only the neck.
            spine.rotation = Quaternion.AngleAxis(yaw * .25f * blend, Vector3.up) *
                Quaternion.AngleAxis(-18 * blend, transform.right) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(yaw * .22f * blend, Vector3.up) *
                Quaternion.AngleAxis(-10 * blend, transform.right) * chest.rotation;
            neck.rotation = Quaternion.AngleAxis(yaw * .18f * blend, Vector3.up) * neck.rotation;
            Vector3 direction = target - head.position;
            if (direction.sqrMagnitude > .0001f)
            {
                // Limit the head relative to the shared torso/neck yaw. Targets outside
                // this arc must wait for the body; never twist the neck through itself.
                Vector3 bodyForward = Quaternion.AngleAxis(yaw * .65f * blend, Vector3.up) * transform.forward;
                Vector3 headFlat = Vector3.ProjectOnPlane(direction, Vector3.up);
                float headYaw = headFlat.sqrMagnitude > .0001f ?
                    Mathf.Clamp(Vector3.SignedAngle(bodyForward, headFlat, Vector3.up), -80, 80) : 0;
                float pitch = Mathf.Clamp(Mathf.Atan2(direction.y, headFlat.magnitude) * Mathf.Rad2Deg, -35, 45);
                Vector3 look = Quaternion.AngleAxis(headYaw, Vector3.up) * bodyForward;
                look = look * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad);
                Quaternion desired = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(look, Vector3.up) * headForwardOffset, blend);
                trackingHead = Quaternion.RotateTowards(trackingHead, desired, 180f * deltaTime);
                head.rotation = trackingHead;
            }
        }
    }
}
