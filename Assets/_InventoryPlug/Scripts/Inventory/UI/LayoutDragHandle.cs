using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Компонент-перетаскиватель, который вешается на каждую кастомизируемую панель/слот
/// инвентаря. В обычном режиме «молчит» — пропускает события дальше (drag предметов,
/// клики). В режиме <see cref="LayoutEditMode.IsActive"/>=true перехватывает drag
/// и двигает <see cref="RectTransform"/> по экрану; при отпускании записывает новую
/// позицию в <see cref="InventoryLayoutCustomization"/>.
///
/// Идентификация (один из трёх вариантов):
///   • <see cref="PanelId"/>          — для больших панелей и карманов;
///   • <see cref="SlotType"/>          — для equipment-слотов из <c>Layout.equipmentSlots[]</c>;
///   • <see cref="SlotType"/> + <see cref="IsIndicator"/>=true — для индикаторов
///     рюкзака/разгрузки/кейса (отдельные 1×1-слоты).
/// </summary>
public class LayoutDragHandle : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Уникальный ID для PanelOverride. Не нужен, если используется SlotType.")]
    public string PanelId;

    [Tooltip("Тип слота экипировки (для EquipmentSlot или Indicator).")]
    public EquipmentSlotType SlotType;
    public bool HasSlotType;

    [Tooltip("Если true и HasSlotType — это slot-indicator, а не equipmentSlot.")]
    public bool IsIndicator;

    [Tooltip("Полупрозрачная подсветка в edit-mode (создаётся LayoutEditMode).")]
    public Image overlay;

    [Tooltip("Опционально: handle'ы, которые двигаются вместе с этим (как единая группа). " +
             "Используется для лейбла «ЗДОРОВЬЕ», чтобы драг лейбла перетаскивал все 7 столбиков.")]
    public LayoutDragHandle[] groupChildren;

    private RectTransform rt;
    private RectTransform parentRt;
    private Canvas        rootCanvas;

    private Vector2 dragStartLocal;
    private Vector2 dragStartAnchored;
    private Vector2[] groupChildStartAnchored;

    void Awake()
    {
        rt = (RectTransform)transform;
        parentRt = rt.parent as RectTransform;
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!LayoutEditMode.IsActive || rt == null || parentRt == null) return;

        var cam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                  ? rootCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, e.position, cam, out dragStartLocal))
            return;

        dragStartAnchored = rt.anchoredPosition;

        if (groupChildren != null && groupChildren.Length > 0)
        {
            groupChildStartAnchored = new Vector2[groupChildren.Length];
            for (int i = 0; i < groupChildren.Length; i++)
                if (groupChildren[i] != null)
                    groupChildStartAnchored[i] = ((RectTransform)groupChildren[i].transform).anchoredPosition;
        }
        else groupChildStartAnchored = null;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!LayoutEditMode.IsActive || rt == null || parentRt == null) return;

        var cam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                  ? rootCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, e.position, cam, out var cur))
            return;

        var delta = cur - dragStartLocal;
        rt.anchoredPosition = dragStartAnchored + delta;

        // Group-drag: применяем тот же delta ко всем дочерним handle'ам (если есть).
        // Так лейбл «ЗДОРОВЬЕ» работает как единая ручка для всех 7 столбиков.
        if (groupChildStartAnchored != null)
            for (int i = 0; i < groupChildren.Length && i < groupChildStartAnchored.Length; i++)
                if (groupChildren[i] != null)
                    ((RectTransform)groupChildren[i].transform).anchoredPosition =
                        groupChildStartAnchored[i] + delta;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!LayoutEditMode.IsActive || rt == null) return;
        LayoutEditMode.Instance?.RecordHandleMoved(this, rt.anchoredPosition);

        // Group-drag: сохраняем новую позицию каждого ребёнка как индивидуальный override —
        // после группового перетаскивания каждый столбик становится «закреплён» в новой
        // точке. Это позволяет потом точечно сдвинуть один из них.
        if (groupChildren != null)
            foreach (var child in groupChildren)
                if (child != null)
                    LayoutEditMode.Instance?.RecordHandleMoved(
                        child, ((RectTransform)child.transform).anchoredPosition);
    }

    /// <summary>
    /// Включить/выключить визуальную подсветку рамкой. Вызывается из LayoutEditMode
    /// при входе/выходе из edit-режима. Перед показом поднимает overlay в последние
    /// siblings — иначе он рисуется ПОД BG/Fill/Label, которые создавались после.
    /// </summary>
    public void SetOverlayActive(bool active)
    {
        if (overlay == null) return;
        if (active) overlay.transform.SetAsLastSibling();
        overlay.gameObject.SetActive(active);
    }
}
