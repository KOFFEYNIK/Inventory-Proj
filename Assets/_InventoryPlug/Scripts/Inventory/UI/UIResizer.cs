using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// «Ручка» в углу окна, позволяющая растягивать <see cref="Target"/> перетаскиванием.
/// Висит на маленьком GameObject'е, заякоренном в одном из углов окна. Рассчитан на
/// окно с pivot (0.5, 0.5): при ресайзе противоположный угол остаётся на месте.
///
/// Работает только в режиме <see cref="LayoutEditMode.IsActive"/> (как и остальная
/// кастомизация лейаута). После отпускания вызывает <see cref="OnResized"/> с новыми
/// размером и позицией окна — InventoryUI сохраняет их в JSON.
/// </summary>
public class UIResizer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum Corner { BottomRight, BottomLeft, TopRight, TopLeft }

    public Canvas        RootCanvas;
    public RectTransform Target;
    public Corner        GripCorner = Corner.BottomRight;

    public Vector2 MinSize = new(480f, 360f);
    public Vector2 MaxSize = new(1800f, 1000f);

    /// <summary>(newSize, newAnchoredPos) — вызывается при отпускании.</summary>
    public System.Action<Vector2, Vector2> OnResized;

    private RectTransform parentRt;   // родитель окна (rootCanvas) — в его координатах считаем
    private Vector2 startPointer;
    private Vector2 startSize;
    private Vector2 startPos;

    // Знаки: какая сторона движется. +1 — правая/верхняя, -1 — левая/нижняя.
    private float Sx => (GripCorner == Corner.BottomRight || GripCorner == Corner.TopRight) ? 1f : -1f;
    private float Sy => (GripCorner == Corner.TopRight   || GripCorner == Corner.TopLeft)  ? 1f : -1f;

    private bool Ready => LayoutEditMode.IsActive && Target != null && parentRt != null;

    void Awake()
    {
        if (Target != null) parentRt = Target.parent as RectTransform;
        if (RootCanvas == null) RootCanvas = GetComponentInParent<Canvas>();
    }

    private Camera Cam => (RootCanvas != null && RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        ? RootCanvas.worldCamera : null;

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (parentRt == null && Target != null) parentRt = Target.parent as RectTransform;
        if (!Ready) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, e.position, Cam, out startPointer))
            return;
        startSize = Target.sizeDelta;
        startPos  = Target.anchoredPosition;
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || !Ready) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, e.position, Cam, out var cur))
            return;

        var delta = cur - startPointer;

        float newW = Mathf.Clamp(startSize.x + Sx * delta.x, MinSize.x, MaxSize.x);
        float newH = Mathf.Clamp(startSize.y + Sy * delta.y, MinSize.y, MaxSize.y);

        float actualDW = newW - startSize.x;
        float actualDH = newH - startSize.y;

        // pivot (0.5,0.5): сдвигаем центр на половину изменения в сторону движущегося угла,
        // чтобы противоположный угол остался на месте.
        Target.sizeDelta        = new Vector2(newW, newH);
        Target.anchoredPosition = new Vector2(
            startPos.x + Sx * actualDW * 0.5f,
            startPos.y + Sy * actualDH * 0.5f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || Target == null) return;
        OnResized?.Invoke(Target.sizeDelta, Target.anchoredPosition);
    }
}
