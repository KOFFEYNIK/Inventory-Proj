using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace PBS2D
{
    [RequireComponent(typeof(SortingGroup))]
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class Weapon : Interactable
    {
        [Header("Weapon References")]
        [Tooltip("IK target position for the front hand")]
        public Transform FrontHandPoint;

        [Tooltip("IK target position for the back hand")]
        public Transform BackHandPoint;

        [Header("Weapon Settings")]
        [SerializeField, Tooltip("Display name of the weapon")]
        public string WeaponName = "Weapon";

        [Tooltip("Default position offset when weapon is idle")]
        public Vector2 IdlePosition;

        [Tooltip("Hold offset applied while running")]
        public WeaponHoldOffset RunOffset;

        [HandIndex]
        public int FrontHandIdx = 0;
        [HandIndex]
        public int BackHandIdx = 1;

        [System.NonSerialized] public Character character;
        [System.NonSerialized] public bool IsLocked = true;
        [System.NonSerialized] public bool IsDropped = true;
        [System.NonSerialized] public bool IsAttacking;

        public bool IsEquippedByPlayer => !IsDropped && character != null && character.IsPlayer;

        private static readonly WaitForSeconds _unlockDelay = new(.1f);
        protected SortingGroup _sortingGroup;
        protected Rigidbody2D _rigidbody;

        protected override void Awake()
        {
            base.Awake();
            _sortingGroup = GetComponent<SortingGroup>();
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        public override bool CanInteract()
        {
            return !Settings.LockActions && IsDropped;
        }

        public override void Interact(Character interactor)
        {
            if (Settings.LockActions || !IsDropped) return;

            interactor.WeaponManager.EquipWeapon(this);
        }

        public void Attack(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            // Start attack
            if (context.phase == InputActionPhase.Performed)
            {
                StartAttack();
            }
            // Stop attack
            else if (context.phase == InputActionPhase.Canceled)
            {
                StopAttack();
            }
        }

        public virtual void StartAttack() { }

        public virtual void StopAttack() { }

        private IEnumerator WaitToUnlock()
        {
            yield return _unlockDelay;
            IsLocked = false;
        }

        public virtual void Equip(Character character)
        {
            _sortingGroup.sortingLayerName = "Gun";
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.mass = .1f;
            transform.localScale = new Vector2(1, 1);

            IsLocked = true;
            this.character = character;
            character.CharacterSkin.ChangeHandSprite(true, FrontHandIdx);
            character.CharacterSkin.ChangeHandSprite(false, BackHandIdx);
            this.character.IsReloading = false;
            this.character.CharacterSkin.BackHandTargetSRenderer.sortingLayerName = "Outer Limb";
            this.character.CharacterRotation.WeaponHolderBalance.CurrentWeight = .25f;

            IsDropped = false;
            gameObject.layer = LayerMask.NameToLayer(character.IsPlayer ? "Weapon" : "Phasing");

            StartCoroutine(WaitToUnlock());
        }

        public virtual void Drop()
        {
            StopAllCoroutines();

            IsDropped = true;
            gameObject.layer = LayerMask.NameToLayer("Phasing");
            transform.parent = null;
            _rigidbody.mass = 1f;

            _sortingGroup.enabled = true;
        }

    }
}
