using UnityEngine;

namespace PBS2D
{
    public class GameplaySettings : SettingsMenu
    {
        [SerializeField] private ToggleSetting _autoCycleSetting;
        [SerializeField] private ToggleSetting _dayTimeSetting;
        [SerializeField] private ToggleSetting _tracerRoundsSetting;
        [SerializeField] private SliderSetting _bulletForceSetting;
        [SerializeField] private SliderSetting _cameraSizeSetting;
        [SerializeField] private SliderSetting _slowMotionEffectSetting;

        void OnEnable()
        {
            _autoCycleSetting.OnValueApplied += ApplyAutoCycleValue;
            _dayTimeSetting.OnValueApplied += ApplyDayTimeValue;
            _tracerRoundsSetting.OnValueApplied += ApplyTracerRoundsValue;
            _bulletForceSetting.OnValueApplied += ApplyBulletForceValue;
            _cameraSizeSetting.OnValueApplied += ApplyCameraSizeValue;
            _slowMotionEffectSetting.OnValueApplied += ApplySlowMotionEffectValue;
        }

        void OnDisable()
        {
            _autoCycleSetting.OnValueApplied -= ApplyAutoCycleValue;
            _dayTimeSetting.OnValueApplied -= ApplyDayTimeValue;
            _tracerRoundsSetting.OnValueApplied -= ApplyTracerRoundsValue;
            _bulletForceSetting.OnValueApplied -= ApplyBulletForceValue;
            _cameraSizeSetting.OnValueApplied -= ApplyCameraSizeValue;
            _slowMotionEffectSetting.OnValueApplied -= ApplySlowMotionEffectValue;
        }

        private void ApplyAutoCycleValue(bool value)
        {
            Settings.AutoCycle = value;
        }

        private void ApplyDayTimeValue(bool value)
        {
            if (value) WorldTimeManager.Instance.SetDayTime();
            else WorldTimeManager.Instance.SetNightTime();
        }

        private void ApplyTracerRoundsValue(bool value)
        {
            Settings.TracerRounds = value;
        }

        private void ApplyBulletForceValue(float value)
        {
            Settings.BulletForceMultiplier = value;
        }

        private void ApplyCameraSizeValue(float value)
        {
            CameraManager.Instance.ChangeCameraSize(value);
        }

        private void ApplySlowMotionEffectValue(float value)
        {
            Settings.SlowMotionEffect = value;
        }
    }
}
