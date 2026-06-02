using UnityEngine;

namespace PBS2D
{
    public class FootAlignment : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Character _character;

        [Header("Alignment Settings")]
        [SerializeField] private float _rayLength = .25f;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _rotationSpeed = 20f;

        [Header("Raycast Offsets (Local Space)")]
        public Vector2 frontRayOffset = new(.1f, 0f);
        public Vector2 backRayOffset = new(-.1f, 0f);

        public bool Enabled { get; set; } = true;
        
        private void Update()
        {
            if (_character.IsConscious && _character.IsGrounded && Enabled)
                AlignFootToGround();
        }

        private void AlignFootToGround()
        {
            Vector2 footRight = transform.right;
            Vector2 frontOrigin = (Vector2)transform.position + footRight * frontRayOffset.x + (Vector2)transform.up * frontRayOffset.y;
            Vector2 backOrigin = (Vector2)transform.position + footRight * backRayOffset.x + (Vector2)transform.up * backRayOffset.y;

            RaycastHit2D frontHit = Physics2D.Raycast(frontOrigin, Vector2.down, _rayLength, _groundLayer);
            RaycastHit2D backHit = Physics2D.Raycast(backOrigin, Vector2.down, _rayLength, _groundLayer);

            bool hasFront = frontHit.collider != null;
            bool hasBack = backHit.collider != null;

            float t = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);

            if (hasFront || hasBack)
            {
                Vector2 normal = hasFront && hasBack
                    ? (frontHit.normal + backHit.normal).normalized
                    : hasFront ? frontHit.normal : backHit.normal;

                float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, 0f, angle), t);
            }
            else
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.identity, t);
            }
        }
    }
}
