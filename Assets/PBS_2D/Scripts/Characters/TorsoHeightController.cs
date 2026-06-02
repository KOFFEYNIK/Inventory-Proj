using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class TorsoHeightController : MonoBehaviour
    {
        private class HeightModifier
        {
            public float Multiplier;
            public float TimeRemaining;
        }

        private const float FOOT_HEIGHT = .1875f;
        private const float KNEELING_HEIGHT_MULTIPLIER = 0.6f;
        private const float CROUCHING_HEIGHT_MULTIPLIER = 0.85f;
        private const float FOOT_EXCESS_HEIGHT_MULTIPLIER = 0.9f;
        private const float GIZMO_TORSO_OFFSET = 0.35f;

        [Header("References")]
        [SerializeField]
        private LayerMask _targetLayers;

        [Header("Settings")]
        [SerializeField]
        private float _defaultGroundOffset = 2.05f;

        [SerializeField]
        private float _maxFootExcess = 0.05f;

        [SerializeField]
        private float _kneelingCooldown = 0.5f;

        [SerializeField]
        private float _crouchCooldown = .5f;

        [Header("Walk Settings")]
        [SerializeField]
        private float _walkSpringStrength = 175f;

        [SerializeField]
        private float _walkDampingCoefficient = 40f;

        [Header("Run Settings")]
        [SerializeField]
        private float _runSpringStrength = 200f;

        [SerializeField]
        private float _runDampingCoefficient = 30f;

        private readonly List<HeightModifier> _activeModifiers = new ();
        [System.NonSerialized] public Vector2 DesiredTorsoPos;
        [System.NonSerialized] public Vector2 CurrentTorsoPos;
        [System.NonSerialized] public float Multiplier = 1;
        [System.NonSerialized] public bool IsKneeling = false;
        [System.NonSerialized] public bool IsCrouching = false;
        private Character _character;
        private float _currentTemporaryMultiplier = 1f;
        private float _currentYOffset;
        private bool _canCrouch = true;

        private readonly WaitForFixedUpdate _waitForFixedUpdate = new();

        void Awake()
        {
            _character = GetComponent<Character>();
        }

        void Start()
        {
            _currentYOffset = _defaultGroundOffset;
        }

        void FixedUpdate()
        {
            if (_character.IsConscious && (_character.IsFrontFootGrounded || _character.IsBackFootGrounded))
            {
                ProcessHeightModifiers();
                UpdateCurrentY();
                UpdateTorsoOffset();
                ApplyTorsoOffset();
            }
        }

        private void UpdateCurrentY()
        {
            IsKneeling = IsCrouching && _character.Movement.moveInput == Vector2.zero;

            if (IsKneeling) _currentYOffset = _defaultGroundOffset * KNEELING_HEIGHT_MULTIPLIER;
            else if (IsCrouching) _currentYOffset = _defaultGroundOffset * CROUCHING_HEIGHT_MULTIPLIER;
            else _currentYOffset = _defaultGroundOffset;
        }

        public void Crouch()
        {
            // Start crouching
            if (!IsCrouching)
            {
                if (_canCrouch)
                {
                    IsCrouching = true;
                    if (_character.IsRunning)
                        _character.Movement.StopRunning();
                }
            }
            // Stop crouching
            else
            {
                IsCrouching = false;
                StartCoroutine(CrouchCooldown());
            }
        }

        private IEnumerator CrouchCooldown()
        {
            _canCrouch = false;
            float elapsed = 0f;
            while (elapsed < _crouchCooldown)
            {
                elapsed += Time.fixedDeltaTime;
                yield return _waitForFixedUpdate;
            }
            _canCrouch = true;
        }

        public void TemporaryChangeHeight(float targetReduction, float duration)
        {
            _activeModifiers.Add(new HeightModifier
            {
                Multiplier = targetReduction,
                TimeRemaining = duration
            });
        }

        private void ProcessHeightModifiers()
        {
            if (_activeModifiers.Count == 0)
            {
                _currentTemporaryMultiplier = 1f;
                return;
            }

            float lowest = 1f;
            for (int i = _activeModifiers.Count - 1; i >= 0; i--)
            {
                _activeModifiers[i].TimeRemaining -= Time.fixedDeltaTime;

                if (_activeModifiers[i].TimeRemaining <= 0)
                {
                    _activeModifiers.RemoveAt(i);
                    continue;
                }
                if (_activeModifiers[i].Multiplier < lowest)
                    lowest = _activeModifiers[i].Multiplier;
            }
            _currentTemporaryMultiplier = lowest;
        }

        private void UpdateTorsoOffset()
        {
            LegState frontState = _character.LegsController.FrontLegState;
            LegState backState = _character.LegsController.BackLegState;

            bool isMoving = frontState.IsMoving() || backState.IsMoving();

            float targetOffset = _currentYOffset;

            if (!isMoving)
            {
                float frontExcess = GetFootExcess(frontState.Foot.position, frontState.FootTarget.position);
                float backExcess = GetFootExcess(backState.Foot.position, backState.FootTarget.position);

                float t = Mathf.Clamp01(Mathf.Max(frontExcess, backExcess) / _maxFootExcess);
                targetOffset = Mathf.Lerp(_currentYOffset, _currentYOffset * FOOT_EXCESS_HEIGHT_MULTIPLIER, t);
            }

            float groundY = Mathf.Min(
                GetGroundPosition(new(frontState.Foot.position.x, (frontState.Foot.position.y + frontState.DesiredFootPosition.y) / 2), 1, FOOT_HEIGHT).y,
                GetGroundPosition(new(backState.Foot.position.x, (backState.Foot.position.y + backState.DesiredFootPosition.y) / 2), 1, FOOT_HEIGHT).y
            );

            float targetTorsoY = groundY + targetOffset * _currentTemporaryMultiplier * Multiplier;

            DesiredTorsoPos = new(_character.LowerTorso.Rb.position.x, targetTorsoY);
            CurrentTorsoPos = new(_character.LowerTorso.Rb.position.x, groundY + _currentYOffset);
        }

        private void ApplyTorsoOffset()
        {
            float spring = _character.IsRunning ? _runSpringStrength : _walkSpringStrength;
            float damping = _character.IsRunning ? _runDampingCoefficient : _walkDampingCoefficient;

            float velocityY = _character.LowerTorso.Rb.linearVelocity.y;
            float forceY = (spring * (DesiredTorsoPos.y - _character.LowerTorso.Rb.position.y)) - (damping * velocityY);

            _character.LowerTorso.Rb.AddForce(Vector2.up * forceY, ForceMode2D.Force);
        }

        private float GetFootExcess(Vector2 foot, Vector2 target)
        {
            return Mathf.Max(0f, foot.y - target.y);
        }

        private Vector2 GetGroundPosition(Vector2 origin, float rayCastDistance, float groundOffset)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rayCastDistance, _targetLayers);
            if (hit.collider != null)
            {
                return hit.point + Vector2.up * groundOffset;
            }
            return origin;
        }

        void OnDrawGizmos()
        {
            if (_character == null) return;

            Gizmos.color = Color.yellow;

            Gizmos.DrawSphere(new(CurrentTorsoPos.x, CurrentTorsoPos.y - GIZMO_TORSO_OFFSET), .05f);

            // Back foot ray
            if (_character.BackFoot != null)
            {
                Vector2 origin = _character.BackFoot.Transform.position;
                Gizmos.DrawLine(origin, origin + Vector2.down * .5f);
            }

            // Front foot ray
            if (_character.FrontFoot != null)
            {
                Vector2 origin = _character.FrontFoot.Transform.position;
                Gizmos.DrawLine(origin, origin + Vector2.down * .5f);
            }
        }
    }
}
