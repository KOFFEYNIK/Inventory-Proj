using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PBS2D
{
    public class VideoSettings : SettingsMenu
    {
        [SerializeField] private SliderSetting _renderScale;

        private UniversalRenderPipelineAsset _urpAsset;

        void Start()
        {
            _urpAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            if (Application.isMobilePlatform)
            {
                _urpAsset.renderScale = .7f;
            }
            else
            {
                _urpAsset.renderScale = 1;
            }
        }

        void OnEnable()
        {
            _renderScale.OnValueApplied += ApplyRenderScaleValue;
        }

        void OnDisable()
        {
            _renderScale.OnValueApplied -= ApplyRenderScaleValue;
        }

        private void ApplyRenderScaleValue(float value)
        {
            _urpAsset.renderScale = value;
        }
    }
}
