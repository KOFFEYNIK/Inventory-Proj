using UnityEngine;

namespace PBS2D
{
    public class Killbox : MonoBehaviour
    {
        [SerializeField]
        private LayerMask _targetLayers;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (((1 << other.gameObject.layer) & _targetLayers) == 0) return;

            if (other.TryGetComponent(out Torso torso))
                torso.Character.Die(true);
        }
    }
}
