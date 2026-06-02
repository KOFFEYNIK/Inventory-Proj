namespace PBS2D
{
    [System.Flags]
    public enum GunTriggerMode { Auto = 1, Semi = 2, Burst = 4 }

    [System.Serializable]
    public struct SelfCycleConfig
    {
        public GunTriggerMode FiringModes;

        public bool IsBurstSemiAuto;
        public int BurstLength;
        public float BurstInterval;
    }
}
