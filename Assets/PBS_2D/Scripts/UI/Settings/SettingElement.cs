using System;
using TMPro;
using UnityEngine;

namespace PBS2D
{
    public abstract class SettingElement<T> : MonoBehaviour
    {
        [SerializeField] protected TMP_Text _valueLabel;
        [SerializeField] private T _startValue;

        protected T _appliedValue;
        protected T _currentValue;

        public event Action<T> OnValueApplied;

        protected virtual void Awake()
        {
            _currentValue = _startValue;
            _appliedValue = _startValue;
        }

        void OnEnable()
        {
            PauseMenu.OnResetValues += ResetSettingValue;
            PauseMenu.OnApplyValues += ApplySettingValue;

            DiscardSetting();
        }

        void OnDisable()
        {
            PauseMenu.OnResetValues -= ResetSettingValue;
            PauseMenu.OnApplyValues -= ApplySettingValue;
        }

        protected abstract void UpdateValueDisplay();

        private void ApplySettingValue()
        {
            _appliedValue = _currentValue;
            OnValueApplied?.Invoke(_currentValue);
        }

        private void ResetSettingValue()
        {
            _currentValue = _startValue;

            UpdateValueDisplay();
            ApplySettingValue();
        }

        public void DiscardSetting()
        {
            _currentValue = _appliedValue;

            UpdateValueDisplay();
        }
    }
}
