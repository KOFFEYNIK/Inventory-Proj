using System.Collections;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class CharacterSkin : MonoBehaviour
    {
        [SerializeField, Min(0.001f)]
        private float _blinkInterval = 5f;

        [SerializeField, Min(0.001f)]
        private float _blinkDuration = .1f;

        [SerializeField, Range(0f, 1f)]
        private float _hitBlinkChance = 0.35f;

        private Character _character;
        private Coroutine _blinkRoutine;

        [System.NonSerialized]
        public SpriteRenderer FrontHandSRenderer, BackHandSRenderer;
        [System.NonSerialized]
        public SpriteRenderer FrontHandTargetSRenderer, BackHandTargetSRenderer;

        void Awake()
        {
            _character = GetComponent<Character>();

            FrontHandSRenderer = _character.FrontHand.GetComponent<SpriteRenderer>();
            BackHandSRenderer = _character.BackHand.GetComponent<SpriteRenderer>();
            FrontHandTargetSRenderer = _character.FrontHandIKTarget.GetComponent<SpriteRenderer>();
            BackHandTargetSRenderer = _character.BackHandIKTarget.GetComponent<SpriteRenderer>();
        }

        public void ApplySkin()
        {
            SkinConfig skin = _character.Skin;

            _character.Head.Sr.sprite = skin.Head0;
            _character.UpperTorso.Sr.sprite = skin.UpperTorso;
            _character.MidTorso.Sr.sprite = skin.MidTorso;
            _character.LowerTorso.Sr.sprite = skin.LowerTorso;

            _character.UpperFrontArm.Sr.sprite = skin.UpperFrontArm;
            _character.LowerFrontArm.Sr.sprite = skin.LowerFrontArm;
            FrontHandSRenderer.sprite = skin.FrontHand;
            _character.UpperBackArm.Sr.sprite = skin.UpperBackArm;
            _character.LowerBackArm.Sr.sprite = skin.LowerBackArm;
            BackHandSRenderer.sprite = skin.BackHand;

            _character.UpperFrontLeg.Sr.sprite = skin.UpperFrontLeg;
            _character.LowerFrontLeg.Sr.sprite = skin.LowerFrontLeg;
            _character.FrontFoot.Sr.sprite = skin.FrontFoot;
            _character.UpperBackLeg.Sr.sprite = skin.UpperBackLeg;
            _character.LowerBackLeg.Sr.sprite = skin.LowerBackLeg;
            _character.BackFoot.Sr.sprite = skin.BackFoot;
        }

        public void StartBlinking()
        {
            _blinkRoutine = StartCoroutine(BlinkCoroutine(true));
        }

        public void SetHeadSprite(Sprite sprite)
        {
            _character.Head.Sr.sprite = sprite;
        }

        public void HandleGetHit()
        {
            if (_character.IsDead || !_character.IsConscious) return;

            if (Random.value < _hitBlinkChance)
            {
                StopCoroutine(_blinkRoutine);
                _blinkRoutine = StartCoroutine(BlinkCoroutine(false));
            }
        }

        public void DefaultHands()
        {
            DefaultFrontHand();
            DefaultBackHand();
        }

        public void DefaultFrontHand()
        {
            FrontHandSRenderer.enabled = true;
            FrontHandTargetSRenderer.enabled = false;
        }

        public void DefaultBackHand()
        {
            BackHandSRenderer.enabled = true;
            BackHandTargetSRenderer.enabled = false;
        }

        public void ChangeHandSprite(bool frontHand, int handIdx)
        {
            if (frontHand)
            {
                FrontHandSRenderer.enabled = false;
                FrontHandTargetSRenderer.sprite = _character.Skin.Hands[handIdx];
                FrontHandTargetSRenderer.enabled = true;
            }
            else
            {
                BackHandSRenderer.enabled = false;
                BackHandTargetSRenderer.sprite = _character.Skin.Hands[handIdx];
                BackHandTargetSRenderer.enabled = true;
            }
        }

        private IEnumerator BlinkCoroutine(bool initialDelay)
        {
            if (initialDelay)
                yield return new WaitForSeconds(Random.Range(0, _blinkInterval));

            while (_character.IsConscious)
            {
                if (!_character.IsAiming)
                    SetHeadSprite(_character.Skin.Head2);

                yield return new WaitForSeconds(_blinkDuration);

                if (!_character.IsAiming && _character.IsConscious)
                    SetHeadSprite(_character.Skin.Head0);

                yield return new WaitForSeconds(_blinkInterval);
            }
        }
    }
}
