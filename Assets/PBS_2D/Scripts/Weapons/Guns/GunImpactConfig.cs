using UnityEngine;

namespace PBS2D
{
    [CreateAssetMenu(fileName = "Gun_ImpactConfig", menuName = "Guns/Impact Config")]
    public class GunImpactConfig : ScriptableObject
    {
        [Header("Environment Impact FX")]
        public GameObject GroundImpact;
        public GameObject BloodWallSplash;

        [Header("Biological Impact FX")]
        public GameObject Wound;
        public GameObject BloodDrops;
        public GameObject BloodMush;
        public GameObject BloodMushFinisher;
        public GameObject BloodCascade;

        [Header("Chances")]
        [Range(0f, 1f)]
        public float WoundSpawnChance = 1f;

        [Range(0f, 1f)]
        public float BloodDropsSpawnChance = .9f;

        [Range(0f, 1f)]
        public float BloodMushSpawnChance = 1f;

        [Range(0f, 1f)]
        public float BloodMushFinisherSpawnChance = 1f;

        [Range(0f, 1f)]
        public float BloodCascadeSpawnChance = 0.9f;

    }
}
