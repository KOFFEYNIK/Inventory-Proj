using UnityEngine;

namespace PBS2D
{
    public enum GunCycleType { SelfCycle, BoltAction, PumpAction }

    public enum GunReloadType { Magazine, Shell }

    [CreateAssetMenu(fileName = "Gun_Stats", menuName = "Guns/Stats")]
    public class GunStats : ScriptableObject
    {
        [Header("Firing Settings")]
        [Tooltip("The cycle type of the gun")]
        public GunCycleType CycleType = GunCycleType.SelfCycle;

        [Tooltip("Configuration for self-cycling weapons (auto/semi/burst)")]
        public SelfCycleConfig SelfCycleConfig = new()
        {
            FiringModes = GunTriggerMode.Auto | GunTriggerMode.Semi,
            IsBurstSemiAuto = false,
            BurstLength = 3,
            BurstInterval = 0.2f
        };

        [Min(1), Tooltip("How many rounds per minute the gun can shoot")]
        public int RoundsPerMinute = 600;

        [Min(1), Tooltip("How many projectiles come out of the gun each shot")]
        public int ProjectilesPerShot = 1;

        [Header("Ammo Settings")]
        [Tooltip("The way the gun is reloaded")]
        public GunReloadType ReloadType = GunReloadType.Magazine;

        [Min(1), Tooltip("Maximum ammo in the magazine/chamber")]
        public int MaxLoadedAmmo = 30;

        [Min(1), Tooltip("Maximum reserve ammo the player can carry")]
        public int MaxReserveAmmo = 120;

        [Header("Projectile Settings")]
        [Min(1), Tooltip("The maximum number of colliders the projectile can hit in a single shot")]
        public int MaxPenetration = 2;

        [Range(0f, 1f), Tooltip("Damage multiplier applied per penetration")]
        public float PenetrationDamageMultiplier = .7f;

        [Range(0f, 1f), Tooltip("Range reduction multiplier applied per penetration")]
        public float PenetrationRangeReduction = .7f;

        public DynamicFloat Range = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 45,
            MaxValue = 50
        };

        public DynamicFloat ProjectileSpeed = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 175,
            MaxValue = 200
        };

        [SerializeField]
        private ParticleSystem.MinMaxCurve _hitDamage = new(1f, AnimationCurve.EaseInOut(0f, 30f, 1f, 15f));

        [Header("Collision")]
        public LayerMask HitMask;

        [Header("Handling & Feel")]
        [Range(0f, 100f)]
        public float Accuracy = 95f;

        [Min(0f)]
        public float RecoilForce = 1f;

        [Min(0f)]
        public float BulletForce = 2f;

        public int GetDamage(float distance = 0)
        {
            float normalizedDistance = Mathf.Clamp01(distance / Range.GetValue());

            int damage = Mathf.CeilToInt(_hitDamage.Evaluate(normalizedDistance, Random.value));

            return damage;
        }

        public GunTriggerMode NextFiringMode(GunTriggerMode currentMode)
        {
            GunTriggerMode[] cycle = { GunTriggerMode.Auto, GunTriggerMode.Semi, GunTriggerMode.Burst };
            int start = System.Array.IndexOf(cycle, currentMode);

            for (int i = 1; i < cycle.Length; i++)
            {
                GunTriggerMode next = cycle[(start + i) % cycle.Length];
                if (HasFireMode(next)) return next;
            }

            return currentMode;
        }

        public bool HasFireMode(GunTriggerMode mode)
        {
            return (SelfCycleConfig.FiringModes & mode) != 0;
        }
    }
}
