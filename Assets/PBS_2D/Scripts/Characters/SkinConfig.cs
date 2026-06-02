using UnityEngine;

namespace PBS2D
{
    [CreateAssetMenu(fileName = "NewSkinConfig", menuName = "Character/Skin Config")]
    public class SkinConfig : ScriptableObject
    {
        public Sprite
            Head0, Head1, Head2,
            UpperTorso, MidTorso, LowerTorso,

            UpperFrontArm, LowerFrontArm, FrontHand,
            UpperBackArm, LowerBackArm, BackHand,

            UpperFrontLeg, LowerFrontLeg, FrontFoot,
            UpperBackLeg, LowerBackLeg, BackFoot;

        public Sprite[] Hands = new Sprite[7];
    }   
}
