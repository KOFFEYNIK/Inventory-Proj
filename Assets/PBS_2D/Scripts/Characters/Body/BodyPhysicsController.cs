using System.Collections;
using UnityEngine;

namespace PBS2D
{
    [System.Serializable]
    public struct TorsoAngles
    {
        public float Lower;
        public float Mid;
        public float Upper;
    }

    [RequireComponent(typeof(Character))]
    public class BodyPhysicsController : MonoBehaviour
    {
        private const float FOOT_OFFSET = .15625f; // 5/32 pixel-grid unit

        [Header("Settings")]
        [SerializeField, Tooltip("How far the head can rotate")]
        private float _maxHeadAngle = 50f;

        [SerializeField, Tooltip("How far the torsos can lean")]
        private float _maxTorsoAngle = 20f;

        [SerializeField, Tooltip("Torso target angles while idle")]
        private TorsoAngles _idleTorsoAngles = new()
        {
            Lower = 0f,
            Mid = 2.5f,
            Upper = 5f
        };

        [SerializeField, Tooltip("Torso target angles while crouching")]
        private TorsoAngles _crouchTorsoAngles = new()
        {
            Lower = 5f,
            Mid = 7.5f,
            Upper = 10f
        };

        [SerializeField, Tooltip("How much the torsos lean into horizontal velocity")]
        private float _velocityLeanMultiplier = 2.5f;

        [Header("Breathing")]
        [SerializeField, Tooltip("How fast the breathing cycle runs")]
        private float _breathingSpeed = 1.6f;

        [SerializeField, Tooltip("How much the torsos move per breath")]
        private float _breathingAmplitude = 1f;

        [SerializeField, Tooltip("How much harder the upper torso breathes")]
        private float _upperTorsoBreathingScale = 1.5f;

        [SerializeField, Tooltip("How much breathing motion is kept while aiming. 0 = no breathing while aiming")]
        private float _aimBreathingScale = 0.5f;

        [Header("Head")]
        [SerializeField, Tooltip("How much extra the head tilts down while aiming (degrees)")]
        private float _headAimOffset = 35f;

        [SerializeField, Tooltip("How much the upper torso follows the head rotation. 0 = ignore, 1 = match")]
        private float _upperTorsoHeadFollow = 0.2f;

        [SerializeField, Tooltip("How much the mid torso follows the head rotation. 0 = ignore, 1 = match")]
        private float _midTorsoHeadFollow = 0.1f;

        [Header("Death")]
        [SerializeField, Tooltip("Lower torso tilt past which the character disables the legs")]
        private float _torsoTiltThreshold = 40f;

        [SerializeField, Tooltip("Foot distance from torso past which the character disables the legs")]
        private float _footDistanceThreshold = 0.6f;

        [SerializeField, Tooltip("Angle the upper legs hold while the character is falling")]
        private float _fallingUpperLegRotation = 65f;

        [SerializeField, Tooltip("Angle the lower legs hold while the character is falling")]
        private float _fallingLowerLegRotation = 30f;

        [SerializeField, Tooltip("How strongly the upper legs hold their target while falling")]
        private float _fallingUpperLegWeight = 0.1f;

        [SerializeField, Tooltip("How strongly the lower legs and feet hold their target while falling")]
        private float _fallingLowerWeight = 0.05f;

        [SerializeField, Tooltip("How strongly the head holds its target rotation during death")]
        private float _fadeOutHeadWeight = 0.01f;

        [SerializeField, Tooltip("How strongly the torsos hold their target at the start of death")]
        private DynamicFloat _fadeOutTorsoWeight = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 0.1f,
            MaxValue = 0.2f
        };

