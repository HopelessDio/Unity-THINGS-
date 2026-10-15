using UnityEngine;

namespace Survivor.Player
{
    public sealed class GreatSwordMovementSet : ScriptableObject
    {
        public AnimationClip idle,forward,backward,left,right;
        // Distance travelled in one source cycle, used to match foot cadence to velocity.
        public Vector4 strideDistances=new Vector4(2.6034f,1.9208f,1.3494f,1.9855f);
        public Vector4 stanceOffsets;
    }
}
