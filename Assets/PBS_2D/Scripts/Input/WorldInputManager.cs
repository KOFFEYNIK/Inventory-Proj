using UnityEngine;
using UnityEngine.InputSystem;

namespace PBS2D
{
    public class WorldInputManager : MonoBehaviour
    {
        private GameControls _inputSystem;

        private void OnEnable()
        {
            _inputSystem = new GameControls();
            _inputSystem.World.Enable();

            _inputSystem.World.SlowMotion.performed += ToggleSlowMotion;
        }

        private void OnDisable()
        {
            if (_inputSystem == null) return;

            _inputSystem.World.SlowMotion.performed -= ToggleSlowMotion;
            _inputSystem.World.Disable();
            _inputSystem.Dispose();
            _inputSystem = null;
        }

        private void ToggleSlowMotion(InputAction.CallbackContext context)
        {
            Settings.SlowMotionActive = !Settings.SlowMotionActive;

            if (!Settings.LockActions)
            {
                Time.timeScale = Settings.SlowMotionActive ? Settings.SlowMotionEffect : 1f;
            }
        }
    }
}
