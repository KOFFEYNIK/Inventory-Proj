using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PBS2D
{
    public class PauseMenu : Singleton<PauseMenu>
    {
        [SerializeField] private GameObject _pauseMenu;
        [SerializeField] private MenuSelector _initialMenu;

        private bool _isOpen;
        private bool _hasOpenedOnce;

        public static event Action OnMenuOpened;
        public static event Action OnResetValues;
        public static event Action OnApplyValues;

        protected override void Awake()
        {
            base.Awake();
        }

        void Start()
        {
            CloseMenu();
        }

        public void ToggleMenu()
        {
            if (_isOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        public void OpenMenu()
        {
            _isOpen = true;
            _pauseMenu.SetActive(true);

            Settings.LockActions = true;
            Time.timeScale = 0f;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);

            OnMenuOpened?.Invoke();

            if (!_hasOpenedOnce)
            {
                _hasOpenedOnce = true;
                _initialMenu.SelectMenu();
            }
        }

        public void CloseMenu()
        {
            _isOpen = false;
            _pauseMenu.SetActive(false);

            Settings.LockActions = false;
            Time.timeScale = Settings.SlowMotionActive ? Settings.SlowMotionEffect : 1f;
        }

        public void HandleReset()
        {
            OnResetValues?.Invoke();
        }

        public void HandleApply()
        {
            OnApplyValues?.Invoke();
        }
    }
}
