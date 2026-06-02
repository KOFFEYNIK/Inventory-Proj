using System.Collections;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Balance : MonoBehaviour
    {
        public float TargetRotation = 0f;

        public float CurrentWeight = 1f;
        
        public bool Active = true;

        [SerializeField] private float _force = 100f;

        private Rigidbody2D _rb;

        void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (Active && CurrentWeight > 0 && _rb.bodyType == RigidbodyType2D.Dynamic)
            {
                float newRotation = Mathf.LerpAngle(_rb.rotation, TargetRotation, _force * CurrentWeight * Time.fixedDeltaTime);
                _rb.MoveRotation(newRotation);
            }
        }

        public void SetBalanceWeight(float w) => CurrentWeight = w;

        public IEnumerator FadeOut(float duration1, float duration2, float lastBalance)
        {
            float startWeight = CurrentWeight;
            float t = 0f;

            while (t < duration1 && Active)
            {
                t += Time.deltaTime;
                CurrentWeight = Mathf.Lerp(startWeight, lastBalance, t / duration1);
                yield return null;
            }

            CurrentWeight = lastBalance;

            t = 0f;
            while (t < duration2 && Active)
            {
                t += Time.deltaTime;
                CurrentWeight = Mathf.Lerp(lastBalance, 0f, t / duration2);
                yield return null;
            }

            CurrentWeight = 0f;
        }

        public IEnumerator FadeOut(float duration1)
        {
            float startWeight = CurrentWeight;
            float t = 0f;

            while (t < duration1 && Active)
            {
                t += Time.deltaTime;
                CurrentWeight = Mathf.Lerp(startWeight, 0, t / duration1);
                yield return null;
            }

            CurrentWeight = 0f;
        }
    }
}
