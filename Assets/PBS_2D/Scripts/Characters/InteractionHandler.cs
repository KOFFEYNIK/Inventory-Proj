using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace PBS2D
{
    [RequireComponent(typeof(Character))]
    public class InteractionHandler : MonoBehaviour
    {
        [SerializeField]
        private float _detectionRadius = 0.5f;
        
        [SerializeField]
        private float _detectionRayDistance = 2.5f;
        
        [SerializeField]
        private float _detectionInterval = 0.2f;

        private Character _character;
        private Interactable _closestInteractable;
        private Vector2 _closestPoint;
        private Coroutine _checkRoutine;

        void Awake()
        {
            _character = GetComponent<Character>();
        }

        private void OnEnable()
        {
            _checkRoutine = StartCoroutine(CheckClosestInteractableRoutine());
        }

        private void OnDisable()
        {
            if (_checkRoutine != null)
                StopCoroutine(_checkRoutine);

            // Clear the highlight so the previously-targeted weapon doesn't
            // stay outlined for the next player to pick up
            if (_closestInteractable != null)
            {
                _closestInteractable.HideOutline();
                _closestInteractable = null;
            }
        }

        private IEnumerator CheckClosestInteractableRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_detectionInterval);

                UpdateClosestInteractable();
            }
        }

        public bool HasInteractable()
        {
            return _closestInteractable != null;
        }

        private void UpdateClosestInteractable()
        {
            if (_closestInteractable != null) _closestInteractable.HideOutline();

            // Don't surface or highlight pickups while ragdolled
            if (!_character.IsConscious)
            {
                _closestInteractable = null;
                return;
            }

            _closestInteractable = FindClosestInteractable();

            if (_closestInteractable != null) _closestInteractable.ShowOutline();
        }

        private Interactable FindClosestInteractable()
        {
            Interactable closest;

            closest = FindClosestCircle();

            if (closest == null)
            {
                closest = FindClosestRaycast();
            }

            return closest;
        }

        private Interactable FindClosestCircle()
        {
            Vector2 handPosition = _character.BackHand.transform.position;

            Collider2D[] hits = Physics2D.OverlapCircleAll(handPosition, _detectionRadius);

            Interactable nearest = null;
            float nearestDistance = Mathf.Infinity;
            Vector2 nearestPoint = handPosition;

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent(out Interactable interactable)) continue;
                if (!interactable.CanInteract()) continue;

                Vector2 pointOnCollider = hit.ClosestPoint(handPosition);
                float distance = Vector2.Distance(handPosition, pointOnCollider);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = interactable;
                    nearestPoint = pointOnCollider;
                }
            }

            _closestPoint = nearestPoint;
            return nearest;
        }

        private Interactable FindClosestRaycast()
        {
            Vector2 handPosition = _character.BackHand.transform.position;
            Vector2 aimDirection = (PlayerInfo.mousePos - handPosition).normalized;

            if (PlayerInfo.currentDevice is Gamepad) aimDirection = PlayerInfo.controllerInput;

            RaycastHit2D[] hits = Physics2D.RaycastAll(handPosition, aimDirection, _detectionRayDistance);

            Interactable nearest = null;
            float nearestDistance = Mathf.Infinity;
            Vector2 nearestPoint = handPosition;

            foreach (var hit in hits)
            {
                if (!hit.collider.TryGetComponent(out Interactable interactable)) continue;
                if (!interactable.CanInteract()) continue;

                float distance = Vector2.Distance(handPosition, hit.point);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = interactable;
                    nearestPoint = hit.point;
                }
            }

            _closestPoint = nearestPoint;
            return nearest;
        }

        public void InteractWithClosest()
        {
            if (!_character.IsConscious) return;

            if (_closestInteractable != null)
            {
                _closestInteractable.Interact(_character);
            }
        }

        public void InteractWithPos(Vector2 pos)
        {
            if (!_character.IsConscious) return;

            RaycastHit2D hit = Physics2D.Raycast(pos, Vector2.zero);

            if (hit.collider != null && hit.collider.TryGetComponent<Interactable>(out var interactable))
            {
                interactable.Interact(_character);
            }
        }

        private void OnDrawGizmos()
        {
            if (_character == null) return;

            Vector2 handPosition = _character.BackHand.transform.position;

            // --- Draw Circle ---
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(handPosition, _detectionRadius);

            // --- Draw Raycast ---
            Vector2 aimTarget = Application.isPlaying ? PlayerInfo.mousePos : handPosition + Vector2.right;
            Vector2 aimDirection = (aimTarget - handPosition).normalized;
            if (PlayerInfo.currentDevice is Gamepad) aimDirection = PlayerInfo.controllerInput;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(handPosition, handPosition + aimDirection * _detectionRayDistance);

            // --- Draw Closest Hit Point ---
            if (_closestInteractable != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_closestPoint, 0.05f);
            }
        }
    }
}
