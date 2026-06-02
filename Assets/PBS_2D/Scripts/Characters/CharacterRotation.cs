using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class CharacterRotation : MonoBehaviour
    {
        private const float AIM_PIVOT_OFFSET = 3f;

        public Balance WeaponHolderBalance;

        [System.NonSerialized] public float HeadRotation;
        private Character _character;
        private float _targetRotation = 0;

        void Awake()
        {
            _character = GetComponent<Character>();
        }

        void Update()
        {
            if (!_character.IsConscious) return;

            UpdateFacingRight();
            FlipCharacterIfNeeded();
        }

        void LateUpdate()
        {
            if (!_character.IsConscious) return;

            LookTo();
        }

        private void LookTo()
        {
            if (!HasWeapon()) return;

            UpdateTargetRotation();

            HeadRotation = _targetRotation;

            if (!_character.IsAiming && (_character.IsRunning || _character.IsReloading))
            {
                // Gun rotation = 0
                // Head follows the aim target
                WeaponHolderBalance.TargetRotation = 0;
            }
            else
            {
                // Gun and head follow the aim target
                WeaponHolderBalance.TargetRotation = _targetRotation;
            }
        }

        private void UpdateTargetRotation()
        {
            if (_character.AimMode == AimMode.WorldPoint)
            {
                Vector2 aimPoint = _character.AimWorldPoint;

                Transform weapon = _character.WeaponManager.Weapon.transform;
                Gun gun = _character.WeaponManager.Gun;

                Vector2 weaponOffset =
                    (Vector2)weapon.localPosition +
                    (Vector2)gun.ShootingPoint.transform.localPosition;

                Vector2 rotatedOffset = weapon.rotation * weaponOffset;
                aimPoint -= rotatedOffset;

                Vector2 offset = new(-AIM_PIVOT_OFFSET, 0f);
                Vector2 weaponPivot = _character.WeaponManager.WeaponHolder.transform.TransformPoint(offset);
                Vector2 weaponDir = (aimPoint - weaponPivot).normalized;

                if (_character.IsRunning)
                {
                    Vector2 facingDir = _character.IsFacingRight ? Vector2.right : Vector2.left;
                    weaponDir = Vector2.Lerp(facingDir, weaponDir, 0.5f).normalized;
                }

                _targetRotation = Mathf.Atan2(weaponDir.y, weaponDir.x) * Mathf.Rad2Deg;

                if (!_character.IsFacingRight)
                {
                    _targetRotation += 180f;
                }
            }
            else if (_character.AimMode == AimMode.Direction)
            {
                _targetRotation = _character.AimDirection;

                if (!_character.IsFacingRight)
                {
                    _targetRotation += 180f;
                }
            }
        }

        private void UpdateFacingRight()
        {
            _character.IsFacingRight = _character.LowerTorso.Transform.localScale.x >= 0;
        }

        private void FlipCharacterIfNeeded()
        {
            float lowerTorsoX = _character.LowerTorso.Transform.position.x;

            if (_character.AimMode == AimMode.WorldPoint)
            {
                Vector2 targetPos = _character.AimWorldPoint;

                if ((targetPos.x < lowerTorsoX && _character.IsFacingRight) || (targetPos.x > lowerTorsoX && !_character.IsFacingRight))
                {
                    _character.BodyController.FlipCharacter();
                }
            }
            else if (_character.AimMode == AimMode.Direction)
            {
                float x = _character.AimDirectionInput.x;

                if ((x > 0 && !_character.IsFacingRight) || (x < 0 && _character.IsFacingRight))
                {
                    _character.BodyController.FlipCharacter();
                }
            }

            _character.IsFacingRight = _character.LowerTorso.Transform.localScale.x >= 0;
        }

        private bool HasWeapon() =>
            _character.WeaponManager != null &&
            _character.WeaponManager.Weapon != null &&
            _character.WeaponManager.Gun != null;
    }
}
