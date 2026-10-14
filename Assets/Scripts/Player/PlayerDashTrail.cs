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
        private ParticleSystem ash;

        public TrailRenderer DashTrail => dashTrail;

        private void Awake()
        {
            ConfigureVisuals();
        }

        public void ConfigureVisuals()
        {
            playerMovement = GetComponent<PlayerMovement>();
            dashTrail = GetComponent<TrailRenderer>();
            dashTrail.sharedMaterial = Resources.Load<Material>("DashIvoryStreak");
            dashTrail.time = .075f;
            dashTrail.minVertexDistance = 0.05f;
            dashTrail.widthMultiplier = .065f;
            dashTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            dashTrail.shadowCastingMode = ShadowCastingMode.Off;
            dashTrail.receiveShadows = false;
            dashTrail.emitting = false;
            dashTrail.alignment = LineAlignment.View;
            dashTrail.textureMode = LineTextureMode.Stretch;
            dashTrail.numCapVertices = 2;
            dashTrail.numCornerVertices = 2;
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(new Color(1f,.95f,.87f), 0f),
                new GradientColorKey(new Color(.8f,.78f,.72f), 1f)
            }, new[] { new GradientAlphaKey(.5f,0f), new GradientAlphaKey(0f,1f) });
            dashTrail.colorGradient = gradient;
            if (Application.IsPlaying(gameObject)) PrepareAsh();
        }

        public void PrepareAsh()
        {
            if (ash != null) return;
            GameObject cloud = new GameObject("DashAshPuffs");
            cloud.transform.SetParent(transform, false);
            cloud.layer = gameObject.layer;
            if (!Application.IsPlaying(gameObject))
                cloud.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            ash = cloud.AddComponent<ParticleSystem>();
            ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ash.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = .6f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .5f);
            main.startSpeed = 0f;
            var emission = ash.emission; emission.enabled = false;
            var shape = ash.shape; shape.enabled = false;
            var size = ash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f,.7f,1f,1.7f));
            var color = ash.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white,0f), new GradientColorKey(Color.white,1f) },
                new[] { new GradientAlphaKey(1f,0f), new GradientAlphaKey(0f,1f) });
            color.color = new ParticleSystem.MinMaxGradient(fade);
            var renderer = ash.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>("DashAsh");
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void EmitAshPuff(bool landing)
        {
            PrepareAsh();
            if (!ash.isPlaying) ash.Play(true);
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            Vector3 feet = transform.position;
            if (capsule != null) feet.y = capsule.bounds.min.y;
            // Raise billboard centres clear of the floor so the puff isn't
            // mostly clipped/hidden by the arena surface.
            feet.y += .22f;
            Vector3 heading = playerMovement.FacingDirection;
            if (heading.sqrMagnitude < .001f) heading = transform.forward;
            heading.Normalize();
            int count = landing ? 8 : 12;
            for (int i = 0; i < count; i++)
            {
                Vector2 scatter = Random.insideUnitCircle;
                var particle = new ParticleSystem.EmitParams
                {
                    position = feet + new Vector3(scatter.x,0f,scatter.y) * .22f,
                    velocity = new Vector3(scatter.x,.4f,scatter.y) * .75f - heading * (landing ? .15f : .7f),
                    startSize = Random.Range(.3f,.5f),
                    startLifetime = Random.Range(.32f,.5f),
                    startColor = new Color(.95f,.93f,.88f,landing ? .5f : .65f),
                    rotation = Random.Range(0f,360f)
                };
                ash.Emit(particle, 1);
            }
        }

        public void ClearAsh()
        {
            if (ash != null) ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public ParticleSystem AshParticles => ash;

        private void OnEnable()
        {
            playerMovement.DashStarted += PlayTrail;
        }

        private void OnDisable()
        {
            playerMovement.DashStarted -= PlayTrail;
            if (trailRoutine != null) StopCoroutine(trailRoutine);
            trailRoutine = null;
            dashTrail.emitting = false;
            dashTrail.Clear();
            ClearAsh();
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
            EmitAshPuff(false);
            dashTrail.emitting = true;
            yield return new WaitForSeconds(duration);
            dashTrail.emitting = false;
            EmitAshPuff(true);
            trailRoutine = null;
        }
    }
}
