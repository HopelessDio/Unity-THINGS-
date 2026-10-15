using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Survivor.Player
{
    /// <summary>Humanoid locomotion only. Existing combat/dash overlays run afterward.</summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class GreatSwordLocomotion : MonoBehaviour
    {
        private GreatSwordMovementSet data;
        private Animator animator;
        private Rigidbody body;
        private PlayerMovement movement;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] clips;
        private AnimationClip[] motions;
        private readonly float[] weights=new float[5];
        private float phase;
        public bool IsReady => graph.IsValid();
        public float StandingWeight => IsReady ? mixer.GetInputWeight(0) : 0f;

        public void Initialize()
        {
            if(IsReady) return;
            data=Resources.Load<GreatSwordMovementSet>("GreatSwordMovement");
            animator=GetComponent<Animator>(); body=GetComponentInParent<Rigidbody>();
            if(data==null || animator==null || body==null || animator.avatar==null || !animator.avatar.isHuman) return;
            movement=body.GetComponent<PlayerMovement>();
            animator.runtimeAnimatorController=null; animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Great Sword Humanoid Movement"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            motions=new[]{data.idle,data.forward,data.backward,data.left,data.right};
            mixer=AnimationMixerPlayable.Create(graph,5); clips=new AnimationClipPlayable[5];
            for(int i=0;i<5;i++)
            {
                clips[i]=AnimationClipPlayable.Create(graph,motions[i]); clips[i].SetSpeed(0); clips[i].SetApplyFootIK(false);
                graph.Connect(clips[i],0,mixer,i);
            }
            var output=AnimationPlayableOutput.Create(graph,"Humanoid movement",animator); output.SetSourcePlayable(mixer);
            for(int i=0;i<5;i++) weights[i]=i==0?1:0;
            mixer.SetInputWeight(0,1); graph.Play(); graph.Evaluate(0);
            CalibrateWeapon();
        }

        private void CalibrateWeapon()
        {
            var gear=GetComponent<PlayerWeaponEquipment>();
            if(gear==null || gear.SwordSocket==null || gear.GunSocket==null) return;
            Vector3 blade=gear.SwordSocket.position-gear.GunSocket.position;
            if(blade.sqrMagnitude<.001f) return;
            blade.Normalize();
            Vector3 up=Mathf.Abs(Vector3.Dot(blade,Vector3.up))>.9f?body.transform.forward:Vector3.up;
            gear.SwordSocket.rotation=Quaternion.LookRotation(blade,up);
        }

        private void LateUpdate()
        {
            if(!Application.isPlaying) return;
            if(!IsReady) Initialize();
            if(!IsReady) return;
            Vector3 velocity=body.linearVelocity; velocity.y=0;
            bool dash=movement!=null && movement.IsDashing;
            float speed=velocity.magnitude;
            if(!dash)
            {
                Vector4 target=DirectionalWeights(body.transform.InverseTransformDirection(velocity));
                float moving=Mathf.Clamp01(speed/.7f), blend=1-Mathf.Exp(-Time.deltaTime/.085f);
                weights[0]=Mathf.Lerp(weights[0],1-moving,blend);
                for(int i=0;i<4;i++) weights[i+1]=Mathf.Lerp(weights[i+1],target[i]*moving,blend);
                float frequency=0;
                for(int i=0;i<4;i++) frequency+=target[i]*speed/Mathf.Max(.1f,data.strideDistances[i]);
                phase=Mathf.Repeat(phase+Time.deltaTime*frequency,1);
            }
            // Static standing base only; do not play the imported idle action.
            clips[0].SetTime(0);
            for(int i=0;i<4;i++) clips[i+1].SetTime(Mathf.Repeat(phase+data.stanceOffsets[i],1)*motions[i+1].length);
            for(int i=0;i<5;i++) mixer.SetInputWeight(i,weights[i]);
            graph.Evaluate(0);
        }
        public static Vector4 DirectionalWeights(Vector3 localVelocity)
        {
            float sum=Mathf.Abs(localVelocity.x)+Mathf.Abs(localVelocity.z);
            if(sum<.001f) return Vector4.zero;
            return new Vector4(Mathf.Max(0,localVelocity.z),Mathf.Max(0,-localVelocity.z),
                Mathf.Max(0,-localVelocity.x),Mathf.Max(0,localVelocity.x))/sum;
        }
        public void Sample(int clip,float normalizedTime)
        {
            Initialize(); if(!IsReady) return;
            for(int i=0;i<5;i++) mixer.SetInputWeight(i,i==clip?1:0);
            clips[clip].SetTime(clip==0 ? 0 : normalizedTime*motions[clip].length); graph.Evaluate(0);
            if(clip==0) GetComponent<VeilWalkerLocomotion>()?.ApplyStandingGuard(1);
        }
        private void OnDisable() { if(graph.IsValid()) graph.Destroy(); }
        private void OnDestroy() { if(graph.IsValid()) graph.Destroy(); }
    }
}
