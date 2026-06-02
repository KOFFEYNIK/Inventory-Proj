using UnityEngine;

namespace PBS2D
{
    [CreateAssetMenu(fileName = "AI_Behavior", menuName = "Character/AI Behavior")]
    public class AIBehavior : ScriptableObject
    {
        [Min(0.05f), Tooltip("How often the AI re-evaluates its decisions (seconds). Lower = snappier reactions")]
        public float TickInterval = 0.3f;

        [Header("Movement"), Tooltip("Whether the AI moves toward the target")]
        public bool ChaseTarget = false;

        [Min(0), Tooltip("Preferred distance from the target. Backs off if closer, chases if farther")]
        public float KeepDistance = 10f;

        [Min(0), Tooltip("Past this distance the AI runs instead of walking")]
        public float WalkDistance = 20f;

        [Header("Shoot"), Tooltip("Whether the AI shoots at the target")]
        public bool AttackTarget = false;

        [Min(0), Tooltip("Max distance at which the AI will open fire")]
        public float MaxShootDistance = 20f;

        [Min(1), Tooltip("Rays fanned from the gun toward the target. More rays = better at spotting limbs poking out of cover")]
        public int RayCount = 5;

        [Min(0), Tooltip("Total angle covered by the ray fan (degrees)")]
        public float RaySpreadAngle = 20f;

        [Min(0), Tooltip("Delay between spotting the target and the first shot. Resets each time line of sight is re-acquired")]
        public float ReactionTime = .75f;

        [Min(0), Tooltip("How long the trigger is held per burst. 0 = hold continuously while the target is in sight")]
        public float BurstDuration = 0.5f;

        [Min(0), Tooltip("Pause between bursts")]
        public float BurstPause = 0.5f;

        [Range(0f, 1f), Tooltip("Per-bullet chance to actually hit the target. 0 = AI bullets always pass through")]
        public float HitChance = 0.5f;
    }
}
