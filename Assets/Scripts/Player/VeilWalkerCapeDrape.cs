using UnityEngine;

namespace Survivor.Player
{
    /// <summary>Poses only the extra cape bones, after locomotion/combat/dash.</summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class VeilWalkerCapeDrape : MonoBehaviour
    {
        private readonly Transform[] bones=new Transform[3];
        private readonly Quaternion[] rest=new Quaternion[3];
        private readonly Vector3[] axes=new Vector3[3];
        private Transform player;
        private Transform[] bodyPoints;
        private Vector3 attachment;
        private bool ready;

        public void Initialize()
        {
            if(ready) return;
            var movement=GetComponentInParent<PlayerMovement>();
            var animator=GetComponent<Animator>();
            if(movement==null || animator==null || !animator.isHuman) return;
            player=movement.transform;
            string[] names={"mixamorig:CapeUpper","mixamorig:CapeMiddle","mixamorig:CapeLower"};
            foreach(var bone in GetComponentsInChildren<Transform>(true))
                for(int i=0;i<3;i++) if(bone.name==names[i]) bones[i]=bone;
            if(bones[0]==null || bones[1]==null || bones[2]==null) return;
            attachment=bones[0].localPosition;
            for(int i=0;i<3;i++)
            {
                rest[i]=bones[i].localRotation;
                Vector3 direction=i<2 ? bones[i+1].position-bones[i].position : bones[2].position-bones[1].position;
                axes[i]=bones[i].InverseTransformDirection(direction.normalized);
            }
            bodyPoints=new[]{animator.GetBoneTransform(HumanBodyBones.Hips),
                animator.GetBoneTransform(HumanBodyBones.Chest),
                animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),animator.GetBoneTransform(HumanBodyBones.RightLowerLeg)};
            ready=true;
        }

        private void LateUpdate() => ApplyPose();

        public void ApplyPose()
        {
            if(!ready) return;
            // Start from bind each frame, never the raised pose left by a dash.
            bones[0].localPosition=attachment;
            for(int i=0;i<3;i++) bones[i].localRotation=rest[i];
            Vector3 back=-player.forward;
            bones[0].position+=back*.08f;
            Vector3 anchor=bones[0].position;
            // A straight, slightly rearward drape. Increase clearance only when
            // a hip/knee is behind it; no flutter or changes to the body pose.
            float slope=.18f;
            foreach(var point in bodyPoints)
            {
                if(point==null) continue;
                float drop=anchor.y-point.position.y;
                if(drop<.15f || drop>1.05f) continue;
                float depth=Vector3.Dot(point.position-anchor,back)+.16f;
                slope=Mathf.Max(slope,depth/drop);
            }
            Vector3 down=(Vector3.down+back*Mathf.Clamp(slope,.18f,.65f)).normalized;
            for(int i=0;i<3;i++)
                bones[i].rotation=Quaternion.FromToRotation(bones[i].TransformDirection(axes[i]),down)*bones[i].rotation;
        }
    }
}
