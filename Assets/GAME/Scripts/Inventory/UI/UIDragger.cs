using UnityEngine;
using UnityEngine.EventSystems;

// Attach to any UI panel to make it draggable by the user.
// Optionally set Target to drag a different RectTransform — useful when this
// component sits on a title bar but should move the parent window.
public class UIDragger : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public Canvas        RootCanvas;
    public RectTransform Target;

    private RectTransform self;
    private Vector2       dragOffset;

    void Awake() => self = GetComponent<RectTransform>();

    private RectTransform Active => Target != null ? Target : self;

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RootCanvas.GetComponent<RectTransform>(), e.position, RootCanvas.worldCamera, out var local);
        dragOffset = Active.anchoredPosition - local;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RootCanvas.GetComponent<RectTransform>(), e.position, RootCanvas.worldCamera, out var local);
        Active.anchoredPosition = local + dragOffset;
    }
}
