using UnityEngine;

namespace PBS2D
{
    public class GroundDetection : MonoBehaviour
    {
        [SerializeField]
        private float _rayDistance = .4f;

        [SerializeField]
        private LayerMask _targetLayers;

        public bool IsGrounded()
        {
            return Physics2D.Raycast(transform.position, Vector2.down, _rayDistance, _targetLayers).collider != null;
        }
    }
}
