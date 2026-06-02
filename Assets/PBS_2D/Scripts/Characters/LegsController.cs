using System.Collections;
using UnityEngine;

namespace PBS2D
{
    public class LegState
    {
        public Rigidbody2D UpperLeg, LowerLeg, Foot, FootTarget;
        public FootAlignment FootAlignment;

        public Vector2 DesiredFootPosition;
        public Coroutine MoveCoroutine;

        public bool IsLocked;

        public LegState(Rigidbody2D upperLeg, Rigidbody2D lowerLeg, Rigidbody2D foot, Rigidbody2D footTarget, FootAlignment footAlignment)
        {
            UpperLeg = upperLeg; LowerLeg = lowerLeg;
            Foot = foot; FootTarget = footTarget; FootAlignment = footAlignment;

            DesiredFootPosition = Foot.position;
            IsLocked = false;
            MoveCoroutine = null;
        }

        public bool IsMoving()
        {
            return MoveCoroutine != null;
        }
    }

    /// <summary>
    /// Manages the procedural animation and IK target placement for a character's legs and feet.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class LegsController : MonoBehaviour
    {
        [System.Serializable]
        public class StepSettings
        {
            public float
                FootSpeed = 8.5f,
                StepLength = 1.05f,
                MinStepHeight = .3f,

                StepUpHeightMultiplier = 1,
                StepLevelHeightMultiplier = .9f,
                StepDownHeightMultiplier = .8f,

                StepUpHeightDuration = .05f,
                StepLevelHeightDuration = .05f,
                StepDownHeightDuration = .1f,

                FootstepVolume = 1f;
        }

        // Foot offset used because of the isometric character view
        private const float INITIAL_FOOT_PLACE_DISTANCE = .35f;
        // Foot offset used because of the isometric character view
        private const float HEAD_RAYCAST_Y_OFFSET = -.5f;
        // Foot offset used because of the isometric character view
        private const float TORSO_RAYCAST_Y_OFFSET = -.35f;
        // Foot offset used because of the isometric character view
        private const float FOOT_ISOMETRIC_OFFSET = .15625f;
        // Foot offset used because of the isometric character view
        private const float FOOT_HEIGHT = .1875f;
        // Foot safe distance, used to check if the foot can be place without being inside a wall
        private const float FOOT_SAFE_DISTANCE = .09375f;
        // Number of rays used when checking the world for obstacles
        private const int OBSTACLE_CHECK_RAYS = 10;

        [Header("References")]
        [SerializeField]
        private LayerMask _groundLayers;

        [SerializeField]
        private AudioClip _stepClip;

        [Header("Settings")]
        private Vector2 _footFallbackOffset = new(0, .2f);

        private float _maxGroundCheckDistance = 4.5f;

        [SerializeField]
        private bool _stepOverObstacles = true;

        [Header("Idle Settings")]
        private float _idleForwardBalanceOffset = -0.1f;

        private float _idleBackBalanceOffset = 0.15f;

        [Header("Walk Settings")]
        public StepSettings WalkStepSettings = new()
        {
            FootSpeed = 8.5f,
            StepLength = 1.05f,
            MinStepHeight = .3f,
            StepUpHeightMultiplier = 1,
            StepLevelHeightMultiplier = .9f,
            StepDownHeightMultiplier = .8f,
            StepUpHeightDuration = .05f,
            StepLevelHeightDuration = .05f,
            StepDownHeightDuration = .1f,
            FootstepVolume = 1f
        };

        private float _walkForwardBalanceOffset = -0.15f;

        private float _walkBackBalanceOffset = -.1f;

        [Header("Run Settings")]
        public StepSettings RunStepSettings = new()
        {
            FootSpeed = 13f,
            StepLength = 1.9f,
            MinStepHeight = .4f,
            StepUpHeightMultiplier = 1,
            StepLevelHeightMultiplier = .8f,
            StepDownHeightMultiplier = .7f,
            StepUpHeightDuration = .05f,
            StepLevelHeightDuration = .05f,
            StepDownHeightDuration = .1f,
            FootstepVolume = 1f
        };

        private float _runForwardBalanceOffset = -.5f;

        [Header("Crouch Settings")]
        public StepSettings CrouchStepSettings = new()
        {
            FootSpeed = 8.5f,
            StepLength = 1.05f,
            MinStepHeight = .3f,
            StepUpHeightMultiplier = 1,
            StepLevelHeightMultiplier = .9f,
            StepDownHeightMultiplier = .8f,
            StepUpHeightDuration = .05f,
            StepLevelHeightDuration = .05f,
            StepDownHeightDuration = .1f,
            FootstepVolume = 1f
        };

        private float _crouchForwardBalanceOffset = -0.15f;

        private float _crouchBackBalanceOffset = .15f;

        private Character _character;
        public LegState
            FrontLegState, BackLegState;
        private readonly WaitForFixedUpdate _waitForFixedUpdate = new();

        #region MAIN METHODS

        void Awake()
        {
            _character = GetComponent<Character>();

            FrontLegState = new LegState(_character.UpperFrontLeg.Rb, _character.LowerFrontLeg.Rb, _character.FrontFoot.Rb, _character.FrontFootIKTarget, _character.FrontFootAlignment);
            BackLegState = new LegState(_character.UpperBackLeg.Rb, _character.LowerBackLeg.Rb, _character.BackFoot.Rb, _character.BackFootIKTarget, _character.BackFootAlignment);
        }

        void FixedUpdate()
        {
            if (!_character.IsConscious || !_character.IsGrounded) return;

            int frontLegBalanceState = 0;
            int backLegBalanceState = 0;

            UpdateLegsBalanceState(ref frontLegBalanceState, ref backLegBalanceState);

            if (frontLegBalanceState != 0)
                MoveLeg(FrontLegState, frontLegBalanceState > 0);

            if (backLegBalanceState != 0)
                MoveLeg(BackLegState, backLegBalanceState > 0);
        }

        public void StopLeg(LegState leg)
        {
            if (leg.IsMoving())
            {
                StopCoroutine(leg.MoveCoroutine);
                leg.MoveCoroutine = null;
            }

            leg.IsLocked = false;
        }

        public void MoveTargetToFeet()
        {
            FrontLegState.FootTarget.position = FrontLegState.Foot.position;
            BackLegState.FootTarget.position = BackLegState.Foot.position;
        }

        public void PlaceLegs(bool randomizeStance)
        {
            float offset = 0f;
            if (randomizeStance)
            {
                offset = INITIAL_FOOT_PLACE_DISTANCE;

                // 50% chance for the legs to be crossed
                if (Random.value < 0.5f) offset *= -1;
            }

            FrontLegState.DesiredFootPosition = GetGroundPosition(new Vector2(FrontLegState.Foot.position.x + offset, FrontLegState.Foot.position.y), 5f)
                                    ?? FrontLegState.Foot.position;

            BackLegState.DesiredFootPosition = GetGroundPosition(new Vector2(BackLegState.Foot.position.x - offset, BackLegState.Foot.position.y), 5f)
                                       ?? BackLegState.Foot.position;

            FrontLegState.MoveCoroutine = StartCoroutine(MoveFootTarget(FrontLegState, false));
            BackLegState.MoveCoroutine = StartCoroutine(MoveFootTarget(BackLegState, false));
        }

        public void InverseLegsOrientation()
        {
            (FrontLegState.UpperLeg, BackLegState.UpperLeg) = (BackLegState.UpperLeg, FrontLegState.UpperLeg);
            (FrontLegState.LowerLeg, BackLegState.LowerLeg) = (BackLegState.LowerLeg, FrontLegState.LowerLeg);
            (FrontLegState.Foot, BackLegState.Foot) = (BackLegState.Foot, FrontLegState.Foot);
            (FrontLegState.FootAlignment, BackLegState.FootAlignment) = (BackLegState.FootAlignment, FrontLegState.FootAlignment);
        }

        public void PlaceLegsAfterRun()
        {
            StartCoroutine(PlaceLegsAfterRunCor());
        }

        #endregion
        #region AUXILIARY METHODS

        private void MoveLeg(LegState leg, bool moveRight)
        {
            if (leg.IsMoving()) return;

            StepSettings stepSettings = GetStepSettings();

            float stepDistance = stepSettings.StepLength;
            float xPos = moveRight ? leg.UpperLeg.position.x + stepDistance : leg.UpperLeg.position.x - stepDistance;
            bool isWallInBetween = false;

            UpdateDesiredFootPos(leg, moveRight, new(xPos, _character.Head.Transform.position.y + HEAD_RAYCAST_Y_OFFSET), ref isWallInBetween);

            if (isWallInBetween)
            {
                if (leg.IsLocked) return;

                if (moveRight)
                {
                    if (leg.Foot.position.x < leg.DesiredFootPosition.x)
                        leg.MoveCoroutine = StartCoroutine(MoveFootTarget(leg, true));
                }
                else
                {
                    if (leg.Foot.position.x > leg.DesiredFootPosition.x)
                        leg.MoveCoroutine = StartCoroutine(MoveFootTarget(leg, true));
                }

                leg.IsLocked = true;
            }
            else
            {
                leg.MoveCoroutine = StartCoroutine(MoveFootTarget(leg, true));

                leg.IsLocked = false;
            }
        }

        private IEnumerator MoveFootTarget(LegState leg, bool moveInArc)
        {
            StepSettings stepSettings = GetStepSettings();

            Vector2 startPos = leg.Foot.position;
            Vector2 endPos = leg.DesiredFootPosition;

            UpdateIntermediatePoints(leg, out Vector2 liftPoint, out Vector2 descentPoint);

            float totalPathLength, liftDist = 0, glideDist = 0, descentDist = 0;

            // Account for the arc increased distance (not precise)
            if (moveInArc)
            {
                liftDist = Vector2.Distance(startPos, liftPoint) * 1.1f;
                glideDist = Vector2.Distance(liftPoint, descentPoint);
                descentDist = Vector2.Distance(descentPoint, endPos) * 1.1f;

                totalPathLength = liftDist + glideDist + descentDist;
            }
            else
            {
                totalPathLength = Vector2.Distance(endPos, startPos);
            }

            float elapsed = 0f;
            float duration = totalPathLength / stepSettings.FootSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.fixedDeltaTime;
                float tNormalized = elapsed / duration;
                float currentTraveled = tNormalized * totalPathLength;

                Vector2 newPos;

                if (moveInArc)
                {
                    if (currentTraveled > liftDist / 2 && currentTraveled < liftDist) leg.FootAlignment.Enabled = false;
                    else if (currentTraveled > descentDist / 2) leg.FootAlignment.Enabled = true;

                    newPos = GetNextArcPosition(currentTraveled, liftDist, glideDist, descentDist, startPos, liftPoint, descentPoint, endPos);
                }
                else
                {
                    newPos = GetNextLinearPosition(currentTraveled, totalPathLength, startPos, endPos);
                }

                leg.FootTarget.MovePosition(newPos);
                yield return _waitForFixedUpdate;
            }

            leg.FootTarget.MovePosition(endPos);

            if (moveInArc) AudioManager.Instance.PlaySound(_stepClip, endPos, volume: stepSettings.FootstepVolume);

            ApplyStepHeightReduction(startPos, endPos);

            leg.MoveCoroutine = null;
        }

