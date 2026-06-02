using UnityEngine;
using UnityEngine.U2D.IK;

namespace PBS2D
{
    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(WeaponManager), typeof(CharacterHealth), typeof(CharacterMovement))]
    [RequireComponent(typeof(LegsController), typeof(BodyPhysicsController), typeof(TorsoHeightController))]
    [RequireComponent(typeof(CharacterSkin), typeof(CharacterRotation))]
    public class Character : MonoBehaviour
    {
        [Header("Character")]
        public bool IsPlayer;
        public AIBehavior AIBehavior;
        public SkinConfig Skin;

        [Header("Physics")]
        public LayerMask GroundLayer;

        [Header("IK")]
        public IKManager2D IKManager;
        public GameObject IKTargets;

        [Header("Body Parts")]
        public BodyPartRef Head;
        public BodyPartRef UpperTorso, MidTorso, LowerTorso;
        public BodyPartRef
            UpperFrontArm, LowerFrontArm,
            UpperBackArm, LowerBackArm;
        public BodyPartRef
            UpperFrontLeg, LowerFrontLeg, FrontFoot,
            UpperBackLeg, LowerBackLeg, BackFoot;

        [Header("Joints")]
        public HingeJoint2D HeadHinge;
        public HingeJoint2D
            UpperTorsoHinge, MidTorsoHinge,
            RestHinge, AimHinge;
        public HingeJoint2D
            UpperFrontArmHinge, LowerFrontArmHinge,
            UpperBackArmHinge, LowerBackArmHinge;
        public HingeJoint2D
            UpperFrontLegHinge, LowerFrontLegHinge, FrontFootHinge,
            UpperBackLegHinge, LowerBackLegHinge, BackFootHinge;
        public FixedJoint2D FrontHandFixedJoint, BackHandFixedJoint;

        [Header("Hands & Feet")]
        public GameObject FrontHand;
        public GameObject BackHand;
        public Rigidbody2D FrontHandIKTarget, BackHandIKTarget;
        public Rigidbody2D FrontFootIKTarget, BackFootIKTarget;
        public Transform FrontFootTarget, BackFootTarget;
        public FootAlignment FrontFootAlignment, BackFootAlignment;
        public BodyPart FrontFootBodyPart, BackFootBodyPart;

        [Header("Ground Detection")]
        public GroundDetection FrontFootDetection;
        public GroundDetection BackFootDetection;
        public GroundDetection
                    FrontFootTargetDetection, BackFootTargetDetection,
                    FrontLegDetection, BackLegDetection;

        // Cached Components
        [System.NonSerialized] public WeaponManager WeaponManager;
        [System.NonSerialized] public CharacterHealth Health;
        [System.NonSerialized] public CharacterMovement Movement;
        [System.NonSerialized] public LegsController LegsController;
        [System.NonSerialized] public BodyPhysicsController BodyController;
        [System.NonSerialized] public TorsoHeightController HeightController;
        [System.NonSerialized] public CharacterSkin CharacterSkin;
        [System.NonSerialized] public InteractionHandler InteractionHandler;
        [System.NonSerialized] public CharacterRotation CharacterRotation;
        [System.NonSerialized] public AIBrain AIBrain;

        // IK Solvers
        [System.NonSerialized] public LimbSolver2D FrontArmSolver, BackArmSolver;
        [System.NonSerialized] public LimbSolver2D FrontLegSolver, BackLegSolver;

        // Init Values
        [System.NonSerialized] public Vector2 HingeNormalInit, HingeAimDownInit;

        // Lifecycle
        [System.NonSerialized] public bool IsConscious = true;
        [System.NonSerialized] public bool IsDead;

        // Movement
        [System.NonSerialized] public bool IsFacingRight = true;
        [System.NonSerialized] public bool IsGrounded;
        [System.NonSerialized] public bool IsFrontFootGrounded = true;
        [System.NonSerialized] public bool IsBackFootGrounded = true;
        [System.NonSerialized] public bool IsRunning;
        [System.NonSerialized] public bool IsJumping;

        // Weapon
        [System.NonSerialized] public bool IsAiming;
        [System.NonSerialized] public bool IsReloading;
        [System.NonSerialized] public bool IsCycling;

        // Aim
        [System.NonSerialized] public AimMode AimMode = AimMode.WorldPoint;
        [System.NonSerialized] public Vector2 AimWorldPoint;
        [System.NonSerialized] public Vector2 AimDirectionInput;
        [System.NonSerialized] public float AimDirection;

        private readonly float[] _lastImpactTimes = new float[System.Enum.GetValues(typeof(BodyPartGroup)).Length];

        void Awake()
        {
            if (IsPlayer)
                PlayerManager.Register(this);

            CacheScripts();
            CacheBodyPartComponents();

            FrontLegSolver = UpperFrontLeg.GetComponent<LimbSolver2D>();
            BackLegSolver = UpperBackLeg.GetComponent<LimbSolver2D>();
            FrontArmSolver = UpperFrontArm.GetComponent<LimbSolver2D>();
            BackArmSolver = UpperBackArm.GetComponent<LimbSolver2D>();

            HingeNormalInit = RestHinge.anchor;
            HingeAimDownInit = AimHinge.anchor;
        }

        void Start()
        {
            CharacterSkin.ApplySkin();
            PlaceLegsInitially();
            CharacterSkin.StartBlinking();
        }

        void OnDestroy()
        {
            if (IsPlayer)
                PlayerManager.Unregister(this);
        }

        void FixedUpdate()
        {
            if (!IsConscious) return;

            CheckGrounded();
        }

        private void CacheScripts()
        {
            WeaponManager = GetComponent<WeaponManager>();
            Health = GetComponent<CharacterHealth>();
            Movement = GetComponent<CharacterMovement>();
            LegsController = GetComponent<LegsController>();
            BodyController = GetComponent<BodyPhysicsController>();
            HeightController = GetComponent<TorsoHeightController>();
            CharacterSkin = GetComponent<CharacterSkin>();
            CharacterRotation = GetComponent<CharacterRotation>();

            if (IsPlayer)
            {
                gameObject.AddComponent<PlayerInputHandler>();
                InteractionHandler = gameObject.AddComponent<InteractionHandler>();
            }
            else
            {
                AIBrain = gameObject.AddComponent<AIBrain>();
                AIBrain.Behavior = AIBehavior;
            }
        }

        private void PlaceLegsInitially()
        {
            IKManager.enabled = true;

            if (FrontFootDetection.IsGrounded() || BackFootDetection.IsGrounded())
            {
                IsGrounded = true;
                BodyController.RagdollLegs(false, false);
                LegsController.PlaceLegs(true);
                FrontFootAlignment.Enabled = true;
                BackFootAlignment.Enabled = true;
            }
            else
            {
                HandleUngrounded();
            }
        }

        private void CheckGrounded()
        {
            if (IsJumping) return;

            IsFrontFootGrounded = FrontFootDetection.IsGrounded();
            if (!IsFrontFootGrounded && IsGrounded) IsFrontFootGrounded = FrontFootTargetDetection.IsGrounded();

            IsBackFootGrounded = BackFootDetection.IsGrounded();
            if (!IsBackFootGrounded && IsGrounded) IsBackFootGrounded = BackFootTargetDetection.IsGrounded();

            bool isFrontLegGrounded = FrontLegDetection.IsGrounded();
            bool isBackLegGrounded = BackLegDetection.IsGrounded();

            if (!IsGrounded && (IsBackFootGrounded || IsFrontFootGrounded || isFrontLegGrounded || isBackLegGrounded))
            {
                HandleGrounded();
            }
            else if (!LegsController.FrontLegState.IsMoving() && !LegsController.BackLegState.IsMoving() && !IsBackFootGrounded && !IsFrontFootGrounded && IsGrounded)
            {
                HandleUngrounded();
            }
        }

        public void Die(bool instant)
        {
            if (IsConscious)
            {
                IsConscious = false;

                if (WeaponManager.IsHoldingGun)
                    WeaponManager.Gun.AbortAllActions();

                RestHinge.enabled = false;
                AimHinge.enabled = false;

                if (instant)
                {
                    if (WeaponManager.IsHoldingGun)
                        WeaponManager.DropWeapon();

                    BodyController.StopAllCoroutines();
                    BodyController.DeactivateBalances();
                }
                else
                {
                    if (WeaponManager.IsHoldingGun)
                    {
                        if (Random.value < .8f)
                            WeaponManager.DropWeapon(Random.Range(0f, 3f));
                        else
                            WeaponManager.DropWeapon();
                    }

                    StartCoroutine(BodyController.FadeOutBalance(Random.Range(.5f, 3f)));
                }
            }
            else if (instant)
            {
                BodyController.StopAllCoroutines();
                BodyController.DeactivateBalances();
            }

            CharacterSkin.SetHeadSprite(Skin.Head2);
        }

        private void CacheBodyPartComponents()
        {
            Head.CacheComponents();
            UpperTorso.CacheComponents();
            MidTorso.CacheComponents();
            LowerTorso.CacheComponents();
            UpperFrontArm.CacheComponents();
            LowerFrontArm.CacheComponents();
            UpperBackArm.CacheComponents();
            LowerBackArm.CacheComponents();
            UpperFrontLeg.CacheComponents();
            LowerFrontLeg.CacheComponents();
            FrontFoot.CacheComponents();
            UpperBackLeg.CacheComponents();
            LowerBackLeg.CacheComponents();
            BackFoot.CacheComponents();
        }

        public float GetLastImpactTime(BodyPartGroup group) => _lastImpactTimes[(int)group];
        public void SetLastImpactTime(BodyPartGroup group, float time) => _lastImpactTimes[(int)group] = time;

        // Player shots always land; AI shots roll against the brain's HitChance per bullet.
        public bool RollHit()
        {
            if (IsPlayer || AIBrain == null || AIBrain.Behavior == null) return true;
            return Random.value < AIBrain.Behavior.HitChance;
        }

        public void HandleUngrounded()
        {
            IsGrounded = false;
            IsFrontFootGrounded = false;
            IsBackFootGrounded = false;
            BackFootAlignment.Enabled = false;
            FrontFootAlignment.Enabled = false;
            BodyController.RagdollLegs(true, true);
        }

        public void HandleGrounded()
        {
            IsGrounded = true;
            Movement.StartCoroutine(Movement.JumpCooldown());
            LegsController.MoveTargetToFeet();

            BodyController.RagdollLegs(false, false);

            LegsController.PlaceLegs(false);

            BackFootAlignment.Enabled = true;
            FrontFootAlignment.Enabled = true;
            HeightController.TemporaryChangeHeight(.8f, .1f);
        }
    }

    [System.Serializable]
    public class BodyPartRef
    {
        public Rigidbody2D Rb;
        [System.NonSerialized] public SpriteRenderer Sr;
        [System.NonSerialized] public Balance Bal;

        public Transform Transform => Rb.transform;
        public GameObject GameObject => Rb.gameObject;

        public void CacheComponents()
        {
            Sr = Rb.GetComponent<SpriteRenderer>();
            Bal = Rb.GetComponent<Balance>();
        }

        public T GetComponent<T>() => Rb.GetComponent<T>();
    }
}
