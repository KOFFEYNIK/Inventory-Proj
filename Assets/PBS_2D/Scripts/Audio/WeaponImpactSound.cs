using UnityEngine;

namespace PBS2D
{
    public class WeaponImpactSound : ImpactSound
    {
        [SerializeField] private Weapon _weapon;

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_weapon.IsDropped)
                return;

            base.OnCollisionEnter2D(collision);
        }
    }
}
