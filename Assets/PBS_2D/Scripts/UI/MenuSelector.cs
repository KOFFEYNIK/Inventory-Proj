using System;
using UnityEngine;
using UnityEngine.UI;

namespace PBS2D
{
    [RequireComponent(typeof(Button))]
    public class MenuSelector : MonoBehaviour
    {
        [SerializeField] private GameObject _settingsMenu;
        [SerializeField] private Color _selectedColor;

        private Color _startColor;
        private Button _button;

        public static event Action<MenuSelector> OnUnselectMenus;

        void Awake()
        {
            _button = GetComponent<Button>();

            _startColor = _button.colors.normalColor;
        }

        void OnEnable()
        {
            OnUnselectMenus += HandleUnselectRequest;
        }

        void OnDisable()
        {
            OnUnselectMenus -= HandleUnselectRequest;
        }

        public void SelectMenu()
        {
            OnUnselectMenus?.Invoke(this);

            _settingsMenu.SetActive(true);

            ColorBlock cb = _button.colors;
            cb.normalColor = _selectedColor;
            _button.colors = cb;
        }

        private void HandleUnselectRequest(MenuSelector sender)
        {
            if (sender != this)
            {
                UnselectMenu();
            }
        }

        private void UnselectMenu()
        {
            _settingsMenu.SetActive(false);
            ColorBlock cb = _button.colors;
            cb.normalColor = _startColor;
            _button.colors = cb;
        }
    }
}
