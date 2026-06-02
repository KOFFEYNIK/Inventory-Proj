using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(TrailRenderer))]
    public class BulletVisual : MonoBehaviour
    {
        private TrailRenderer _trail;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
        }

        private void OnEnable()
        {
            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
                _trail.emitting = true;
            }
        }

        private void OnDisable()
        {
            if (_trail != null)
            {
                _trail.emitting = false;
                _trail.Clear();
            }
        }
    }
}