        [SerializeField, Tooltip("How strongly the legs hold their target at the start of death")]
        private DynamicFloat _fadeOutLegWeight = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 0.5f,
            MaxValue = 0.8f
        };

        [SerializeField, Tooltip("How weakly the body holds its target right before going limp")]
        private DynamicFloat _fadeOutFinalWeight = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 0.02f,
            MaxValue = 0.03f
        };

        [SerializeField, Tooltip("How long the second phase of the death fade lasts")]
        private DynamicFloat _fadeOutSecondDuration = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 3f,
            MaxValue = 6f
        };

        private float _aimMultiplier = 0;
        private Character _character;

        private BodyPartRef[] _torsoAndHead;
        private BodyPartRef[] _legParts;

        private float _breathingPhase = 0f;
        private float _breathingOffset;

        private float _velocityLean;

        void Awake()
        {
            _character = GetComponent<Character>();

            _torsoAndHead = new[]
            {
                _character.Head,
                _character.UpperTorso, _character.MidTorso, _character.LowerTorso
            };

            _legParts = new[]
            {
                _character.UpperFrontLeg, _character.LowerFrontLeg, _character.FrontFoot,
                _character.UpperBackLeg, _character.LowerBackLeg, _character.BackFoot
            };
        }

        void Update()
        {
            if (_character.IsDead) return;

            if (_character.IsAiming) _aimMultiplier = 1;
            else _aimMultiplier = 0;

            if (!_character.IsConscious)
            {
                float tilt = _character.LowerTorso.Transform.eulerAngles.z;
                if (tilt > 180f) tilt = 360f - tilt;

                if (FeetTooFarFromTorso() || tilt > _torsoTiltThreshold)
                {
                    foreach (BodyPartRef part in _legParts) part.Bal.Active = false;
                }
                return;
            }

            _breathingPhase += Time.deltaTime * _breathingSpeed;
            _breathingOffset = Mathf.Sin(_breathingPhase) * _breathingAmplitude;
            if (_character.IsAiming) _breathingOffset *= _aimBreathingScale;

            _velocityLean = _character.LowerTorso.Rb.linearVelocityX * _velocityLeanMultiplier;

            RotateLowerTorso();
            RotateMidTorso();
            RotateUpperTorso();
            RotateHead();
        }

        public void ActivateBalance()
        {
            _character.IsConscious = true;

            foreach (BodyPartRef part in _torsoAndHead) part.Bal.Active = true;

            RagdollArms(false);
            RagdollLegs(false, false);
        }

        public IEnumerator FadeOutBalance(float duration)
        {
            _character.UpperFrontLeg.Bal.TargetRotation = _character.UpperFrontLeg.Rb.rotation;
            _character.LowerFrontLeg.Bal.TargetRotation = _character.LowerFrontLeg.Rb.rotation;
            _character.UpperBackLeg.Bal.TargetRotation = _character.UpperBackLeg.Rb.rotation;
            _character.LowerBackLeg.Bal.TargetRotation = _character.LowerBackLeg.Rb.rotation;
            _character.FrontFoot.Bal.TargetRotation = 0;
            _character.BackFoot.Bal.TargetRotation = 0;

            float torsoWeight = _fadeOutTorsoWeight.GetValue();
            float legWeight = _fadeOutLegWeight.GetValue();

            _character.Head.Bal.CurrentWeight = _fadeOutHeadWeight;
            _character.UpperTorso.Bal.CurrentWeight = torsoWeight;
            _character.MidTorso.Bal.CurrentWeight = torsoWeight;
            _character.LowerTorso.Bal.CurrentWeight = torsoWeight;

            _character.UpperFrontLeg.Bal.CurrentWeight = legWeight;
            _character.LowerFrontLeg.Bal.CurrentWeight = legWeight;
            _character.FrontFoot.Bal.CurrentWeight = legWeight;
            _character.UpperBackLeg.Bal.CurrentWeight = legWeight;
            _character.LowerBackLeg.Bal.CurrentWeight = legWeight;
            _character.BackFoot.Bal.CurrentWeight = legWeight;

            RagdollArms(true);
            RagdollLegs(true, false);

            float lastBalance = _fadeOutFinalWeight.GetValue();
            float secondPhaseDuration = _fadeOutSecondDuration.GetValue();

            StartCoroutine(_character.Head.Bal.FadeOut(duration, secondPhaseDuration, _fadeOutHeadWeight));
            StartCoroutine(_character.UpperTorso.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));
            StartCoroutine(_character.MidTorso.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));
            StartCoroutine(_character.LowerTorso.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));

            StartCoroutine(_character.UpperFrontLeg.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));
            StartCoroutine(_character.LowerFrontLeg.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));
            StartCoroutine(_character.FrontFoot.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));

            StartCoroutine(_character.UpperBackLeg.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));
            StartCoroutine(_character.LowerBackLeg.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));
            StartCoroutine(_character.BackFoot.Bal.FadeOut(duration, secondPhaseDuration, lastBalance));

            yield return new WaitForSeconds(duration + secondPhaseDuration);

            _character.IsDead = true;
        }

        public void DeactivateBalances()
        {
            RagdollArms(true);
            RagdollLegs(true, false);

            _character.Head.Bal.Active = false;
            _character.UpperTorso.Bal.Active = false;
            _character.MidTorso.Bal.Active = false;
            _character.LowerTorso.Bal.Active = false;

            _character.UpperFrontLeg.Bal.Active = false;
            _character.LowerFrontLeg.Bal.Active = false;
            _character.FrontFoot.Bal.Active = false;

            _character.UpperBackLeg.Bal.Active = false;
            _character.LowerBackLeg.Bal.Active = false;
            _character.BackFoot.Bal.Active = false;

            _character.IsDead = true;
        }

        private void RotateHead()
        {
            float angle = NormalizedHeadAngle();

            if (_character.IsAiming)
                angle += _character.IsFacingRight ? -_headAimOffset : _headAimOffset;

            _character.Head.Bal.TargetRotation = Mathf.Clamp(angle, -_maxHeadAngle, _maxHeadAngle);
        }

        private float NormalizedHeadAngle()
        {
            float angle = _character.CharacterRotation.HeadRotation;
            return angle > 180f ? angle - 360f : angle;
        }

        private void RotateUpperTorso()
        {
            float angle = NormalizedHeadAngle();
            float clamped = Mathf.Clamp(angle * _upperTorsoHeadFollow, -_maxTorsoAngle, _maxTorsoAngle);

            if (_character.IsFacingRight)
            {
                if (_character.HeightController.IsCrouching)
                    _character.UpperTorso.Bal.TargetRotation = clamped - _crouchTorsoAngles.Upper - (_aimMultiplier * _idleTorsoAngles.Upper) + _breathingOffset * _upperTorsoBreathingScale;
                else
                    _character.UpperTorso.Bal.TargetRotation = clamped - _idleTorsoAngles.Upper - (_aimMultiplier * _idleTorsoAngles.Upper) - _velocityLean + _breathingOffset * _upperTorsoBreathingScale;
            }
            else
            {
                if (_character.HeightController.IsCrouching)
                    _character.UpperTorso.Bal.TargetRotation = clamped + _crouchTorsoAngles.Upper + (_aimMultiplier * _idleTorsoAngles.Upper) + _breathingOffset * _upperTorsoBreathingScale;
                else
                    _character.UpperTorso.Bal.TargetRotation = clamped + _idleTorsoAngles.Upper + (_aimMultiplier * _idleTorsoAngles.Upper) - _velocityLean + _breathingOffset * _upperTorsoBreathingScale;
            }
        }

        private void RotateMidTorso()
        {
            float angle = NormalizedHeadAngle();
            float clamped = Mathf.Clamp(angle * _midTorsoHeadFollow, -_maxTorsoAngle, _maxTorsoAngle);

            if (_character.IsFacingRight)
            {
                if (_character.HeightController.IsCrouching)
                    _character.MidTorso.Bal.TargetRotation = clamped - _crouchTorsoAngles.Mid - (_aimMultiplier * _idleTorsoAngles.Mid) + _breathingOffset;
                else
                    _character.MidTorso.Bal.TargetRotation = clamped - _idleTorsoAngles.Mid - (_aimMultiplier * _idleTorsoAngles.Mid) - _velocityLean + _breathingOffset;
            }
            else
            {
                if (_character.HeightController.IsCrouching)
                    _character.MidTorso.Bal.TargetRotation = clamped + _crouchTorsoAngles.Mid + (_aimMultiplier * _idleTorsoAngles.Mid) + _breathingOffset;
                else
                    _character.MidTorso.Bal.TargetRotation = clamped + _idleTorsoAngles.Mid + (_aimMultiplier * _idleTorsoAngles.Mid) - _velocityLean + _breathingOffset;
            }
        }

        private void RotateLowerTorso()
        {
            if (_character.IsFacingRight)
            {
                if (_character.HeightController.IsCrouching)
                    _character.LowerTorso.Bal.TargetRotation = -_crouchTorsoAngles.Lower - (_aimMultiplier * _idleTorsoAngles.Lower);

                else
                    _character.LowerTorso.Bal.TargetRotation = -_idleTorsoAngles.Lower - (_aimMultiplier * _idleTorsoAngles.Lower) - _velocityLean;
            }
            else
            {
                if (_character.HeightController.IsCrouching)
                    _character.LowerTorso.Bal.TargetRotation = _crouchTorsoAngles.Lower + (_aimMultiplier * _idleTorsoAngles.Lower);

                else
                    _character.LowerTorso.Bal.TargetRotation = _idleTorsoAngles.Lower + (_aimMultiplier * _idleTorsoAngles.Lower) - _velocityLean;
            }
        }

        public void RagdollArms(bool ragdoll)
        {
            RagdollFrontArm(ragdoll);
            RagdollBackArm(ragdoll);
        }

        public void RagdollFrontArm(bool ragdoll)
        {
            _character.FrontArmSolver.enabled = !ragdoll;
            _character.UpperFrontArmHinge.enabled = ragdoll;

            RigidbodyType2D rbType = ragdoll ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            SetRigidbodyType(new[] { _character.UpperFrontArm.Rb, _character.LowerFrontArm.Rb }, rbType);
        }

        public void RagdollBackArm(bool ragdoll)
        {
            _character.BackArmSolver.enabled = !ragdoll;
            _character.UpperBackArmHinge.enabled = ragdoll;

            RigidbodyType2D rbType = ragdoll ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            SetRigidbodyType(new[] { _character.UpperBackArm.Rb, _character.LowerBackArm.Rb }, rbType);
        }

        public void RagdollLegs(bool ragdoll, bool falling)
        {
            RagdollFrontLeg(ragdoll);
            RagdollBackLeg(ragdoll);

            if (falling)
            {
                float sign = _character.IsFacingRight ? 1f : -1f;
                float upperLegRotation = sign * _fallingUpperLegRotation;
                float lowerLegRotation = -sign * _fallingLowerLegRotation;

                ApplyBalance(new Balance[] { _character.UpperFrontLeg.Bal }, upperLegRotation, true, _fallingUpperLegWeight);
                ApplyBalance(new Balance[] { _character.LowerFrontLeg.Bal }, lowerLegRotation, true, _fallingLowerWeight);

                ApplyBalance(new Balance[] { _character.UpperBackLeg.Bal }, upperLegRotation, true, _fallingUpperLegWeight);
                ApplyBalance(new Balance[] { _character.LowerBackLeg.Bal }, lowerLegRotation, true, _fallingLowerWeight);
                ApplyBalance(new Balance[] { _character.BackFoot.Bal, _character.FrontFoot.Bal }, 0f, true, _fallingLowerWeight);

            }
        }

        public void RagdollFrontLeg(bool ragdoll)
        {
            if (ragdoll)
                _character.LegsController.StopLeg(_character.LegsController.FrontLegState);

            _character.FrontLegSolver.enabled = !ragdoll;
            _character.UpperFrontLegHinge.enabled = ragdoll;

            RigidbodyType2D rbType = ragdoll ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            SetRigidbodyType(new[] { _character.UpperFrontLeg.Rb, _character.LowerFrontLeg.Rb, _character.FrontFoot.Rb }, rbType);

            _character.FrontFootBodyPart.FixLocalPosition();
        }

        public void RagdollBackLeg(bool ragdoll)
        {
            if (ragdoll)
                _character.LegsController.StopLeg(_character.LegsController.BackLegState);

            _character.BackLegSolver.enabled = !ragdoll;
            _character.UpperBackLegHinge.enabled = ragdoll;

            RigidbodyType2D rbType = ragdoll ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            SetRigidbodyType(new[] { _character.UpperBackLeg.Rb, _character.LowerBackLeg.Rb, _character.BackFoot.Rb }, rbType);

            _character.BackFootBodyPart.FixLocalPosition();
        }

        private void ApplyBalance(Balance[] balances, float targetRotation, bool active, float currentWeight)
        {
            foreach (Balance balance in balances)
            {
                balance.TargetRotation = targetRotation;
                balance.Active = active;
                balance.CurrentWeight = currentWeight;
            }
        }

        private bool FeetTooFarFromTorso()
        {
            float torsoX = _character.LowerTorso.Transform.position.x;
            float frontX = _character.FrontFoot.Transform.position.x;
            float backX = _character.BackFoot.Transform.position.x;

            if (_character.IsFacingRight) { frontX += FOOT_OFFSET; backX -= FOOT_OFFSET; }
            else { frontX -= FOOT_OFFSET; backX += FOOT_OFFSET; }

            float frontDist = Mathf.Abs(torsoX - frontX);
            float backDist = Mathf.Abs(torsoX - backX);

            return Mathf.Min(frontDist, backDist) > _footDistanceThreshold;
        }

        public void FlipCharacter()
        {
            _character.LowerTorso.Rb.bodyType = RigidbodyType2D.Static;

            InvertIKTargets();

            _character.LowerTorso.Transform.localScale = new Vector3(-_character.LowerTorso.Transform.localScale.x, 1, 1);

            InvertHingeLimits(_character.HeadHinge);
            InvertHingeLimits(_character.UpperTorsoHinge);
            InvertHingeLimits(_character.MidTorsoHinge);
            InvertHingeLimits(_character.LowerBackArmHinge);
            InvertHingeLimits(_character.LowerFrontArmHinge);
            InvertHingeLimits(_character.UpperBackLegHinge);
            InvertHingeLimits(_character.LowerBackLegHinge);
            InvertHingeLimits(_character.BackFootHinge);
            InvertHingeLimits(_character.UpperFrontLegHinge);
            InvertHingeLimits(_character.LowerFrontLegHinge);
            InvertHingeLimits(_character.FrontFootHinge);

            if (_character.UpperFrontLeg.Bal.Active) _character.UpperFrontLeg.Bal.TargetRotation = -_character.UpperFrontLeg.Bal.TargetRotation;
            if (_character.LowerFrontLeg.Bal.Active) _character.LowerFrontLeg.Bal.TargetRotation = -_character.LowerFrontLeg.Bal.TargetRotation;

            if (_character.UpperBackLeg.Bal.Active) _character.UpperBackLeg.Bal.TargetRotation = -_character.UpperBackLeg.Bal.TargetRotation;
            if (_character.LowerBackLeg.Bal.Active) _character.LowerBackLeg.Bal.TargetRotation = -_character.LowerBackLeg.Bal.TargetRotation;

            _character.LowerTorso.Rb.bodyType = RigidbodyType2D.Dynamic;

            _character.LegsController.InverseLegsOrientation();

            if (_character.IsRunning) _character.Movement.StopRunning();
            _character.WeaponManager.InvertWeaponPositionAndRotation();
        }

        private void InvertIKTargets()
        {
            (_character.FrontLegSolver.GetChain(0).target, _character.BackLegSolver.GetChain(0).target)
                = (_character.BackLegSolver.GetChain(0).target, _character.FrontLegSolver.GetChain(0).target);
        }

        private void InvertHingeLimits(HingeJoint2D hinge)
        {
            JointAngleLimits2D limits = hinge.limits;
            float newMin = -limits.max;
            float newMax = -limits.min;

            limits.min = newMin;
            limits.max = newMax;

            hinge.limits = limits;
        }

        private void SetRigidbodyType(Rigidbody2D[] rbs, RigidbodyType2D type)
        {
            foreach (Rigidbody2D rb in rbs)
            {
                rb.bodyType = type;
            }
        }
    }
}