        private StepSettings GetStepSettings()
        {
            if (_character.IsRunning)
            {
                return RunStepSettings;
            }
            else if (_character.HeightController.IsCrouching)
            {
                return CrouchStepSettings;
            }
            else
            {
                return WalkStepSettings;
            }
        }

        private float GetForwardBalanceOffset()
        {
            if (Mathf.Abs(_character.Movement.moveInput.x) < 0.1f) return _idleForwardBalanceOffset;
            else if (_character.IsRunning)
            {
                return _runForwardBalanceOffset;
            }
            else if (_character.HeightController.IsCrouching)
            {
                return _crouchForwardBalanceOffset;
            }
            else
            {
                return _walkForwardBalanceOffset;
            }
        }

        private float GetBackBalanceOffset()
        {
            if (Mathf.Abs(_character.Movement.moveInput.x) < 0.1f) return _idleBackBalanceOffset;
            else if (_character.IsRunning)
            {
                // Running backwards is not possible 
                return Mathf.Infinity;
            }
            else if (_character.HeightController.IsCrouching)
            {
                return _crouchBackBalanceOffset;
            }
            else
            {
                return _walkBackBalanceOffset;
            }
        }

        private void ApplyStepHeightReduction(Vector2 startPos, Vector2 endPos)
        {
            StepSettings stepSettings = GetStepSettings();

            // Steping in the same height
            if (Mathf.Abs(endPos.y - startPos.y) <= .1f)
            {
                _character.HeightController.TemporaryChangeHeight(stepSettings.StepLevelHeightMultiplier, stepSettings.StepLevelHeightDuration);
            }
            // Steping up
            else if (endPos.y > startPos.y)
            {
                _character.HeightController.TemporaryChangeHeight(stepSettings.StepUpHeightMultiplier, stepSettings.StepUpHeightDuration);
            }
            // Steping down
            else if (endPos.y < startPos.y)
            {
                _character.HeightController.TemporaryChangeHeight(stepSettings.StepDownHeightMultiplier, stepSettings.StepDownHeightDuration);
            }
        }

