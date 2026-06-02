using UnityEngine;

namespace PBS2D
{
    public enum BodyPartGroup { Head, Torso, FrontArm, BackArm, FrontLeg, BackLeg }

    public class CharacterImpactSound : ImpactSound
    {
        [SerializeField]
        private Character _character;

        [SerializeField]
        private BodyPartGroup _bodyPartGroup;

        protected override void OnCollisionEnter2D(Collision2D collision)
        {
            if (Time.time < _character.GetLastImpactTime(_bodyPartGroup) + _cooldown)
                return;

            base.OnCollisionEnter2D(collision);
        }

        protected override void UpdateCooldown()
        {
            _character.SetLastImpactTime(_bodyPartGroup, Time.time);
        }
    }
}
