using System.Collections;
using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class CharacterHealth : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Min(0.001f)]
        private float MaxHealth = 100f;

        [SerializeField, Min(0.001f)]
        private float MaxBleedRate = 5f;
        
        public float Blood { get; private set; } = 100f;

        private Character _character;
        private Coroutine _bleedCoroutine;
        private float _currentHealth;
        private float _currentBleedRate;
        
        private static readonly WaitForSeconds _waitForSeconds0_5 = new (0.5f);

        void Awake()
        {
            _character = GetComponent<Character>();
            Blood = 100;
        }

        void Start()
        {
            _currentHealth = MaxHealth;
        }

        void Update()
        {
            float normalized = Mathf.Clamp01(_currentHealth / MaxHealth);
            _character.HeightController.Multiplier = Mathf.Lerp(0.95f, 1f, normalized);
        }
        public void TakeDamage(float damage, bool instant)
        {
            _currentHealth -= damage;

            if (_currentHealth <= 0)
            {
                _character.Die(instant);
                return;
            }

            _character.CharacterSkin.HandleGetHit();
        }

        public void UpdateBleedRate(float amountPerTick)
        {
            _currentBleedRate += amountPerTick;
            _currentBleedRate = Mathf.Min(_currentBleedRate, MaxBleedRate);

            if (_bleedCoroutine == null) _bleedCoroutine = StartCoroutine(BleedCoroutine());
        }

        public IEnumerator BleedCoroutine()
        {
            while (Blood > 0f && _currentBleedRate > 0f)
            {
                Blood -= _currentBleedRate;
                if (Blood <= 0f)
                {
                    _character.Die(true);
                    break;
                }

                yield return _waitForSeconds0_5;
            }

            _bleedCoroutine = null;
        }
    }
}
