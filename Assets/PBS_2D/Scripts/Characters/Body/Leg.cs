using UnityEngine;

namespace PBS2D
{
    public class Leg : BodyPart
    {
        [SerializeField]
        private float _offsetReductionFactor = .8f;
        
        [SerializeField]
        private float _offsetReductionDuration = .2f;

        public override void TakeDamage(RaycastHit2D hit, float damage, float bulletForce)
        {
            Vector2 forceDir = -hit.normal;
            if (!Character.IsConscious)
            {
                _rb.AddForceAtPosition(bulletForce * Settings.BulletForceMultiplier * forceDir, hit.point, ForceMode2D.Impulse);
                return;
            }

            Character.Health.TakeDamage(damage * _damageMultiplier, false);

            Character.LowerTorso.Rb.AddForce(forceDir * bulletForce, ForceMode2D.Impulse);

            Character.HeightController.TemporaryChangeHeight(_offsetReductionFactor, _offsetReductionDuration);
        }
    }
}
