using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class BodyPart : MonoBehaviour
    {
        [Header("References")]
        public Character Character;

        [Header("Settings")]
        [SerializeField]
        protected float _damageMultiplier = 1;

        [SerializeField]
        private int _maxBloodCascades = 3;

        [SerializeField]
        private DynamicFloat _bloodCascadeLifetime = new()
        {
            Mode = DynamicValueMode.BetweenTwoConstants,
            MinValue = 2f,
            MaxValue = 10f
        };

        protected Rigidbody2D _rb;

        private Vector2 _initialLocalPos;
        private int _currentBloodCascades = 0;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _initialLocalPos = transform.localPosition;
        }

        public virtual void TakeDamage(RaycastHit2D hit, float damage, float bulletForce)
        {
            Character.Health.TakeDamage(damage * _damageMultiplier, false);
        }

        public void FixLocalPosition()
        {
            transform.localPosition = _initialLocalPos;
        }

        public bool CanSpawnBloodCascade()
        {
            return Character.Health.Blood > 0 && _maxBloodCascades > _currentBloodCascades;
        }

        public void AddBloodCascade(BloodCascade bloodCascade)
        {
            _currentBloodCascades++;
            bloodCascade.Initialize(Character);
            bloodCascade.StartCoroutine(bloodCascade.LifeTimeCoroutine(_bloodCascadeLifetime.GetValue()));
        }
    }
}
