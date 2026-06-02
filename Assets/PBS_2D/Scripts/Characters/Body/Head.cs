using UnityEngine;

namespace PBS2D
{
    public class Head : BodyPart
    {
        [SerializeField]
        private float _offsetReductionFactor = .7f;
        
        [SerializeField]
        private float _offsetReductionDuration = 0.4f;

        public override void TakeDamage(RaycastHit2D hit, float damage, float bulletForce)
        {
            Vector2 forceDir = -hit.normal;
            _rb.AddForceAtPosition(bulletForce * Settings.BulletForceMultiplier * forceDir, hit.point, ForceMode2D.Impulse);

            if (Character.IsDead) return;

            Character.Health.TakeDamage(damage * _damageMultiplier, true);

            Character.HeightController.TemporaryChangeHeight(_offsetReductionFactor, _offsetReductionDuration);
        }
    }
}