        private Vector2 GetNextArcPosition(float currentDistance, float liftDist, float glideDist, float descentDist, Vector2 startPos, Vector2 liftPoint, Vector2 descentPoint, Vector2 endPos)
        {
            Vector2 newPos;

            // Phase 1: Arc up from startPos to liftPoint
            if (currentDistance < liftDist)
            {
                float t = currentDistance / liftDist;
                float posX = Mathf.Lerp(startPos.x, liftPoint.x, t);
                float posY = Mathf.Lerp(startPos.y, liftPoint.y, Mathf.Sin(t * (Mathf.PI / 2)));

                newPos = new(posX, posY);
            }
            // Phase 2: Linear from liftPoint to descentPoint
            else if (currentDistance < liftDist + glideDist)
            {
                float t = (currentDistance - liftDist) / glideDist;
                newPos = Vector2.Lerp(liftPoint, descentPoint, t);
            }
            // Phase 3: Arc down from descentPoint to endPos
            else
            {
                float t = Mathf.Clamp01((currentDistance - liftDist - glideDist) / descentDist);
                float posX = Mathf.Lerp(descentPoint.x, endPos.x, t);
                float posY = Mathf.Lerp(descentPoint.y, endPos.y, 1f - Mathf.Cos(t * (Mathf.PI / 2)));

                newPos = new(posX, posY);
            }

            return newPos;
        }

