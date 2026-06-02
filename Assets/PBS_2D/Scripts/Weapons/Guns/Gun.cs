using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PBS2D
{
    public class Gun : Weapon
    {
        private const float MAX_INACCURACY = 10f;
        private const float TIME_BETWEEN_SHOOTS_SEMI = .3f;
        private const float BARREL_SMOKE_INCREMENT = 0.05f;
        private const float CAMERA_SHAKE_DURATION = 0.1f;
        private const float RECOIL_KICKBACK_DURATION = .025f;
        // Lets you trigger the reload animation even when the mag is already full
        private const bool RELOAD_WHEN_FULL = false;

        [Header("Gun References")]
        [Tooltip("Where the gun shoots from")]
        public GameObject ShootingPoint;

        [Tooltip("Where the casings are ejected from")]
        public Transform EjectionPoint;

        [Tooltip("Where the hand is placed when Reloading")]
        public Transform ReloadHandPoint;

        [Tooltip("Where the hand is placed when cycling")]
        public Transform CycleHandPoint;

        [Tooltip("The bolt/slider of the gun that moves to release the casing")]
        public Transform Bolt;

        [Tooltip("The forend/pump of the gun (pump-action only)")]
        public Transform Forend;

        [Tooltip("Prefab for the detachable magazine")]
        public GameObject MagPrefab;

        [Tooltip("Prefab for individual shells (shell-reload only)")]
        public GameObject ShellPrefab;

        [Tooltip("The current magazine GameObject attached to the gun")]
        public GameObject CurrentMag;

        [Header("Gun Config")]
        [Tooltip("ScriptableObject defining weapon parameters")]
        public GunStats Stats;

        [Tooltip("ScriptableObject defining gun sound effects")]
        public GunAudioConfig AudioConfig;

        [Tooltip("ScriptableObject defining gun visual effects")]
        public GunEffectConfig EffectConfig;

        [Tooltip("ScriptableObject defining projectile impact effects")]
        public GunImpactConfig ImpactConfig;

        [Tooltip("Weapon hold offset when aiming down")]
        public WeaponHoldOffset AimDownOffset;

        [Tooltip("Weapon hold offset during reload")]
        public WeaponHoldOffset ReloadOffset;

        [HandIndex]
        public int ReloadHandIdx = 6;

        [HandIndex]
        public int CycleHandIdx = 3;

        // Cycle
        [Header("Cycle Settings")]
        [Tooltip("Whether the bolt locks open when the last round is fired")]
        public bool OpenBoltWhenEmpty = false;

        public bool BoltClosedSafeGuard = true;

        [Header("Reload Settings")]
        public float CycleHandMoveDistance = -.25f;

        public float BoltMoveAmount = .1875f;

        [Tooltip("How much of the magazine is inside the gun")]
        public float MagInsertDepth = .0625f;

        [Tooltip("How deep the shell is inserted into the gun")]
        public float ShellInsertDepth = .1875f;

        public Vector2 ShellHandOffset = new(.15625f, .09375f);

        public Vector2 HandToBodyOffset = new(-.125f, 0f);

        // Public state (accessed by Cycle, Reload, WeaponUI)
        [System.NonSerialized] public int CurrentLoadedAmmo;
        [System.NonSerialized] public int CurrentReserveAmmo;
        [System.NonSerialized] public GunTriggerMode CurrentFireMode;
        [System.NonSerialized] public Vector2 BoltInitialPos;
        [System.NonSerialized] public bool IsBoltOpened = false;
        [System.NonSerialized] public bool IsCycled = true;
        [System.NonSerialized] public bool AbortReload = false;
        [System.NonSerialized] public Coroutine BoltCoroutine = null;
        [System.NonSerialized] public Coroutine CycleCoroutine = null;
        [System.NonSerialized] public Rigidbody2D MagRB;
        [System.NonSerialized] public SpriteRenderer NewShell;

        // Private state
        private LayerMask _hitMask;
        private float _timeBetweenShots;
        private float _minShotAngle, _maxShotAngle;
        private float _barrelSmokeDissipation = .1f;
        private float _barrelSmokeMult;
        private float _nextFireTime = 0f;
        private ParticleSystem _barrelSmoke;
        private bool _isChamberEmpty = false;
        private Vector2 _magInitialPos;
        private Vector2 _forendInitialPos;
        private int _groundLayer;
        private Coroutine _attackCoroutine = null;

        protected override void Awake()
        {
            base.Awake();

            _timeBetweenShots = 60f / Stats.RoundsPerMinute;
            CurrentLoadedAmmo = Stats.MaxLoadedAmmo;
            CurrentReserveAmmo = Stats.MaxReserveAmmo;

            _hitMask = Stats.HitMask;

            if (Bolt != null)
                BoltInitialPos = Bolt.localPosition;

            if (CurrentMag != null)
                _magInitialPos = CurrentMag.transform.localPosition;

            if (Forend != null)
                _forendInitialPos = Forend.transform.localPosition;

            if (Stats.CycleType == GunCycleType.SelfCycle)
            {
                if (Stats.HasFireMode(GunTriggerMode.Auto))
                    CurrentFireMode = GunTriggerMode.Auto;
                else if (Stats.HasFireMode(GunTriggerMode.Semi))
                    CurrentFireMode = GunTriggerMode.Semi;
                else if (Stats.HasFireMode(GunTriggerMode.Burst))
                    CurrentFireMode = GunTriggerMode.Burst;
            }

            _barrelSmoke = ShootingPoint.GetComponent<ParticleSystem>();
            _barrelSmokeMult = 0;

            _groundLayer = LayerMask.NameToLayer("Ground");

            _maxShotAngle = (1f - Stats.Accuracy / 100f) * MAX_INACCURACY;
            _minShotAngle = -_maxShotAngle;

            if (CurrentMag != null)
                MagRB = CurrentMag.GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            if (Settings.LockActions || _barrelSmoke == null) return;

            _barrelSmokeMult -= _barrelSmokeDissipation * Time.deltaTime;
            _barrelSmokeMult = Mathf.Clamp(_barrelSmokeMult, 0f, 1f);
            var emission = _barrelSmoke.emission;
            emission.enabled = true;
            emission.rateOverTimeMultiplier = _barrelSmokeMult * 40;
        }

        public override void StartAttack()
        {
            if (character == null || !character.IsConscious) return;

            IsAttacking = true;

            if (_attackCoroutine == null)
                _attackCoroutine = StartCoroutine(AttackCoroutine());
        }

        public override void StopAttack()
        {
            IsAttacking = false;
        }

        private IEnumerator AttackCoroutine()
        {
            // Whether the user can hold down the attack to keep shooting semi auto weapons
            bool holdDownSemi = character.AimMode == AimMode.Direction;

            if (CanPlayTriggerSound())
            {
                AudioManager.Instance.PlaySound(AudioConfig.PullTriggerClip, transform.position);
            }

            while (IsAttacking)
            {
                if (!character.IsConscious) break;
                if (character.IsCycling) break;

                if (character.IsReloading)
                {
                    AbortReload = true;
                    if (!holdDownSemi) break;
                    yield return null;
                    continue;
                }

                if (_isChamberEmpty && CurrentLoadedAmmo <= 0) break;

                if (IsCycled)
                {
                    yield return ProcessFiringCycle();

                    if ((!holdDownSemi && CurrentLoadedAmmo <= 0) || (!holdDownSemi && (CurrentFireMode == GunTriggerMode.Semi || (CurrentFireMode == GunTriggerMode.Burst && Stats.SelfCycleConfig.IsBurstSemiAuto) || Stats.CycleType != GunCycleType.SelfCycle)))
                    {
                        break;
                    }

                    yield return WaitBetweenShots();
                }
                else
                {
                    if (Stats.CycleType == GunCycleType.BoltAction)
                    {
                        yield return CycleCoroutine = StartCoroutine(Cycle.CycleBolt(this, !_isChamberEmpty));
                    }
                    else if (Stats.CycleType == GunCycleType.PumpAction)
                    {
                        yield return CycleCoroutine = StartCoroutine(Cycle.CycleForend(this, !_isChamberEmpty));
                    }
                    _isChamberEmpty = CurrentLoadedAmmo == 0;
                }

                if (!holdDownSemi && Stats.CycleType != GunCycleType.SelfCycle) break;

                yield return null;
            }

            _attackCoroutine = null;
        }

        private IEnumerator ProcessFiringCycle()
        {
            if (!CanShoot()) yield break;

            if (CurrentFireMode == GunTriggerMode.Burst)
            {
                int shots = Mathf.Min(CurrentLoadedAmmo, Stats.SelfCycleConfig.BurstLength);
                for (int i = 0; i < shots; i++)
                {
                    if (character.IsReloading) break;
                    Shoot();
                    yield return new WaitForSeconds(_timeBetweenShots);
                }
                yield return new WaitForSeconds(Stats.SelfCycleConfig.BurstInterval);
                _nextFireTime = Time.time;
            }
            else
            {
                Shoot();
                _nextFireTime = Time.time + _timeBetweenShots;
            }
        }

        public void SwitchFireMode()
        {
            CurrentFireMode = Stats.NextFiringMode(CurrentFireMode);
        }

        private IEnumerator WaitBetweenShots()
        {
            if (Stats.CycleType == GunCycleType.SelfCycle && (CurrentFireMode == GunTriggerMode.Auto || (CurrentFireMode == GunTriggerMode.Burst && !Stats.SelfCycleConfig.IsBurstSemiAuto)))
            {
                yield return null;
            }
            else
            {
                yield return new WaitForSecondsRealtime(TIME_BETWEEN_SHOOTS_SEMI);
            }
        }

        private bool CanPlayTriggerSound()
        {
            return (CurrentLoadedAmmo == 0 && !character.IsRunning) || CanShoot();
        }

        private bool CanShoot()
        {
            return !character.IsRunning
                && !character.IsReloading
                && !character.IsCycling
                && IsCycled
                && CurrentLoadedAmmo > 0
                && Time.time >= _nextFireTime;
        }

        public void ReloadGun()
        {
            if (character.IsReloading || character.IsCycling || CurrentReserveAmmo <= 0) return;
            if (!RELOAD_WHEN_FULL && CurrentLoadedAmmo >= Stats.MaxLoadedAmmo) return;

            switch (Stats.ReloadType)
            {
                case GunReloadType.Magazine:
                    StartCoroutine(Reload.ReloadMagazine(this));

                    break;
                case GunReloadType.Shell:
                    if (CurrentLoadedAmmo < Stats.MaxLoadedAmmo)
                        StartCoroutine(Reload.ReloadShell(this));

                    break;
            }
        }

        public void AbortAllActions()
        {
            StopAllCoroutines();

            IsAttacking = false;
            AbortReload = false;
            _attackCoroutine = null;
            BoltCoroutine = null;
            CycleCoroutine = null;
        }

        public override void Drop()
        {
            base.Drop();

            AbortAllActions();

            switch (Stats.CycleType)
            {
                case GunCycleType.SelfCycle:
                    Bolt.localPosition = (OpenBoltWhenEmpty && CurrentLoadedAmmo == 0)
                        ? BoltInitialPos + new Vector2(-BoltMoveAmount, 0f)
                        : BoltInitialPos;
                    break;
                case GunCycleType.BoltAction:
                    Bolt.localPosition = BoltInitialPos;
                    break;
                case GunCycleType.PumpAction:
                    Forend.localPosition = _forendInitialPos;
                    break;
            }

            switch (Stats.ReloadType)
            {
                case GunReloadType.Magazine:
                    CurrentMag.transform.SetParent(transform);
                    CurrentMag.transform.SetLocalPositionAndRotation(_magInitialPos, Quaternion.identity);
                    CurrentMag.GetComponent<SpriteRenderer>().enabled = true;
                    break;
                case GunReloadType.Shell:
                    if (NewShell != null)
                        Destroy(NewShell);
                    break;
            }
        }

        protected void Shoot()
        {
            _barrelSmokeMult = Mathf.Min(1f, _barrelSmokeMult + BARREL_SMOKE_INCREMENT);

            CameraManager.Instance.Shake(EffectConfig.CameraShakeAmount, CAMERA_SHAKE_DURATION);
            AudioManager.Instance.PlaySound(AudioConfig.GunMechClip, transform.position);
            AudioManager.Instance.PlaySound(AudioConfig.ShootClip, transform.position);

            for (int i = 0; i < Stats.ProjectilesPerShot; i++)
            {
                Vector2 dir = GetRandomBulletDir();
                StartCoroutine(ProcessBullet(dir));
            }

            float muzzleAngle = transform.eulerAngles.z + (transform.lossyScale.x < 0 ? 90 : -90);

            SpawnMuzzleEffect(EffectConfig.MuzzleSmoke, muzzleAngle);
            SpawnMuzzleEffect(EffectConfig.MuzzleFlash, muzzleAngle);

            CurrentLoadedAmmo--;
            if (IsEquippedByPlayer) WeaponUI.Instance.UpdateAmmoUI();
            ApplyRecoil();
            IsCycled = false;
            HandleCycle();
        }

        private IEnumerator ProcessBullet(Vector2 dir)
        {
            Vector2 currentPos = ShootingPoint.transform.position;
            int remainingPenetration = Stats.MaxPenetration;
            float travelledDistance = 0f;
            HashSet<Character> victims = new();
            float projectileSpeed = Stats.ProjectileSpeed.GetValue();
            float range = Stats.Range.GetValue();

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            GameObject bulletObj = null;
            if (EffectConfig.BulletPrefab != null && Settings.TracerRounds)
                bulletObj = ObjectPoolManager.SpawnObject(EffectConfig.BulletPrefab, currentPos, Quaternion.Euler(0, 0, angle), PoolType.Projectile);

            while (travelledDistance < range)
            {
                // Clamp ray to remaining range
                float rayLength = Mathf.Min(projectileSpeed * Time.deltaTime, range - travelledDistance);

                RaycastHit2D[] hits = Physics2D.RaycastAll(currentPos, dir, rayLength, _hitMask);
                bool stoppedByObstacle = false;

                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject.layer == _groundLayer)
                    {
                        float hitAngle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg;
                        ObjectPoolManager.SpawnObject(ImpactConfig.GroundImpact, hit.point, Quaternion.Euler(0f, 0f, hitAngle), PoolType.Effect);
                        currentPos = hit.point;
                        stoppedByObstacle = true;
                        break;
                    }

                    if (hit.collider.TryGetComponent<BodyPart>(out var bodyPart)
                        && bodyPart.Character != character
                        && !victims.Contains(bodyPart.Character))
                    {
                        victims.Add(bodyPart.Character);

                        // AI accuracy: bullet phases through this character on a missed roll.
                        if (!character.RollHit()) continue;

                        BulletImpact.HandleHit(this, hit, travelledDistance, remainingPenetration);

                        // Reduce range as a penalty for penetration
                        travelledDistance += (range - travelledDistance) * Stats.PenetrationRangeReduction;
                        remainingPenetration--;

                        if (remainingPenetration <= 0)
                        {
                            currentPos = hit.point;
                            stoppedByObstacle = true;
                            break;
                        }
                    }
                }

                if (stoppedByObstacle) break;

                currentPos += dir * rayLength;
                travelledDistance += rayLength;

                if (bulletObj != null) bulletObj.transform.position = currentPos;

                yield return null;
            }

            if (bulletObj != null) ObjectPoolManager.ReturnObjectToPool(bulletObj);
        }

        private Vector2 GetRandomBulletDir()
        {
            // Calculate the bullet angle
            float randomAngle = Random.Range(_minShotAngle, _maxShotAngle);
            float baseAngleDeg = ShootingPoint.transform.eulerAngles.z + (character.IsFacingRight ? 0f : 180f);
            float finalAngleDeg = baseAngleDeg + randomAngle;
            float rad = finalAngleDeg * Mathf.Deg2Rad;

            // Transform that angle to a direction
            return new(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private void SpawnMuzzleEffect(GameObject prefab, float angle)
        {
            if (prefab != null)
                ObjectPoolManager.SpawnObject(prefab, ShootingPoint.transform.position, Quaternion.Euler(0, 0, angle), PoolType.Effect);
        }

        public void HandleCycle()
        {
            // Gun is already cycling, skip cycle animation
            if (BoltCoroutine != null) return;

            switch (Stats.CycleType)
            {
                case GunCycleType.SelfCycle:
                    BoltCoroutine = (OpenBoltWhenEmpty && CurrentLoadedAmmo == 0)
                        ? StartCoroutine(Cycle.PullBolt(this, true))
                        : StartCoroutine(Cycle.MoveBolt(this));
                    break;

                case GunCycleType.BoltAction:
                case GunCycleType.PumpAction:
                    if (Settings.AutoCycle && character.AimMode != AimMode.Direction)
                    {
                        CycleCoroutine = (Stats.CycleType == GunCycleType.BoltAction)
                            ? StartCoroutine(Cycle.CycleBolt(this, true))
                            : StartCoroutine(Cycle.CycleForend(this, true));
                    }
                    break;
            }
        }

        private void ApplyRecoil()
        {
            Vector2 recoilDirection = character.IsFacingRight ? -ShootingPoint.transform.right : ShootingPoint.transform.right;
            character.UpperTorso.Rb.AddForce(recoilDirection * Stats.RecoilForce, ForceMode2D.Impulse);
            StartCoroutine(MoveGunBackwards());
        }

        private IEnumerator MoveGunBackwards()
        {
            RelativeJoint2D joint = character.WeaponManager.WeaponHolder.GetComponent<RelativeJoint2D>();
            float direction = character.IsFacingRight ? 1f : -1f;
            float kickback = EffectConfig.KickBackDistance * direction;

            joint.linearOffset = new(joint.linearOffset.x - kickback, joint.linearOffset.y);

            yield return new WaitForSeconds(RECOIL_KICKBACK_DURATION);

            joint.linearOffset = new(joint.linearOffset.x + kickback, joint.linearOffset.y);
        }

        private void OnDrawGizmos()
        {
            if (ShootingPoint == null || Stats == null || !IsEquippedByPlayer) return;

            float inaccuracy = Mathf.Clamp01(1f - (Stats.Accuracy / 100f));
            float maxAngleSpread = MAX_INACCURACY * inaccuracy;

            float baseAngleDeg = ShootingPoint.transform.eulerAngles.z + (character.IsFacingRight ? 0f : 180f);

            float range = Stats.Range.GetValue();
            Vector2 origin = ShootingPoint.transform.position;

            static Vector2 DirFromDeg(float deg)
            {
                float rad = deg * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            Gizmos.color = Color.yellow;
            float topDeg = baseAngleDeg + maxAngleSpread;
            float bottomDeg = baseAngleDeg - maxAngleSpread;

            Vector2 topDir = DirFromDeg(topDeg);
            Vector2 botDir = DirFromDeg(bottomDeg);

            Gizmos.DrawLine(origin, origin + topDir * range);
            Gizmos.DrawLine(origin, origin + botDir * range);

            const int steps = 24;
            Vector2 prev = origin + topDir * range;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float a = Mathf.Lerp(topDeg, bottomDeg, t);
                Vector2 p = origin + DirFromDeg(a) * range;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}