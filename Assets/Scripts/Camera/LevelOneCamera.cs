using UnityEngine;

namespace Survivor.CameraSystem
{
    /// <summary>Preserves the approved right-shifted intro framing as the player travels.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(110)]
    public sealed class LevelOneCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform backdrop;
        [SerializeField] private Vector3 startingCameraPosition;
        [SerializeField] private Vector3 startingTargetPosition;
        [SerializeField] private float referenceWidth=62f;
        [SerializeField] private float smoothTime=.15f;
        private Vector3 followVelocity;
        private Camera view;

        public void Configure(Transform player,Transform picture,Vector3 cameraPosition,Quaternion rotation,float width)
        {
            target=player; backdrop=picture; startingCameraPosition=cameraPosition;
            startingTargetPosition=player.position; referenceWidth=width;
            transform.SetPositionAndRotation(cameraPosition,rotation); followVelocity=Vector3.zero;
            Resize();
        }
        private void OnEnable() { followVelocity=Vector3.zero; Resize(); }
        public void RefreshFraming() => Resize();
        private void LateUpdate()
        {
            Resize();
            if(!Application.isPlaying || target==null) return;
            Vector3 displacement=target.position-startingTargetPosition; displacement.y=0;
            transform.position=Vector3.SmoothDamp(transform.position,startingCameraPosition+displacement,
                ref followVelocity,smoothTime);
        }
        private void Resize()
        {
            if(view==null) view=GetComponent<Camera>();
            if(view==null) return;
            view.orthographic=true;
            view.orthographicSize=referenceWidth*.5f/Mathf.Max(.2f,view.aspect);
            if(backdrop!=null)
            {
                backdrop.localPosition=new Vector3(0,0,600);
                backdrop.localRotation=Quaternion.identity;
                backdrop.localScale=new Vector3(referenceWidth*1.01f,view.orthographicSize*2.02f,1);
            }
        }
    }
}
