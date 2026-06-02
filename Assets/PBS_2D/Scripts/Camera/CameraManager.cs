using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace PBS2D
{
    [RequireComponent(typeof(Camera))]
    public class CameraManager : Singleton<CameraManager>
    {
        [Header("Target")]
        [SerializeField]
        private Vector3 _offset = new(0f, 1.5f, -10f);

        private Character _characterTarget;

        [Header("Follow Smoothing")]
        [SerializeField]
        private float _xSmoothTime = 0.15f;
        [SerializeField]
        private float _ySmoothTime = 1f;

        [Header("Aim Parallax")]
        [SerializeField]
        private float _parallaxStrengthX = 3f;
        [SerializeField]
        private float _parallaxStrengthY = 3f;

        [Header("Facing Shift")]
        [SerializeField]
        private float _facingShiftDistance = 2f;
        [SerializeField]
        private float _facingShiftSpeed = 20f;

        private Camera _cam;
        private Transform _target;
        private Vector3 _shakeOffset;
        private Coroutine _shakeRoutine;
        private float _baseOrthoSize;
        private float _xVel, _yVel;
        private float _baseCamX, _baseCamY;
        private float _targetSide = 1f;
        private float _smoothedSide = 1f;

        protected override void Awake()
        {
            base.Awake();

            _cam = GetComponent<Camera>();
            _baseOrthoSize = Mathf.Max(0.0001f, _cam.orthographicSize);
        }

        void OnEnable()
        {
            PlayerManager.OnPlayerSpawned += SetTarget;
            PlayerManager.OnPlayerDespawned += ClearTarget;

            if (PlayerManager.Player != null)
                SetTarget(PlayerManager.Player);
        }

        void OnDisable()
        {
            PlayerManager.OnPlayerSpawned -= SetTarget;
            PlayerManager.OnPlayerDespawned -= ClearTarget;
        }

        private void SetTarget(Character player)
        {
            _characterTarget = player;
            _target = player.LowerTorso.Transform;

            _baseCamX = _target.position.x + _offset.x;
            _baseCamY = _target.position.y + _offset.y;
            transform.position = new Vector3(_baseCamX, _baseCamY, _offset.z);

            _targetSide = _characterTarget.IsFacingRight ? 1f : -1f;
            _smoothedSide = _targetSide;
        }

        private void ClearTarget(Character player)
        {
            _characterTarget = null;
            _target = null;
        }

        void Update()
        {
            _targetSide = (_characterTarget != null && _characterTarget.IsFacingRight) ? 1f : -1f;
            _smoothedSide = Mathf.Lerp(_smoothedSide, _targetSide, Time.unscaledDeltaTime * _facingShiftSpeed);
        }

        public void ChangeCameraSize(float size)
        {
            _cam.orthographicSize = size;
        }

        void LateUpdate()
        {
            if (!_target) return;

            float zoomScale = _cam.orthographicSize / _baseOrthoSize;
            Vector2 parallax = CalculateParallax(zoomScale);
            float facingOffset = _smoothedSide * _facingShiftDistance * zoomScale;

            _baseCamX = Mathf.SmoothDamp(_baseCamX, _target.position.x + _offset.x, ref _xVel, _xSmoothTime);
            _baseCamY = Mathf.SmoothDamp(_baseCamY, _target.position.y + _offset.y, ref _yVel, _ySmoothTime);

            Vector3 finalPos = new Vector3(
                _baseCamX + parallax.x + facingOffset,
                _baseCamY + parallax.y,
                _offset.z
            ) + _shakeOffset;

            if (IsFinite(finalPos))
                transform.position = finalPos;

            if (_shakeRoutine == null)
                _shakeOffset = Vector3.zero;
        }

        private Vector2 CalculateParallax(float zoomScale)
        {
            if (PlayerInfo.currentDevice is Gamepad)
                return CalculateGamepadParallax(zoomScale);

            return CalculateMouseParallax(zoomScale);
        }

        private Vector2 CalculateGamepadParallax(float zoomScale)
        {
            Vector2 aim = PlayerInfo.controllerInput;
            if (aim.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector2 dir = aim.normalized;
            return new Vector2(
                dir.x * _parallaxStrengthX * zoomScale * 0.5f,
                dir.y * _parallaxStrengthY * zoomScale * 0.5f
            );
        }

        private Vector2 CalculateMouseParallax(float zoomScale)
        {
            Vector2 mousePos = PlayerInfo.mouseScreenPos;
            float normX = Mathf.Clamp(mousePos.x, 0f, Screen.width) / Screen.width * 2f - 1f;
            float normY = Mathf.Clamp(mousePos.y, 0f, Screen.height) / Screen.height * 2f - 1f;

            return new Vector2(
                normX * _parallaxStrengthX * zoomScale,
                normY * _parallaxStrengthY * zoomScale
            );
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        }

        public void Shake(float magnitude, float duration)
        {
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeCoroutine(magnitude, duration));
        }

        private IEnumerator ShakeCoroutine(float magnitude, float duration)
        {
            float elapsed = 0f;
            float zoomScale = _cam.orthographicSize / _baseOrthoSize;

            while (elapsed < duration)
            {
                float damping = 1f - (elapsed / duration);
                Vector2 random = Random.insideUnitCircle * magnitude * damping * zoomScale;
                _shakeOffset = new Vector3(random.x, random.y, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _shakeOffset = Vector3.zero;
            _shakeRoutine = null;
        }
    }
}
