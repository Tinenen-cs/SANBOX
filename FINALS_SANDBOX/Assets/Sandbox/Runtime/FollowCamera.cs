using UnityEngine;

namespace Mossmere
{
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        Vector3 velocity;
        void LateUpdate()
        {
            if (target == null) return;
            Vector3 point = new(target.position.x, target.position.y, -10);
            transform.position = Vector3.SmoothDamp(transform.position, point, ref velocity, .16f);
        }
        public void Snap()
        {
            velocity = Vector3.zero;
            transform.position = new Vector3(target.position.x, target.position.y, -10);
            var camera = GetComponent<Camera>();
            camera.cullingMask = ~0;
            camera.orthographic = true;
            camera.orthographicSize = 6.75f;
        }
    }
}
