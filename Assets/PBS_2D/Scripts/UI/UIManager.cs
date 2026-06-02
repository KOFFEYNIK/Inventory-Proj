using UnityEngine;
using UnityEngine.InputSystem;

namespace PBS2D
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private GameObject _mobileControls;
        [SerializeField] private GameObject _interactButton;
        [SerializeField] private GameObject _runButton;

        private GameControls _controls;
        private Character _player;
        private bool _showingTouchControls;

        void OnEnable()
        {
            _controls = new GameControls();
            _controls.UI.Enable();

            _controls.UI.TogglePauseMenu.performed += HandleTogglePauseMenu;
            _controls.UI.ClosePauseMenu.performed += HandleClosePauseMenu;

            PlayerInputHandler.OnKeyboardUsed += DisableTouchControlCanvas;
            PlayerInputHandler.OnGamepadUsed += DisableTouchControlCanvas;
            PlayerInputHandler.OnTouchUsed += EnableTouchControlCanvas;

            _player = PlayerManager.Player;
            PlayerManager.OnPlayerSpawned += HandlePlayerSpawned;
            PlayerManager.OnPlayerDespawned += HandlePlayerDespawned;
        }

        void OnDisable()
        {
            _controls.UI.TogglePauseMenu.performed -= HandleTogglePauseMenu;
            _controls.UI.ClosePauseMenu.performed -= HandleClosePauseMenu;

            PlayerInputHandler.OnKeyboardUsed -= DisableTouchControlCanvas;
            PlayerInputHandler.OnGamepadUsed -= DisableTouchControlCanvas;
            PlayerInputHandler.OnTouchUsed -= EnableTouchControlCanvas;

            PlayerManager.OnPlayerSpawned -= HandlePlayerSpawned;
            PlayerManager.OnPlayerDespawned -= HandlePlayerDespawned;

            _controls.UI.Disable();
        }

        private void HandlePlayerSpawned(Character player) => _player = player;
        private void HandlePlayerDespawned(Character player) => _player = null;

        private void HandleClosePauseMenu(InputAction.CallbackContext context)
        {
            PauseMenu.Instance.CloseMenu();
        }

        private void HandleTogglePauseMenu(InputAction.CallbackContext context)
        {
            PauseMenu.Instance.ToggleMenu();
        }

        void Update()
        {
            if (_player == null)
            {
                _interactButton.SetActive(false);
                _runButton.SetActive(false);
                return;
            }

            _interactButton.SetActive(_player.InteractionHandler.HasInteractable());

            bool isMovingForward = (_player.Movement.moveInput.x >= 0.95f && _player.IsFacingRight)
                                || (_player.Movement.moveInput.x <= -0.95f && !_player.IsFacingRight);
            _runButton.SetActive(isMovingForward && !_player.HeightController.IsCrouching);
        }

        public void EnableTouchControlCanvas()
        {
            if (_showingTouchControls) return;

            _mobileControls.SetActive(true);
            _showingTouchControls = true;
        }

        public void DisableTouchControlCanvas()
        {
            if (!_showingTouchControls) return;

            _mobileControls.SetActive(false);
            _showingTouchControls = false;
        }
    }
}
