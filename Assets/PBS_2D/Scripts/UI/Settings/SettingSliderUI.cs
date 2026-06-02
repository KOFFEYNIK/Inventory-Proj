using System;
using UnityEngine;
using UnityEngine.UI;

namespace PBS2D
{
    public class SliderSetting : SettingElement<float>
    {
        [SerializeField] private Slider _slider;

        [SerializeField] private float _minValue = 0.0f;
        [SerializeField] private float _maxValue = 1.0f;
        [SerializeField] private float _step = 0.05f;
        [SerializeField] private bool _percentage = false;

        private int _decimalPlaces;

        protected override void Awake()
        {
            base.Awake();

            _decimalPlaces = Mathf.Max(0, _step.ToString().Length - 2);
            _slider.wholeNumbers = _step == 1;

            InitSlider();
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(float value)
        {
            _currentValue = value;
            UpdateValueDisplay();
        }

        protected override void UpdateValueDisplay()
        {
            string text = _percentage ? (_currentValue * 100).ToString("F0") + "%" : _currentValue.ToString("F" + _decimalPlaces);

            UpdateSlider();
            _valueLabel.text = text;
        }

        private void InitSlider()
        {
            _slider.minValue = _minValue;
            _slider.maxValue = _maxValue;
            UpdateSlider();
        }

        private void UpdateSlider()
        {
            _slider.value = _currentValue;
        }

        public void DecreaseValue()
        {
            _currentValue = Math.Max(_minValue, _currentValue - _step);

            UpdateValueDisplay();
            UpdateSlider();
        }

        public void IncreaseValue()
        {
            _currentValue = Math.Min(_maxValue, _currentValue + _step);

            UpdateValueDisplay();
            UpdateSlider();
        }
    }
}
