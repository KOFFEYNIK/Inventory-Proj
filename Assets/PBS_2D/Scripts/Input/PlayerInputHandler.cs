using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;


namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class PlayerInputHandler : MonoBehaviour
    {
        private const float SMOOTH_SPEED = 10f;
        private const float DEADZONE = 0.1f;

        private Character _character;
        private GameControls controls;

        private Camera cam;

        public static event Action OnKeyboardUsed;
        public static event Action OnGamepadUsed;
        public static event Action OnTouchUsed;

        private Vector2 smoothInput;
        private Vector2 rawStickInput;

        private float lastTouchTime;
        private const float TOUCH_GRACE_SECONDS = 0.25f;

        private readonly System.Collections.Generic.List<RaycastResult> uiRaycastResults = new();

        #region MAIN

        void Awake()
        {
            _character = GetComponent<Character>();
            cam = Camera.main;
        }

        private void OnEnable()
        {
            controls = new GameControls();
            controls.Player.Enable();
            EnhancedTouchSupport.Enable();

            // Player Actions
            controls.Player.LookMouse.performed += LookMouse;
            controls.Player.LookStick.performed += LookStick;
            controls.Player.LookTouch.performed += LookTouch;
            controls.Player.Move.performed += Move;
            controls.Player.Move.canceled += Move;
            controls.Player.Jump.performed += Jump;
            controls.Player.Crouch.started += Crouch;
            controls.Player.Crouch.canceled += Crouch;
            controls.Player.Run.started += Run;
            controls.Player.Run.canceled += Run;
            controls.Player.Attack.performed += Attack;
            controls.Player.Attack.canceled += Attack;
            controls.Player.Aim.started += Aim;
            controls.Player.Aim.canceled += Aim;

            controls.Player.Reload.performed += Reload;
            controls.Player.SwitchFireMode.performed += SwitchFireMode;

            controls.Player.Interact.performed += Interact;

            foreach (InputAction action in controls.Player.Get())
            {
                action.performed += UpdateCurrentDevice;
            }
        }

        private void OnDisable()
        {
            if (controls == null) return;

            // Player Actions
            controls.Player.LookMouse.performed -= LookMouse;
            controls.Player.LookStick.performed -= LookStick;
            controls.Player.LookTouch.performed -= LookTouch;
            controls.Player.Move.performed -= Move;
            controls.Player.Move.canceled -= Move;
            controls.Player.Jump.performed -= Jump;
            controls.Player.Crouch.started -= Crouch;
            controls.Player.Crouch.canceled -= Crouch;
            controls.Player.Run.started -= Run;
            controls.Player.Run.canceled -= Run;
            controls.Player.Attack.performed -= Attack;
            controls.Player.Attack.canceled -= Attack;
            controls.Player.Aim.started -= Aim;
            controls.Player.Aim.canceled -= Aim;

            controls.Player.Reload.performed -= Reload;
            controls.Player.SwitchFireMode.performed -= SwitchFireMode;

            controls.Player.Interact.performed -= Interact;

            foreach (InputAction action in controls.Player.Get())
            {
                action.performed -= UpdateCurrentDevice;
            }

            controls.Player.Disable();
            EnhancedTouchSupport.Disable();
            controls.Dispose();
            controls = null;
        }

        void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].isInProgress)
                    {
                        lastTouchTime = Time.unscaledTime;
                        break;
                    }
                }
            }

            PlayerInfo.mousePos = cam.ScreenToWorldPoint(PlayerInfo.mouseScreenPos);

            // Smooth stick input per-frame for consistent smoothing
            if (PlayerInfo.aimMode == AimMode.Direction)
            {
                smoothInput = Vector2.Lerp(smoothInput, rawStickInput, Time.deltaTime * SMOOTH_SPEED);
                PlayerInfo.controllerInput = smoothInput;
                PlayerInfo.controllerDirection =
                    Mathf.Atan2(smoothInput.y, smoothInput.x) * Mathf.Rad2Deg;
            }

            // Mirror player aim onto the character so CharacterRotation is source-agnostic
            _character.AimMode = PlayerInfo.aimMode;
            _character.AimWorldPoint = PlayerInfo.mousePos;
            _character.AimDirectionInput = PlayerInfo.controllerInput;
            _character.AimDirection = PlayerInfo.controllerDirection;
        }

        private void UpdateCurrentDevice(InputAction.CallbackContext context)
        {
            InputDevice newDevice = context.control.device;

            // Mouse does the same as Keyboard
            if (newDevice is Mouse)
            {
                newDevice = Keyboard.current;
            }

            if (newDevice != PlayerInfo.currentDevice)
            {
                if (newDevice is Keyboard)
                {
                    OnKeyboardUsed?.Invoke();
                }
                else if (newDevice is Gamepad)
                {
                    if (Time.unscaledTime - lastTouchTime < TOUCH_GRACE_SECONDS)
                        return;

                    OnGamepadUsed?.Invoke();
                }
                else if (newDevice is Touchscreen)
                {
                    OnTouchUsed?.Invoke();
                }
                else
                {
                    Debug.LogWarning("Unknown device used");
                }

                PlayerInfo.currentDevice = newDevice;
            }
        }

        private bool IsValidScreenPosition(Vector2 screenPos)
        {
            return float.IsFinite(screenPos.x) &&
                   float.IsFinite(screenPos.y) &&
                   screenPos.x >= 0 &&
                   screenPos.x <= Screen.width &&
                   screenPos.y >= 0 &&
                   screenPos.y <= Screen.height;
        }

        #endregion

        #region METHODS

        private void LookMouse(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (context.control.device is not Mouse) return;

            Vector2 screenPos = context.ReadValue<Vector2>();

            if (IsValidScreenPosition(screenPos))
            {
                PlayerInfo.aimMode = AimMode.WorldPoint;

                PlayerInfo.mouseScreenPos = screenPos;
            }
        }

        private void LookStick(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (context.control.device is not Gamepad) return;

            Vector2 raw = context.ReadValue<Vector2>();

            if (raw.magnitude < DEADZONE)
            {
                raw = Vector2.zero;
            }
            else
            {
                float scaled = (raw.magnitude - DEADZONE) / (1f - DEADZONE);
                raw = raw.normalized * scaled;
            }

            rawStickInput = raw;
            PlayerInfo.aimMode = AimMode.Direction;
        }


        private void LookTouch(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (context.control.device is not Touchscreen) return;

            var touchControl = context.control.parent as UnityEngine.InputSystem.Controls.TouchControl;
            if (touchControl != null)
            {
                int touchId = touchControl.touchId.ReadValue();

                if (MobileControls.IsTouchClaimedByStick(touchId))
                    return;
            }

            Vector2 screenPos = context.ReadValue<Vector2>();

            if (IsTouchOverUI(screenPos) || !IsValidScreenPosition(screenPos))
                return;

            PlayerInfo.aimMode = AimMode.WorldPoint;
            PlayerInfo.mouseScreenPos = screenPos;
        }

        private bool IsTouchOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null)
                return false;

            var data = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };

            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(data, uiRaycastResults);

            return uiRaycastResults.Count > 0;
        }

        private void Move(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            Vector2 moveInput = context.ReadValue<Vector2>();
            _character.Movement.Move(moveInput.x);
        }

        private void Jump(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            _character.Movement.Jump();
        }

        private void Crouch(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (!_character.IsGrounded) return;

            if (!Settings.HoldDownCrouch && context.phase == InputActionPhase.Canceled) return;

            _character.HeightController.Crouch();
        }

        private void Attack(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (_character.WeaponManager.IsHoldingWeapon)
                _character.WeaponManager.Weapon.Attack(context);
        }

        private void Run(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            _character.Movement.Run(context);
        }

        private void Aim(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (_character.WeaponManager.IsHoldingGun)
                _character.WeaponManager.Aim(context);
        }

        private void Reload(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (_character.WeaponManager.IsHoldingGun)
            {
                _character.WeaponManager.Gun.ReloadGun();
            }
        }

        private void SwitchFireMode(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            if (_character.WeaponManager.IsHoldingGun)
            {
                _character.WeaponManager.SwitchGunFireMode();
            }
        }

        private void Interact(InputAction.CallbackContext context)
        {
            if (Settings.LockActions) return;

            _character.InteractionHandler.InteractWithClosest();
        }

        #endregion
    }

    public static class PlayerInfo
    {
        public static AimMode aimMode;
        public static InputDevice currentDevice;
        public static Vector2 mouseScreenPos = Vector2.zero;
        public static Vector2 mousePos = Vector2.zero;
        public static Vector2 controllerInput = Vector2.zero;
        public static float controllerDirection = 0;
    }

    public enum AimMode
    {
        WorldPoint, Direction
    }
}
