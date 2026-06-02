using UnityEngine;
using UnityEngine.EventSystems;

// Attach to any UI panel to make it draggable by the user.
// Optionally set Target to drag a different RectTransform — useful when this
// component sits on a title bar but should move the parent window.
public class UIDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Canvas        RootCanvas;
    public RectTransform Target;

    [Tooltip("Если true — перетаскивание работает только когда активен LayoutEditMode " +
             "(режим EDIT инвентаря). Используется для главного окна инвентаря.")]
    public bool EditModeOnly = false;

    [Tooltip("Вызывается при отпускании с новой anchoredPosition. Нужен для сохранения " +
             "позиции окна в InventoryLayoutCustomization.")]
    public System.Action<Vector2> OnMoved;

    private RectTransform self;
    private Vector2       dragOffset;

    void Awake() => self = GetComponent<RectTransform>();

    private RectTransform Active => Target != null ? Target : self;

    private bool Blocked => EditModeOnly && !LayoutEditMode.IsActive;

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || Blocked) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RootCanvas.GetComponent<RectTransform>(), e.position, RootCanvas.worldCamera, out var local);
        dragOffset = Active.anchoredPosition - local;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || Blocked) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RootCanvas.GetComponent<RectTransform>(), e.position, RootCanvas.worldCamera, out var local);
        Active.anchoredPosition = local + dragOffset;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || Blocked) return;
        OnMoved?.Invoke(Active.anchoredPosition);
    }
}
