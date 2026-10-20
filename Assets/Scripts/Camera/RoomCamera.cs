using UnityEngine;

namespace Survivor.CameraSystem
{
    /// <summary>Stable room-wide view, with no player-follow bob or rotation.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class RoomCamera : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float verticalSpan = 13.7f;
        [SerializeField, Min(1f)] private float horizontalSpan = 23.2f;
        private Camera view;

        public void Configure(float width, float height)
        {
            horizontalSpan = width;
            verticalSpan = height;
            RefreshFraming();
        }

        private void OnEnable() => RefreshFraming();
        private void LateUpdate() => RefreshFraming();

        public void RefreshFraming()
        {
            if (view == null) view = GetComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = Mathf.Max(verticalSpan*.5f, horizontalSpan*.5f/Mathf.Max(.2f,view.aspect));
        }
    }
}
