using System.Collections.Generic;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(ParticleSystem))]
    public class BloodSplat : MonoBehaviour
    {
        private const float FLOOR_OFFSET = -0.05f; // default for hit on floors, flipped for ceilings
        private const float WALL_OFFSET = -0.05f; // default for right hit on walls, flipped for left

        [SerializeField]
        private float _spawnProbability = .8f;

        private ParticleSystem _particleSystem;
        private readonly List<ParticleCollisionEvent> _collisionEvents = new();

        void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();

            var col = _particleSystem.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision2D;
            col.sendCollisionMessages = true;
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
                float sign = isFloor
                    ? (cardinal.y > 0 ? -1f : 1f)   // flip for ceiling
                    : (cardinal.x < 0 ? -1f : 1f);  // flip for left wall
                float offset = Random.Range(0f, isFloor ? FLOOR_OFFSET : WALL_OFFSET) * sign;
                Vector2 splatPosition = isFloor
                    ? new Vector2(hitPoint.x, hitPoint.y - offset)
                    : new Vector2(hitPoint.x + offset, hitPoint.y);

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
    }
}