        private Vector2 GetNextLinearPosition(float currentDistance, float totalDistance, Vector2 startPos, Vector2 endPos)
        {
            Vector2 newPos;

            float t = currentDistance / totalDistance;
            newPos = Vector2.Lerp(startPos, endPos, t);

            return newPos;
        }

        private void UpdateIntermediatePoints(LegState leg, out Vector2 liftPoint, out Vector2 descentPoint)
        {
            StepSettings stepSettings = GetStepSettings();
            Vector2 startPos = leg.Foot.position;
            Vector2 endPos = leg.DesiredFootPosition;
            bool moveRight = startPos.x < endPos.x;

            float firstX = float.NaN;
            float lastX = float.NaN;
            float highestPointY = -Mathf.Infinity;

            // Use a fixed height or the leg's current height to scan downward
            float scanHeight = leg.UpperLeg.position.y;

            bool foundObstacle = FindObstacle(Vector2.Distance(startPos, endPos), startPos, endPos);

    #if UNITY_EDITOR
            Debug.DrawLine(startPos, endPos, foundObstacle ? Color.red : Color.green, .15f);
    #endif

            if (foundObstacle && _stepOverObstacles)
            {
                for (int i = 0; i <= OBSTACLE_CHECK_RAYS; i++)
                {
                    float t = (float)i / OBSTACLE_CHECK_RAYS;
                    float currX = Mathf.Lerp(startPos.x, endPos.x, t);

                    Vector2 rayOrigin = new(currX, scanHeight);
                    RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 10f, _groundLayers);

                    if (hit.collider != null)
                    {
                        // If the hit point is higher than the floor, it's an obstacle
                        float floorLevel = Mathf.Min(startPos.y, endPos.y);
                        if (hit.point.y > floorLevel + 0.1f)
                        {
                            if (float.IsNaN(firstX)) firstX = hit.point.x;
                            lastX = hit.point.x;
                            highestPointY = Mathf.Max(highestPointY, hit.point.y);
                        }
                    }
                }
            }

