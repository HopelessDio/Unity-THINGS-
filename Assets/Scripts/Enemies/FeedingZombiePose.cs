using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>Visual-only feeding loop for the discovery tableau; not combat AI.</summary>
    public sealed class FeedingZombiePose : MonoBehaviour
    {
        [SerializeField] private Transform head, neck, leftArm, rightArm;
        [SerializeField] private Quaternion headRest, neckRest, leftRest, rightRest;
        public void Configure(Transform headBone, Transform neckBone, Transform left, Transform right)
        {
            head = headBone; neck = neckBone; leftArm = left; rightArm = right;
            headRest = head.localRotation; neckRest = neck.localRotation;
            leftRest = left.localRotation; rightRest = right.localRotation;
        }
        private void LateUpdate()
        {
            float cycle = Time.time * 2.4f;
            float nod = Mathf.Sin(cycle) * 4f;
            if (head != null) head.localRotation = headRest * Quaternion.Euler(nod, 0, Mathf.Sin(cycle * .5f) * 2f);
            if (neck != null) neck.localRotation = neckRest * Quaternion.Euler(nod * .4f, 0, 0);
            if (leftArm != null) leftArm.localRotation = leftRest * Quaternion.Euler(0, 0, Mathf.Sin(cycle) * 1.2f);
            if (rightArm != null) rightArm.localRotation = rightRest * Quaternion.Euler(0, 0, -Mathf.Sin(cycle + .4f) * 1.2f);
        }
        private void OnDisable()
        {
            if (head != null) head.localRotation = headRest;
            if (neck != null) neck.localRotation = neckRest;
            if (leftArm != null) leftArm.localRotation = leftRest;
            if (rightArm != null) rightArm.localRotation = rightRest;
        }
    }
}
