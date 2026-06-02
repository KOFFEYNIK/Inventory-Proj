using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Wound : MonoBehaviour
    {
        [SerializeField]
        private Sprite[] _sprites= new Sprite[1];

        [SerializeField]
        private Color[] _colors = new Color[1];

        private SpriteRenderer _spriteRenderer;

        void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void RandomizeSprite()
        {
            _spriteRenderer.sprite = _sprites[Random.Range(0, _sprites.Length)];
            _spriteRenderer.color = _colors[Random.Range(0, _colors.Length)];
        }
    }
}
