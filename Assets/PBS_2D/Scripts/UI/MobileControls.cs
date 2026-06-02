using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Canvas))]
    public class MobileControls : Singleton<MobileControls>
    {
        public CustomStickManager[] CustomSticks;

        public static bool IsTouchClaimedByStick(int id)
        {
            if (_instance == null || _instance.CustomSticks == null)
                return false;

            foreach (var stick in _instance.CustomSticks)
            {
                if (stick != null && stick.MoveFingerId == id)
                    return true;
            }

            return false;
        }
    }
}
