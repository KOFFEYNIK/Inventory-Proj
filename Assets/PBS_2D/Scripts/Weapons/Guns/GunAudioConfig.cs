using UnityEngine;

namespace PBS2D
{
    [CreateAssetMenu(fileName = "Gun_AudioConfig", menuName = "Guns/Audio Config")]
    public class GunAudioConfig : ScriptableObject
    {
        [Header("Firing Sounds")]
        public AudioClip ShootClip;
        public AudioClip PullTriggerClip;

        [Header("Mechanical Actions")]
        public AudioClip GunMechClip;
        public AudioClip PullBoltClip;
        public AudioClip PushBoltClip;

        [Header("Magazine Actions")]
        public AudioClip SnapMagClip;
        public AudioClip ReleaseMagClip;

        [Header("Shotgun / Manual Actions")]
        public AudioClip PullForendClip;
        public AudioClip PushForendClip;
        public AudioClip InsertShellClip;
    }
}
