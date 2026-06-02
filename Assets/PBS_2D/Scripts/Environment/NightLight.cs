using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PBS2D
{
    public class NightLight : MonoBehaviour
    {
        private Light2D _light;

        void Awake()
        {
            _light = GetComponent<Light2D>();
        }

        void Update()
        {
            _light.enabled = !Settings.DayTime;
        }
    }
}