            if (!float.IsNaN(firstX) && !float.IsNaN(lastX))
            {
                // Set points ABOVE the obstacle
                float clearHeight = highestPointY + 0.2f;
                if (moveRight)
                {
                    liftPoint = new Vector2(Mathf.Max(startPos.x, firstX - 0.1f), clearHeight);
                    descentPoint = new Vector2(Mathf.Min(endPos.x, lastX + 0.1f), clearHeight);
                }
                else
                {
                    liftPoint = new Vector2(Mathf.Min(startPos.x, firstX + 0.1f), clearHeight);
                    descentPoint = new Vector2(Mathf.Max(endPos.x, lastX - 0.1f), clearHeight);
                }
            }
            else
            {
                // Simple arc: Midpoint is slightly raised
                Vector2 middlePoint = Vector2.Lerp(startPos, endPos, 0.5f);
                liftPoint = descentPoint = new Vector2(middlePoint.x, middlePoint.y + stepSettings.MinStepHeight);
            }
        }

        private bool FindObstacle(float distance, Vector2 startPos, Vector2 endPos)
        {
            if (!Physics2D.Linecast(startPos, endPos, _groundLayers)) return false;

            bool moveRight = startPos.x < endPos.x;
            RaycastHit2D startGround = Physics2D.Raycast(startPos, Vector2.down, 0.5f, _groundLayers);
            RaycastHit2D endGround = Physics2D.Raycast(endPos, Vector2.down, 0.5f, _groundLayers);

            Vector2 startNormal = startGround.collider != null ? startGround.normal : Vector2.up;
            Vector2 startDir = moveRight ? new(startNormal.y, -startNormal.x) : new(-startNormal.y, startNormal.x);

            Vector2 endNormal = endGround.collider != null ? endGround.normal : Vector2.up;
            Vector2 endDir = moveRight ? new(-endNormal.y, endNormal.x) : new(endNormal.y, -endNormal.x);

            bool startPosObstacle = Physics2D.Raycast(startPos, startDir, distance, _groundLayers);
            bool endPosObstacle = Physics2D.Raycast(endPos, endDir, distance, _groundLayers);

            return startPosObstacle && endPosObstacle;
        }

        private IEnumerator PlaceLegsAfterRunCor()
        {
            // SAFEGUARD IF BOTH ARE MOVING (WAITS FOR ONE TO FINISH)
            if (FrontLegState.IsMoving() && BackLegState.IsMoving())
                yield return new WaitWhile(() => FrontLegState.IsMoving() && BackLegState.IsMoving());

            LegState movingLeg = FrontLegState.IsMoving() ? FrontLegState : BackLegState;
            LegState stoppedLeg = FrontLegState.IsMoving() ? BackLegState : FrontLegState;

            yield return new WaitWhile(() => movingLeg.IsMoving());

            float footOffset = Random.Range(.2f, .4f);

            if (movingLeg.DesiredFootPosition.x < stoppedLeg.DesiredFootPosition.x)
                footOffset *= -1;

            UpdateDesiredFootPos(stoppedLeg, new(stoppedLeg.UpperLeg.position.x - footOffset, _character.LowerTorso.Transform.position.y + 1f));
            stoppedLeg.MoveCoroutine = StartCoroutine(MoveFootTarget(stoppedLeg, false));

            UpdateDesiredFootPos(movingLeg, new(movingLeg.UpperLeg.position.x + footOffset, _character.LowerTorso.Transform.position.y + 1f));
            movingLeg.MoveCoroutine = StartCoroutine(MoveFootTarget(movingLeg, false));
        }

        private Vector2? GetGroundPosition(Vector2 origin, float maxDistance)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, maxDistance, _groundLayers);
            if (hit.collider != null)
            {
                return hit.point + Vector2.up * FOOT_HEIGHT;
            }

            return null;
        }

        private bool CanPlaceFoot(Vector2 desiredPos)
        {
            return desiredPos.y < _character.HeightController.CurrentTorsoPos.y + TORSO_RAYCAST_Y_OFFSET;
        }

        private void UpdateDesiredFootPos(LegState leg, bool moveRight, Vector2 probe, ref bool isWallInBetween)
        {
            StepSettings stepSettings = GetStepSettings();
            Vector2 upperLegPos = leg.UpperLeg.position;
            Vector2 fallback = probe + _footFallbackOffset;
            Vector2 desiredPos = GetGroundPosition(probe, _maxGroundCheckDistance) ?? fallback;

            // Horizontal check, from the upper leg position to the desired position (In a horizontal line)
            RaycastHit2D horizontalRay = Physics2D.Linecast(upperLegPos, new(desiredPos.x, upperLegPos.y), _groundLayers);
            bool isBlockedHorizontal = horizontalRay;
    #if UNITY_EDITOR
            Debug.DrawLine(upperLegPos, new(desiredPos.x, upperLegPos.y), isBlockedHorizontal ? Color.red : Color.white, 0.15f);
    #endif

            // Direct check, from the upper leg position to the desired position
            RaycastHit2D directRay = Physics2D.Linecast(upperLegPos, desiredPos, _groundLayers);
            bool isBlockedDirect = directRay;
    #if UNITY_EDITOR
            Debug.DrawLine(upperLegPos, desiredPos, isBlockedDirect ? Color.red : Color.white, 0.15f);
    #endif

            // Wall between desiredPos and the character 
            if ((isBlockedHorizontal && isBlockedDirect) || !CanPlaceFoot(desiredPos))
            {
                if (!isWallInBetween)
                {
                    isWallInBetween = true;

                    // Try with a smaller step distance
                    float stepDistance = stepSettings.StepLength;
                    float xPos = moveRight ? leg.UpperLeg.position.x + stepDistance / 2 : leg.UpperLeg.position.x - stepDistance / 2;
                    UpdateDesiredFootPos(leg, moveRight, new(xPos, _character.Head.Transform.position.y), ref isWallInBetween);
                }
                else
                {
                    // If we detect a wall, try to move to the edge of the wall
                    if (isBlockedHorizontal)
                    {
                        if (moveRight)
                        {
                            float x = isBlockedDirect ? horizontalRay.point.x - FOOT_SAFE_DISTANCE : BackLegState.UpperLeg.position.x;
                            desiredPos = GetGroundPosition(new(x, horizontalRay.point.y), _maxGroundCheckDistance) ?? fallback;
                        }
                        else
                        {
                            float x = isBlockedDirect ? horizontalRay.point.x + FOOT_SAFE_DISTANCE : FrontLegState.UpperLeg.position.x;
                            desiredPos = GetGroundPosition(new(x, horizontalRay.point.y), _maxGroundCheckDistance) ?? fallback;
                        }

                        leg.DesiredFootPosition = desiredPos;
                    }
                    else
                    {
                        UpdateDesiredFootPos(leg, new(leg.UpperLeg.position.x, _character.Head.Transform.position.y));
                    }
                }
            }
            else
            {
                isWallInBetween = false;

                leg.DesiredFootPosition = desiredPos;
            }
        }

        private void UpdateDesiredFootPos(LegState leg, Vector2 probe)
        {
            Vector2 fallback = probe + _footFallbackOffset;

            Vector2 desiredPos = GetGroundPosition(probe, _maxGroundCheckDistance) ?? fallback;

            if (!CanPlaceFoot(desiredPos))
            {
                desiredPos = GetGroundPosition(leg.UpperLeg.position, _maxGroundCheckDistance) ?? fallback;
            }

            leg.DesiredFootPosition = desiredPos;
        }

        private void UpdateLegsBalanceState(ref int frontLegBalanceState, ref int backLegBalanceState)
        {
            int dir = _character.IsFacingRight ? 1 : -1;
            float torsoX = _character.LowerTorso.Transform.position.x;
            float frontX = FrontLegState.DesiredFootPosition.x + FOOT_ISOMETRIC_OFFSET;
            float backX = BackLegState.DesiredFootPosition.x - FOOT_ISOMETRIC_OFFSET;

            float forwardBalanceOffset = GetForwardBalanceOffset();
            float backBalanceOffset = GetBackBalanceOffset();

            bool legsNormalStance = (backX - frontX) * dir > 0;
            float leadingX = legsNormalStance ? frontX : backX;
            float trailingX = legsNormalStance ? backX : frontX;

            if ((torsoX - trailingX) * dir > forwardBalanceOffset)
            {
                int balanceState = _character.IsFacingRight ? 1 : -1;
                (legsNormalStance ? ref frontLegBalanceState : ref backLegBalanceState) = balanceState;
            }

            else if ((leadingX - torsoX) * dir > backBalanceOffset)
            {
                int balanceState = _character.IsFacingRight ? -1 : 1;
                (legsNormalStance ? ref backLegBalanceState : ref frontLegBalanceState) = balanceState;
            }
        }

        #endregion

        void OnDrawGizmos()
        {
            if (_character == null) return;

            if (FrontLegState != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(FrontLegState.DesiredFootPosition, 0.05f);
            }

            if (BackLegState != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(BackLegState.DesiredFootPosition, 0.05f);
            }
        }
    }
}
