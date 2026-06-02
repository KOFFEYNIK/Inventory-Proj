using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class CharacterMovement : MonoBehaviour
    {
        private const float CROUCH_SPEED_MULTIPLIER = 0.65f;
        private const float RUN_STICK_THRESHOLD = 0.9f;
        private const float AIR_CONTROL_MULTIPLIER = 0.3f;
        private const float KNEELING_OFFSET = 0.05f;
        private const float KNEELING_SMOOTHING = 0.1f;
        private const float JUMP_GROUND_CLEAR_DELAY = 0.1f;

        [Header("Move Settings")]
        public float WalkSpeed = 2.5f;

        public float RunSpeed = 5f;

        [SerializeField]
        private float _backwardSpeedMultiplier = .75f;

        [SerializeField]
        private float _runCooldownDuration = .5f;

        [SerializeField]
        private bool _allowAirControl = false;

        [Header("Jump Settings")]

        public float JumpForce = 40f;

        [SerializeField]
        private float _horizontalJumpMultiplier = 1.5f;

        [SerializeField]
        private float _jumpChargeDuration = 0.15f;

        [SerializeField]
        private float _jumpCrouchDepth = 0.6f;

        [SerializeField]
        private float _jumpCooldownDuration = .5f;
        
        [System.NonSerialized] public Vector2 moveInput;
        private Character _character;
        private Coroutine _runCooldownCoroutine;
        private readonly WaitForFixedUpdate _waitForFixedUpdate = new();
        private bool _isChargingJump = false;
        private bool _isJumpOnCooldown = false;
        private bool _canRun = true;
        private bool _jumpRequested = false;

        void Awake()
        {
            _character = GetComponent<Character>();
        }

        void FixedUpdate()
        {
            if (!_character.IsConscious) return;

            if (_character.HeightController.IsKneeling)
            {
                float midFeetX = (_character.BackFoot.Transform.position.x + _character.FrontFoot.Transform.position.x) / 2;
                float targetX = midFeetX + KNEELING_OFFSET;

                Vector2 currentPosition = _character.LowerTorso.Rb.position;
                Vector2 targetPosition = new(targetX, currentPosition.y);
                Vector2 newPos = Vector2.Lerp(currentPosition, targetPosition, KNEELING_SMOOTHING);

                _character.LowerTorso.Rb.MovePosition(newPos);

                return;
            }

            if (_jumpRequested)
            {
                ApplyJumpPhysics();
                _jumpRequested = false;
            }

            if (_character.IsGrounded)
            {
                _character.LowerTorso.Rb.linearVelocityX = moveInput.x * GetSpeed();
            }
            else if (_allowAirControl)
            {
                // Add air control as a force instead of overwriting velocity
                float airControlForce = moveInput.x * GetSpeed() * AIR_CONTROL_MULTIPLIER;
                _character.LowerTorso.Rb.AddForce(new Vector2(airControlForce, 0));
            }
        }

        private void ApplyJumpPhysics()
        {
            _character.IsJumping = true;
            _character.HandleUngrounded();

            float horizontalBoost = moveInput.x * GetSpeed() * _horizontalJumpMultiplier;

            Vector2 jumpImpulse = new(horizontalBoost, JumpForce);

            _character.LowerTorso.Rb.AddForce(jumpImpulse, ForceMode2D.Impulse);
        }


        public void Run(InputAction.CallbackContext context)
        {
            if ((moveInput.x <= 0 && _character.IsFacingRight) || (moveInput.x >= 0 && !_character.IsFacingRight)) return;

            if (_character.IsAiming || _character.HeightController.IsCrouching) return;

            // Start running
            if (context.phase == InputActionPhase.Started)
            {
                if (_canRun)
                {
                    StartRunning();
                }
            }
            // Stop running
            else if (context.phase == InputActionPhase.Canceled)
            {
                if (_runCooldownCoroutine == null && _character.IsRunning)
                {
                    StopRunning();
                }
            }
        }

        public void Move(float value)
        {
            if (_character.IsRunning)
            {
                if (_character.IsFacingRight)
                {
                    if (value < RUN_STICK_THRESHOLD) StopRunning();
                }
                else
                {
                    if (value > -RUN_STICK_THRESHOLD) StopRunning();
                }
            }

            moveInput = new(value, 0);
        }

        public void Jump()
        {
            IEnumerator JumpSequence()
            {
                yield return StartCoroutine(ChargeJump());

                _jumpRequested = true;

                // Wait a minimum time to clear the ground, then wait until
                // the character starts falling so IsJumping adapts to any jump height
                yield return new WaitForSeconds(JUMP_GROUND_CLEAR_DELAY);
                yield return new WaitUntil(() => _character.LowerTorso.Rb.linearVelocity.y <= 0);
                _character.IsJumping = false;
            }

            if (CanJump())
            {
                StartCoroutine(JumpSequence());
            }
        }

        private bool CanJump()
        {
            return !_character.HeightController.IsCrouching && !_isJumpOnCooldown && _character.IsGrounded && !_isChargingJump;
        }

        public void StartRunning()
        {
            _character.IsRunning = true;

            _character.WeaponManager.HandleWeaponRunState();
        }

        public void StopRunning()
        {
            _character.IsRunning = false;

            _character.LegsController.PlaceLegsAfterRun();
            if (_character.IsReloading)
                _character.WeaponManager.HandleGunReloadState();
            else
                _character.WeaponManager.HandleWeaponIdleState(.15f);
            _runCooldownCoroutine = StartCoroutine(RunCooldown());
        }

        private IEnumerator ChargeJump()
        {
            _isChargingJump = true;
            _character.HeightController.TemporaryChangeHeight(_jumpCrouchDepth, _jumpChargeDuration);

            float elapsed = 0;
            while (elapsed < _jumpChargeDuration)
            {
                elapsed += Time.fixedDeltaTime;
                yield return _waitForFixedUpdate;
            }

            _isChargingJump = false;
        }

        private float GetSpeed()
        {
            if (_character.IsRunning)
            {
                return RunSpeed;
            }
            else if ((moveInput.x < 0 && _character.IsFacingRight) || (moveInput.x > 0 && !_character.IsFacingRight))
            {
                if (_character.HeightController.IsCrouching)
                    return WalkSpeed * _backwardSpeedMultiplier * CROUCH_SPEED_MULTIPLIER;
                else
                    return WalkSpeed * _backwardSpeedMultiplier;
            }
            else
            {
                if (_character.HeightController.IsCrouching)
                    return WalkSpeed * CROUCH_SPEED_MULTIPLIER;
                else
                    return WalkSpeed;
            }
        }

        private IEnumerator RunCooldown()
        {
            _canRun = false;
            yield return new WaitForSeconds(_runCooldownDuration);
            _canRun = true;
            _runCooldownCoroutine = null;
        }

        public IEnumerator JumpCooldown()
        {
            _isJumpOnCooldown = true;
            yield return new WaitForSeconds(_jumpCooldownDuration);
            _isJumpOnCooldown = false;
        }
    }
}
