using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(ParticleSystem))]
    public class BloodCascade : MonoBehaviour
    {
        private const float FLOOR_OFFSET = 0.06f;
        private const float WALL_OFFSET = 0.025f;

        [SerializeField]
        private float _spawnProbability = .8f;

        private Character _character;
        private ParticleSystem _particleSystem;
        private readonly List<ParticleCollisionEvent> _collisionEvents = new();
        private float _bleedRate;

        void Awake()
        {
            _bleedRate = Random.Range(1f, 2f);
            _particleSystem = GetComponent<ParticleSystem>();

            var collisionModule = _particleSystem.collision;
            collisionModule.enabled = true;
            collisionModule.type = ParticleSystemCollisionType.World;
            collisionModule.mode = ParticleSystemCollisionMode.Collision2D;
            collisionModule.sendCollisionMessages = true;
        }

        public void Initialize(Character character)
        {
            _character = character;
            _character.Health.UpdateBleedRate(_bleedRate);
        }

        void Update()
        {
            if (_character != null)
            {
                if (_character.Health.Blood <= 0) _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void OnParticleCollision(GameObject other)
        {
            int count = _particleSystem.GetCollisionEvents(other, _collisionEvents);

            for (int i = 0; i < count; i++)
            {
                if (Random.value > _spawnProbability) continue;

                var collisionEvent = _collisionEvents[i];
                Vector2 hitPoint = collisionEvent.intersection;
                Vector2 normal = collisionEvent.normal;
                Vector2 cardinal = CardinalDirection(normal);

                bool isFloor = cardinal.x == 0;
                float offset = Random.Range(0f, isFloor ? FLOOR_OFFSET : WALL_OFFSET);
                Vector2 splatPosition = isFloor
                    ? new Vector2(hitPoint.x, hitPoint.y - offset)
                    : new Vector2(hitPoint.x + cardinal.x * offset, hitPoint.y);

                SplatManager.Instance.PlaceBloodSplat(splatPosition, isFloor);
            }
        }

        static Vector2 CardinalDirection(Vector2 normal)
        {
            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y))
                return normal.x > 0 ? Vector2.right : Vector2.left;
            else
                return normal.y > 0 ? Vector2.up : Vector2.down;
        }

        public IEnumerator LifeTimeCoroutine(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);

            if (_character != null && _character.Health != null)
                _character.Health.UpdateBleedRate(-_bleedRate);
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 1f);
        }
    }
}
