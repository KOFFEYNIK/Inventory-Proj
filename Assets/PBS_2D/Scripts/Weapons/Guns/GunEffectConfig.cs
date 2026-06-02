using UnityEngine;

namespace PBS2D
{
    [CreateAssetMenu(fileName = "Gun_EffectConfig", menuName = "Guns/Effect Config")]
    public class GunEffectConfig : ScriptableObject
    {
        [Header("Visual Effects")]
        public GameObject EjectEffect;
        public GameObject MuzzleFlash;
        public GameObject MuzzleSmoke;
        public GameObject BulletPrefab;

        [Header("Feel & Feedback")]
        [Min(0f)] public float CameraShakeAmount = .02f;
        [Min(0f)] public float KickBackDistance = .15f;
    }
}
