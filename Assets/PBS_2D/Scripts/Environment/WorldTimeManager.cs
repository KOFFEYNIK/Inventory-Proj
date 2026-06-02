using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PBS2D
{
    public class WorldTimeManager : Singleton<WorldTimeManager>
    {
        [Header("References")]
        [SerializeField] private Camera _cam;
        [SerializeField] private Light2D _globalLight;

        [Header("Sky")]
        [SerializeField] private Color _daySkyColor;
        [SerializeField] private Color _nightSkyColor;

        [Header("Lighting")]
        [SerializeField] private float _dayLightIntensity;
        [SerializeField] private float _nightLightIntensity;

        public void SetDayTime()
        {
            Settings.DayTime = true;
            _cam.backgroundColor = _daySkyColor;
            _globalLight.intensity = _dayLightIntensity;
        }

        public void SetNightTime()
        {
            Settings.DayTime = false;
            _cam.backgroundColor = _nightSkyColor;
            _globalLight.intensity = _nightLightIntensity;
        }
    }
}
