using System.Collections;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class AIBrain : MonoBehaviour
    {
        private const float KEEP_DISTANCE_TOLERANCE = 2f;

        public AIBehavior Behavior;

        private Character _character;
        private Character _target;
        private Coroutine _behaviorCoroutine;
        private bool _wantsToShoot;
        private bool _isFiring;
        private float _wantsToShootRiseTime;
        private float _burstStartTime;
        private float _burstPauseUntil;
        private float _shootOffsetDeg;

        void Awake()
        {
            _character = GetComponent<Character>();
        }

        private void OnEnable()
        {
            _target = PlayerManager.Player;
            PlayerManager.OnPlayerSpawned += HandlePlayerSpawned;
            PlayerManager.OnPlayerDespawned += HandlePlayerDespawned;

            _behaviorCoroutine = StartCoroutine(BehaviorRoutine());
        }

        private void OnDisable()
        {
            PlayerManager.OnPlayerSpawned -= HandlePlayerSpawned;
            PlayerManager.OnPlayerDespawned -= HandlePlayerDespawned;

            if (_behaviorCoroutine != null)
            {
                StopCoroutine(_behaviorCoroutine);
                _behaviorCoroutine = null;
            }

            StopFiring();
        }

        void Update()
        {
            if (!_character.IsConscious || _character.IsDead) return;
            if (Behavior == null) return;

            UpdateAim();
            UpdateTrigger();
        }

        private void HandlePlayerSpawned(Character player) => _target = player;
        private void HandlePlayerDespawned(Character player) => _target = null;

        private IEnumerator BehaviorRoutine()
        {
            yield return new WaitUntil(() => Behavior != null);

            // Initial random offset spreads ticks across different enemies
            yield return new WaitForSeconds(Random.Range(0f, Behavior.TickInterval));

            while (true)
            {
                yield return new WaitForSeconds(Behavior.TickInterval);
                UpdateBehavior();
            }
        }

        private void UpdateBehavior()
        {
            if (Behavior == null)
                return;

            if (_character.IsDead || !_character.IsConscious || _target == null)
            {
                _character.Movement.moveInput = Vector2.zero;
                _wantsToShoot = false;
                return;
            }

            if (Behavior.ChaseTarget)
                UpdateMovementInput();

            if (Behavior.AttackTarget)
            {
                UpdateShootIntent();
                TryAutoReload();
            }
        }

        private void UpdateAim()
        {
            if (_target == null || !Behavior.AttackTarget || (_character.IsRunning && !_wantsToShoot))
            {
                // No target, face the movement direction
                _character.AimMode = AimMode.Direction;
                _character.AimDirectionInput = new Vector2(_character.Movement.moveInput.x, 0f);
                _character.AimDirection = _character.IsFacingRight ? 0f : 180f;
            }
            else
            {
                _character.AimMode = AimMode.WorldPoint;
                _character.AimWorldPoint = _wantsToShoot ? GetCleanShotPoint() : _target.MidTorso.Rb.position;
            }
        }

        // Aim along the cleared ray relative to the live target
        private Vector2 GetCleanShotPoint()
        {
            Gun gun = _character.WeaponManager.Gun;
            Vector2 origin = gun != null ? gun.ShootingPoint.transform.position : _character.UpperTorso.Rb.position;
            Vector2 toTorso = _target.MidTorso.Rb.position - origin;
            float baseAngle = Mathf.Atan2(toTorso.y, toTorso.x) * Mathf.Rad2Deg;
            float a = (baseAngle + _shootOffsetDeg) * Mathf.Deg2Rad;
            return origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * toTorso.magnitude;
        }

        private void UpdateTrigger()
        {
            if (!_wantsToShoot)
            {
                StopFiring();
                return;
            }

            // Hold fire while reacting
            if (Time.time - _wantsToShootRiseTime < Behavior.ReactionTime) return;

            // Trigger held for BurstDuration, released for BurstPause, repeat
            if (_isFiring)
            {
                if (Behavior.BurstDuration > 0f && Time.time - _burstStartTime >= Behavior.BurstDuration)
                {
                    StopFiring();
                    _burstPauseUntil = Time.time + Behavior.BurstPause;
                }
            }
            else if (Time.time >= _burstPauseUntil)
            {
                StartFiring();
            }
        }

        private void StartFiring()
        {
            Gun gun = _character.WeaponManager.Gun;

            if (gun != null)
            {
                gun.StartAttack();
                _isFiring = true;
                _burstStartTime = Time.time;
            }
        }

        private void StopFiring()
        {
            if (!_isFiring) return;

            Gun gun = _character.WeaponManager.Gun;

            if (gun != null)
            {
                gun.StopAttack();
                _isFiring = false;
            }
        }

        private void UpdateMovementInput()
        {
            Vector2 selfPos = _character.LowerTorso.Rb.position;
            Vector2 targetPos = _target.LowerTorso.Rb.position;

            float signedDx = targetPos.x - selfPos.x;
            float distance = Mathf.Abs(signedDx);

            if (distance < Behavior.KeepDistance - KEEP_DISTANCE_TOLERANCE)     // target too close - back off
            {
                if (_character.IsRunning)
                    _character.Movement.StopRunning();

                _character.Movement.moveInput = signedDx > 0f ? Vector2.left : Vector2.right;
            }
            else if (distance > Behavior.KeepDistance + KEEP_DISTANCE_TOLERANCE) // target too far - close in
            {
                bool shouldRun = distance > Behavior.WalkDistance;

                if (shouldRun && !_character.IsRunning)
                    _character.Movement.StartRunning();

                else if (!shouldRun && _character.IsRunning)
                    _character.Movement.StopRunning();

                _character.Movement.moveInput = signedDx > 0f ? Vector2.right : Vector2.left;
            }
            else                                                                // inside toolerance - hold position
            {
                if (_character.IsRunning)
                    _character.Movement.StopRunning();

                _character.Movement.moveInput = Vector2.zero;
            }
        }

        private void UpdateShootIntent()
        {
            float offset = 0f;
            Gun gun = _character.WeaponManager.Gun;

            bool shootIntent = Behavior.AttackTarget && !_target.IsDead && !_character.IsRunning
                && !_character.IsReloading && !_character.IsCycling && gun != null
                && gun.CurrentLoadedAmmo > 0 && TryFindCleanShot(out offset);

            if (shootIntent)
            {
                _shootOffsetDeg = offset;
                if (!_wantsToShoot) _wantsToShootRiseTime = Time.time;
            }
            _wantsToShoot = shootIntent;
        }

        // Fans rays at the target, bestOffsetDeg ends up on the most centered clean ray.
        private bool TryFindCleanShot(out float bestOffsetDeg)
        {
            bestOffsetDeg = 0f;

            Gun gun = _character.WeaponManager.Gun;
            if (gun == null) return false;

            Vector2 origin = gun.ShootingPoint.transform.position;
            Vector2 toTorso = _target.MidTorso.Rb.position - origin;

            float torsoDistance = toTorso.magnitude;
            if (torsoDistance > Behavior.MaxShootDistance || torsoDistance < 0.001f) return false;

            float baseAngle = Mathf.Atan2(toTorso.y, toTorso.x) * Mathf.Rad2Deg;
            int rayCount = Mathf.Max(1, Behavior.RayCount);
            float halfSpread = Behavior.RaySpreadAngle * 0.5f;
            // Even spacing across the cone; single ray sits dead center
            float step = rayCount > 1 ? Behavior.RaySpreadAngle / (rayCount - 1) : 0f;

            LayerMask hitMask = gun.Stats.HitMask;
            bool clean = false;
            float bestOffset = float.PositiveInfinity;

            for (int i = 0; i < rayCount; i++)
            {
                float offset = rayCount > 1 ? -halfSpread + step * i : 0f;
                float angleDeg = baseAngle + offset;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

                RaycastHit2D hit = Physics2D.Raycast(origin, dir, Behavior.MaxShootDistance, hitMask);
                BodyPart hitPart = hit.rigidbody != null ? hit.rigidbody.GetComponent<BodyPart>() : null;
                Character hitChar = hitPart != null ? hitPart.Character : null;

                if (hitChar == _target)
                {
                    clean = true;
                    // Prefer the most centered clean ray for the actual aim direction
                    if (Mathf.Abs(offset) < bestOffset)
                    {
                        bestOffset = Mathf.Abs(offset);
                        bestOffsetDeg = offset;
                    }
                }

#if UNITY_EDITOR
                Vector2 rayEnd = hit.collider != null ? hit.point : origin + dir * Behavior.MaxShootDistance;
                Color rayColor = hit.collider == null ? Color.gray : hitChar == _target ? Color.green : Color.red;
                Debug.DrawLine(origin, rayEnd, rayColor, Behavior.TickInterval);
#endif
            }

            return clean;
        }

        private void TryAutoReload()
        {
            Gun gun = _character.WeaponManager.Gun;

            if (gun != null && gun.CurrentLoadedAmmo <= 0 && !_character.IsReloading && !_character.IsCycling)
                gun.ReloadGun();
        }
    }
}
