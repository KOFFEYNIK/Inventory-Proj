using System.Collections;
using UnityEngine;

namespace PBS2D
{
    public class Casing : MonoBehaviour
    {
        [SerializeField, Range(0, 1f)]
        private float _volume = 1f;

        [SerializeField]
        private AudioClip _impactClip;

        private bool _hasPlayedSound;
        private SpriteRenderer _spriteRenderer;
        private ParticleSystem _particleSystem;
        private float _disableDelay;

        void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _particleSystem = GetComponent<ParticleSystem>();
            _disableDelay = _particleSystem.main.startLifetime.constant;
        }

        void OnEnable()
        {
            _spriteRenderer.enabled = true;
            _hasPlayedSound = false;
        }

        private void OnParticleCollision(GameObject other)
        {
            if (_hasPlayedSound)
                return;

            AudioManager.Instance.PlaySound(_impactClip, transform.position, _volume);
            _hasPlayedSound = true;
        }

        public void Play()
        {
            transform.SetParent(ObjectPoolManager.GetPoolParent(PoolType.Casing), worldPositionStays: true);
            _spriteRenderer.enabled = false;
            _particleSystem.Play();
            StartCoroutine(DisableAfterTime());
        }

        private IEnumerator DisableAfterTime()
        {
            yield return new WaitForSeconds(_disableDelay);

            ObjectPoolManager.ReturnObjectToPool(gameObject);
            StopAllCoroutines();
        }
    }
}
