using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor.Player
{
    [RequireComponent(typeof(PlayerMovement), typeof(TrailRenderer))]
    public sealed class PlayerDashTrail : MonoBehaviour
    {
        private PlayerMovement playerMovement;
        private TrailRenderer dashTrail;
        private Coroutine trailRoutine;

        private void Awake()
        {
            playerMovement = GetComponent<PlayerMovement>();
            dashTrail = GetComponent<TrailRenderer>();

            dashTrail.time = 0.2f;
            dashTrail.minVertexDistance = 0.05f;
            dashTrail.widthMultiplier = 0.8f;
            dashTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            dashTrail.shadowCastingMode = ShadowCastingMode.Off;
            dashTrail.receiveShadows = false;
            dashTrail.emitting = false;
        }

        private void OnEnable()
        {
            playerMovement.DashStarted += PlayTrail;
        }

        private void OnDisable()
        {
            playerMovement.DashStarted -= PlayTrail;
            dashTrail.emitting = false;
        }

        private void PlayTrail(float duration)
        {
            if (trailRoutine != null)
            {
                StopCoroutine(trailRoutine);
            }

            trailRoutine = StartCoroutine(EmitTrail(duration));
        }

        private IEnumerator EmitTrail(float duration)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
            yield return new WaitForSeconds(duration);
            dashTrail.emitting = false;
            trailRoutine = null;
        }
    }
}
