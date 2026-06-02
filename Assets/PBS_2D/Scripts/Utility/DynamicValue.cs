using UnityEngine;

namespace PBS2D
{
    public enum DynamicValueMode { Constant, BetweenTwoConstants }

    [System.Serializable]
    public class DynamicFloat
    {
        public DynamicValueMode Mode;

        public float Value;
        public float MinValue;
        public float MaxValue;

        public float GetValue()
        {
            if (Mode == DynamicValueMode.Constant)
            {
                return Value;
            }
            else
            {
                return Random.Range(MinValue, MaxValue);
            }
        }
    }

    [System.Serializable]
    public class DynamicInt
    {
        public DynamicValueMode Mode;

        public int Value;
        public int MinValue;
        public int MaxValue;

        public int GetValue()
        {
            if (Mode == DynamicValueMode.Constant)
            {
                return Value;
            }
            else
            {
                return Random.Range(MinValue, MaxValue + 1);
            }
        }
    }
}
