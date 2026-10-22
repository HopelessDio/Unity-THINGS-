#if UNITY_EDITOR
using UnityEngine;

namespace Survivor.Enemies
{
    /// <summary>Temporary Play-test instrumentation; excluded from player builds.</summary>
    [DefaultExecutionOrder(500), AddComponentMenu("")]
    public sealed class EnemyDashSpinProbe : MonoBehaviour
    {
        public Rigidbody PlayerBody;
        public int Contacts;
        public float PeakStep, PeakAngularSpeed;
        private Quaternion previous;
        private int skip;
        private void Awake() => ResetSample();
        public void ResetSample() { previous = GetComponent<Rigidbody>().rotation; skip = 2; }
        private void FixedUpdate()
        {
            var body = GetComponent<Rigidbody>();
            if (skip-- > 0) { previous = body.rotation; return; }
            PeakStep = Mathf.Max(PeakStep, Quaternion.Angle(previous, body.rotation));
            PeakAngularSpeed = Mathf.Max(PeakAngularSpeed, body.angularVelocity.magnitude);
            previous = body.rotation;
        }
        private void OnCollisionEnter(Collision hit) { if (hit.rigidbody == PlayerBody) Contacts++; }
        private void OnCollisionStay(Collision hit) { if (hit.rigidbody == PlayerBody) Contacts++; }
    }
}
#endif
