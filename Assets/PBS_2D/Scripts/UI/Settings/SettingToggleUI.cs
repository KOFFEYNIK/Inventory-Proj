using UnityEngine;

namespace PBS2D
{
    public class ToggleSetting : SettingElement<bool>
    {
        protected override void UpdateValueDisplay()
        {
            _valueLabel.text = _currentValue ? "On" : "Off";
        }

        public void ToggleValue()
        {
            _currentValue = !_currentValue;

            UpdateValueDisplay();
        }
    }
}
