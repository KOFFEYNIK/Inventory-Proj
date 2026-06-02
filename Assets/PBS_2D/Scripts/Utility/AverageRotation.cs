using UnityEngine;

namespace PBS2D
{
    public class AverageRotation : MonoBehaviour
    {
        [SerializeField]
        private Transform _targetA;

        [SerializeField]
        private Transform _targetB;

        void Update()
        {
            if (_targetA == null || _targetB == null) return;

            float angleA = _targetA.eulerAngles.z;
            float angleB = _targetB.eulerAngles.z;
            float averageAngle = Mathf.DeltaAngle(0, angleA - angleB) / 2f + angleB;

            transform.rotation = Quaternion.Euler(0f, 0f, averageAngle);
        }
    }
}
