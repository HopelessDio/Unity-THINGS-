using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>Grounded visual-only feeding: contact, hold, then a short body pull.</summary>
    public sealed class FeedingZombiePose : MonoBehaviour
    {
        [SerializeField] private Transform hips, spine, chest, head, mouthAnchor, contactTarget;
        [SerializeField] private Transform[] leftLeg, rightLeg, leftArm, rightArm;
        [SerializeField] private Transform[] posedBones;
        [SerializeField] private Vector3[] restPositions;
        [SerializeField] private Quaternion[] restRotations;
        [SerializeField] private Vector3 leftFootTarget, rightFootTarget, leftHandTarget, rightHandTarget;
        public Vector3 MouthPosition => mouthAnchor.position;
        public Vector3 ContactPosition => contactTarget.position;
        private bool holding;
        private float heldTime;
        public void Hold(float time) { holding = true; heldTime = time; }
        private void OnEnable() => holding = false;

        public void Configure(Transform pelvis, Transform back, Transform upperBack, Transform skull,
            Transform mouth, Transform target, Transform[] legL, Transform[] legR, Transform[] armL, Transform[] armR)
        {
            hips = pelvis; spine = back; chest = upperBack; head = skull;
            mouthAnchor = mouth; contactTarget = target;
            leftLeg = legL; rightLeg = legR; leftArm = armL; rightArm = armR;
            leftFootTarget = legL[2].position; rightFootTarget = legR[2].position;
            leftHandTarget = armL[2].position; rightHandTarget = armR[2].position;
            posedBones = hips.GetComponentsInChildren<Transform>();
            restPositions = new Vector3[posedBones.Length]; restRotations = new Quaternion[posedBones.Length];
            for (int i = 0; i < posedBones.Length; i++)
            { restPositions[i] = posedBones[i].localPosition; restRotations[i] = posedBones[i].localRotation; }
        }
        private void LateUpdate() => Evaluate(holding ? heldTime : Time.time);
        public void Evaluate(float time)
        {
            if (hips == null || contactTarget == null) return;
            Restore();
            float phase = Mathf.Repeat(time / 2.8f, 1f);
            float pull = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .73f, phase)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.79f, 1f, phase)));
            float bite = Mathf.Sin(time * 15f) * .9f * (1 - pull);
            Vector3 axis = transform.right;
            spine.rotation = Quaternion.AngleAxis(-pull * 7f, axis) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(bite, axis) * chest.rotation;
            head.rotation = Quaternion.AngleAxis(bite * 1.7f - pull * 5f, axis) * head.rotation;
            Vector3 desired = contactTarget.position + Vector3.up * (.015f + pull * .16f) - transform.forward * (pull * .075f);
            hips.position += desired - mouthAnchor.position;
            Solve(leftLeg, leftFootTarget, transform.forward);
            Solve(rightLeg, rightFootTarget, transform.forward);
            Solve(leftArm, leftHandTarget, -transform.right);
            Solve(rightArm, rightHandTarget, transform.right);
        }
        private void Restore()
        {
            if (posedBones == null) return;
            for (int i = 0; i < posedBones.Length; i++) if (posedBones[i] != null)
            { posedBones[i].localPosition = restPositions[i]; posedBones[i].localRotation = restRotations[i]; }
        }
        private void OnDisable() => Restore();
        public static void Solve(Transform[] chain, Vector3 target, Vector3 pole)
        {
            Transform a = chain[0], b = chain[1], c = chain[2];
            float l1 = Vector3.Distance(a.position, b.position), l2 = Vector3.Distance(b.position, c.position);
            Vector3 delta = target - a.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + .002f, l1 + l2 - .002f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
            float along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
            Vector3 elbow = a.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            Quaternion endRotation = c.rotation;
            a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, a.position + direction * distance - b.position) * b.rotation;
            c.rotation = endRotation;
        }
    }
}
