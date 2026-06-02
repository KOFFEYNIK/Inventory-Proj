using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ItemUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerClickHandler
{
    public ItemInstance     Item;
    public GridUI           SourceGrid;
    public GridContainer    SourceContainer;
    public EquipmentSlotUI  SourceSlot;

    public void Init(ItemInstance item, GridUI grid, GridContainer container)
    {
        Item            = item;
        SourceGrid      = grid;
        SourceContainer = container;
        SourceSlot      = null;
    }

    public void InitFromSlot(ItemInstance item, EquipmentSlotUI slot)
    {
        Item            = item;
        SourceGrid      = null;
        SourceContainer = null;
        SourceSlot      = slot;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (LayoutEditMode.IsActive) return;   // в edit-режиме предметы недоступны
        if (e.button == PointerEventData.InputButton.Right)
            ContextMenuUI.Instance?.Show(Item, SourceContainer, e.position, SourceSlot);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (LayoutEditMode.IsActive) return;   // edit-mode перехватывает drag на панелях
        if (e.button != PointerEventData.InputButton.Left) return;
        DragDropController.Instance?.BeginDrag(this, e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (LayoutEditMode.IsActive) return;
        DragDropController.Instance?.UpdateDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (LayoutEditMode.IsActive) return;
        DragDropController.Instance?.EndDrag(e);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (LayoutEditMode.IsActive) return;
        if (e.button == PointerEventData.InputButton.Left &&
            !e.dragging && Item.definition.isContainer && Item.nestedContainer != null)
            InventoryUI.Instance?.OpenNestedContainer(Item.nestedContainer, Item.definition.displayName);
    }
}
