using UnityEngine;

namespace PBS2D
{
    public class AudioSettings : SettingsMenu
    {
        [SerializeField]
        private SliderSetting _masterVolume;

        void OnEnable()
        {
            _masterVolume.OnValueApplied += HandleMasterVolumeChange;
        }

        void OnDisable()
        {
            _masterVolume.OnValueApplied -= HandleMasterVolumeChange;
        }

        private void HandleMasterVolumeChange(float value)
        {
            Settings.MasterVolume = value;
            AudioListener.volume = value;
        }
    }
}
