using UnityEngine;

namespace PBS2D
{
    public class ImpactSound : MonoBehaviour
    {
        [SerializeField]
        private AudioClip[] _impactClips = new AudioClip[1];

        [SerializeField]
        private LayerMask _targetLayers = -1;

        [Header("Settings")]
        [SerializeField, Min(0)]
        private float _volumeMultiplier = 1f;

        [SerializeField, Min(0), Tooltip("Minimum velocity to produce a sound")]
        protected float _minImpactSpeed = 1f;

        [SerializeField, Min(0), Tooltip("Velocity at which the volume is maximum")]
        protected float _maxImpactSpeed = 15f;

        [SerializeField, Min(0)]
        protected float _cooldown = 0.2f;

        private float _nextPlayTime;

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (Time.time < _nextPlayTime)
                return;

            if (((1 << collision.gameObject.layer) & _targetLayers) == 0)
                return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < _minImpactSpeed)
                return;

            PlayImpact(impactSpeed);
            UpdateCooldown();
        }

        protected virtual void PlayImpact(float impactSpeed)
        {
            float volume = Mathf.InverseLerp(_minImpactSpeed, _maxImpactSpeed, impactSpeed) * _volumeMultiplier;

            AudioClip impactClip = _impactClips[Random.Range(0, _impactClips.Length)];

            AudioManager.Instance.PlaySound(impactClip, transform.position, volume);
        }

        protected virtual void UpdateCooldown()
        {
            _nextPlayTime = Time.time + _cooldown;
        }
    }
}
