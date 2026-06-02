using UnityEngine;

namespace PBS2D
{
    public class Torso : BodyPart
    {
        private const float TORSO_WEIGHT_MULTIPLIER = 1.25f;

        public override void TakeDamage(RaycastHit2D hit, float damage, float bulletForce)
        {
            Character.Health.TakeDamage(damage * _damageMultiplier, false);

            Vector2 forceDir = -hit.normal;

            _rb.AddForceAtPosition(TORSO_WEIGHT_MULTIPLIER * bulletForce * Settings.BulletForceMultiplier * forceDir, hit.point, ForceMode2D.Impulse);
        }
    }
}
