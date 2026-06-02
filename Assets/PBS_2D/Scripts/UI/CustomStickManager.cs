using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace PBS2D
{
    public class CustomStickManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public int MoveFingerId { get; private set; } = -1;

        [SerializeField] private OnScreenStick _stick;

        public void OnPointerDown(PointerEventData eventData)
        {
            foreach (var t in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                if ((t.screenPosition - eventData.position).sqrMagnitude < 25f * 25f)
                {
                    MoveFingerId = t.touchId;
                    break;
                }
            }

            PointerEventData pointerData = new(EventSystem.current)
            {
                position = new Vector2(transform.position.x, transform.position.y)
            };

            _stick.OnPointerDown(pointerData);

            _stick.OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _stick.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            MoveFingerId = -1;
            _stick.OnPointerUp(eventData);
        }
    }
}
