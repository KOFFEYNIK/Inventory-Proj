using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class WeaponManager : MonoBehaviour
    {
        [Header("References")]
        public GameObject WeaponHolder;

        [Header("Settings")]
        [NoDefaultPose, SerializeField]
        private WeaponHoldOffset _defaultRunOffset;

        [NoDefaultPose, SerializeField]
        private WeaponHoldOffset _defaultAimDownOffset;

        [NoDefaultPose, SerializeField]
        private WeaponHoldOffset _defaultReloadOffset;

        private const float DEFAULT_CORRECTION = 0.1f;
        private const float AIM_CORRECTION = 0.25f;
        private const float IDLE_CORRECTION = 0.15f;
        private const float RUN_CORRECTION = 0.15f;
        private const float CORRECTION_DURATION = 0.1f;
        private const float DROP_WEAPON_MASS = 1f;

        private Character _character;
        [System.NonSerialized] public Weapon Weapon;
        [System.NonSerialized] public Gun Gun;
        private Rigidbody2D _weaponRB;
        private Coroutine _weaponCorrectionCoroutine, _dropWeaponCoroutine;
        private RelativeJoint2D _weaponHolderJoint;

        void Awake()
        {
            _character = GetComponent<Character>();

            _weaponHolderJoint = WeaponHolder.GetComponent<RelativeJoint2D>();
            InitWeaponFromChild();
        }

        void Start()
        {
            if (IsHoldingWeapon)
                EquipWeapon(Weapon);
        }

        public bool IsHoldingWeapon => Weapon != null;

        public bool IsHoldingGun => Gun != null;

        public void Aim(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Started) // Start aiming
            {
                if (!_character.IsAiming)
                {
                    if (_character.IsRunning)
                    {
                        _character.Movement.StopRunning();
                    }

                    HandleGunAimState(AIM_CORRECTION);
                    _character.IsAiming = true;
                }
            }
            else if (context.phase == InputActionPhase.Canceled) // Stop aiming
            {
                if (_character.IsAiming)
                {
                    if (_character.IsReloading)
                        HandleGunReloadState();
                    else
                        HandleWeaponIdleState(AIM_CORRECTION);
                }

                _character.IsAiming = false;
            }
        }

        public void EquipWeapon(Weapon weapon)
        {
            if (IsHoldingWeapon)
                DropWeapon();

            if (_character.IsPlayer)
            {
                WeaponUI.Instance.Gun = weapon.GetComponent<Gun>();
                WeaponUI.Instance.UpdateAmmoUI();
                WeaponUI.Instance.UpdateFireModeIcon();
            }

            Weapon = weapon;
            _weaponRB = weapon.GetComponent<Rigidbody2D>();

            Gun = weapon.GetComponent<Gun>();

            weapon.transform.SetParent(WeaponHolder.transform);

            _weaponHolderJoint.enabled = true;
            _weaponHolderJoint.connectedBody = _weaponRB;

            _character.BodyController.RagdollArms(false);

            _character.FrontHandIKTarget.transform.rotation = weapon.transform.rotation;
            _character.FrontHandIKTarget.transform.SetParent(weapon.transform);
            _character.FrontHandIKTarget.transform.localPosition = Gun.FrontHandPoint.transform.localPosition;
            _character.FrontHandIKTarget.transform.localScale = new(1, 1);

            _character.BackHandIKTarget.transform.rotation = weapon.transform.rotation;
            _character.BackHandIKTarget.transform.SetParent(weapon.transform);
            _character.BackHandIKTarget.transform.localPosition = Gun.BackHandPoint.transform.localPosition;
            _character.BackHandIKTarget.transform.localScale = new(1, 1);

            weapon.Equip(_character);

            weapon.transform.localPosition = weapon.IdlePosition;
            weapon.transform.rotation = WeaponHolder.transform.rotation;

            if (_character.IsRunning)
                HandleWeaponRunState();
            else if (_character.IsAiming)
                HandleGunAimState(AIM_CORRECTION);
            else if (_character.IsReloading)
                HandleGunReloadState();
            else
                HandleWeaponIdleState(IDLE_CORRECTION);
        }

        public void DropWeapon()
        {
            if (!IsHoldingWeapon) return;

            if (_dropWeaponCoroutine != null)
            {
                StopCoroutine(_dropWeaponCoroutine);
                _dropWeaponCoroutine = null;
            }

            _character.BodyController.RagdollArms(true);
            _character.CharacterSkin.DefaultHands();

            _character.FrontHandFixedJoint.enabled = false;
            _character.BackHandFixedJoint.enabled = false;
            _weaponHolderJoint.enabled = false;

            _character.FrontHandIKTarget.transform.SetParent(_character.IKTargets.transform);
            _character.BackHandIKTarget.transform.SetParent(_character.IKTargets.transform);

            Weapon.Drop();

            Weapon = Gun = null;
            _character.IsCycling = false;
        }

        public void DropWeapon(float delay)
        {
            IEnumerator DropWeaponCoroutine()
            {
                _character.BodyController.RagdollArms(true);
                _character.CharacterSkin.DefaultBackHand();

                _character.FrontHandFixedJoint.connectedBody = _weaponRB;
                _character.FrontHandFixedJoint.enabled = true;

                _weaponHolderJoint.enabled = false;
                _character.FrontHandIKTarget.transform.SetParent(_character.LowerFrontArm.Transform);
                _character.FrontHandIKTarget.transform.localPosition = _character.FrontHandFixedJoint.anchor;
                _weaponRB.mass = DROP_WEAPON_MASS;

                yield return new WaitForSeconds(delay);

                _character.FrontHandFixedJoint.enabled = false;

                DropWeapon();
            }

            if (IsHoldingWeapon)
            {
                if (_dropWeaponCoroutine != null)
                    StopCoroutine(_dropWeaponCoroutine);

                _dropWeaponCoroutine = StartCoroutine(DropWeaponCoroutine());
            }
        }

        public void SwitchGunFireMode()
        {
            if (!IsHoldingGun) return;

            Gun.SwitchFireMode();
            if (_character.IsPlayer) WeaponUI.Instance.UpdateFireModeIcon();
        }

        public void InvertWeaponPositionAndRotation()
        {
            _weaponHolderJoint.linearOffset = new(-_weaponHolderJoint.linearOffset.x, _weaponHolderJoint.linearOffset.y);
            _weaponHolderJoint.angularOffset = -_weaponHolderJoint.angularOffset;
        }

        public void HandleWeaponIdleState(float temporaryCorrection)
        {
            if (Weapon == null) return;
            ApplyWeaponState(new WeaponHoldOffset(), new WeaponHoldOffset(), false, temporaryCorrection);
        }

        public void HandleWeaponRunState()
        {
            if (Weapon == null) return;
            ApplyWeaponState(Weapon.RunOffset, _defaultRunOffset, false, RUN_CORRECTION);
        }

        public void HandleGunAimState(float temporaryCorrection)
        {
            if (Gun == null) return;
            ApplyWeaponState(Gun.AimDownOffset, _defaultAimDownOffset, true, temporaryCorrection);
        }

        public void HandleGunReloadState()
        {
            if (Gun == null) return;
            ApplyWeaponState(Gun.ReloadOffset, _defaultReloadOffset, false);
        }

        public void ChangeWeaponPosition(Vector2 newPosition)
        {
            if (_character.IsFacingRight)
                _weaponHolderJoint.linearOffset = newPosition;
            else
                _weaponHolderJoint.linearOffset = new(-newPosition.x, newPosition.y);
        }

        public void ChangeWeaponRotation(float newRotation)
        {
            if (_character.IsFacingRight)
                _weaponHolderJoint.angularOffset = newRotation;
            else
                _weaponHolderJoint.angularOffset = -newRotation;
        }

        private void InitWeaponFromChild()
        {
            if (WeaponHolder.transform.childCount != 0)
            {
                GameObject weaponObj = WeaponHolder.transform.GetChild(0).gameObject;
                Weapon = weaponObj.GetComponent<Weapon>();
                _weaponRB = weaponObj.GetComponent<Rigidbody2D>();
            }
        }

        private IEnumerator TemporarilyChangeWeaponCorrection(float temporaryCorrection)
        {
            ChangeWeaponCorrection(temporaryCorrection);
            yield return new WaitForSeconds(CORRECTION_DURATION);
            ChangeWeaponCorrection(DEFAULT_CORRECTION);
        }

        private void ChangeWeaponCorrection(float newCorrection)
        {
            _weaponHolderJoint.correctionScale = newCorrection;
        }

        private void ApplyWeaponState(WeaponHoldOffset offset, WeaponHoldOffset defaultOffset,
            bool isAiming, float? temporaryCorrection = null)
        {
            if (temporaryCorrection.HasValue)
            {
                if (_weaponCorrectionCoroutine != null) StopCoroutine(_weaponCorrectionCoroutine);
                _weaponCorrectionCoroutine = StartCoroutine(TemporarilyChangeWeaponCorrection(temporaryCorrection.Value));
            }

            _character.CharacterSkin.SetHeadSprite(isAiming ? _character.Skin.Head1 : _character.Skin.Head0);

            _character.RestHinge.enabled = !isAiming;
            _character.AimHinge.enabled = isAiming;

            Vector2 weaponWorldPos = Weapon.transform.position;
            WeaponHolder.transform.localPosition = isAiming ? _character.HingeAimDownInit : _character.HingeNormalInit;
            Weapon.transform.position = weaponWorldPos;

            if (offset.UseDefaultPose)
                offset = defaultOffset;

            Vector2 finalPosition = offset.PositionOffset + Weapon.IdlePosition;
            if (isAiming)
            {
                finalPosition.x += _character.HingeNormalInit.x - _character.HingeAimDownInit.x;
                finalPosition.y += _character.HingeNormalInit.y - _character.HingeAimDownInit.y;
            }

            ChangeWeaponPosition(finalPosition);
            ChangeWeaponRotation(offset.Rotation);
        }
    }
}
