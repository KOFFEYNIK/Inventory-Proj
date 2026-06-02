using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragDropController : MonoBehaviour
{
    public static DragDropController Instance { get; private set; }

    public Canvas RootCanvas;

    private ItemUI              draggedUI;
    private ItemInstance        draggedItem;
    private GridContainer       originContainer;
    private Vector2Int          originPos;
    private EquipmentSlotUI     originSlot;
    private GridUI              hoveredGrid;
    private EquipmentSlotUI     hoveredSlot;
    private WeaponHotbarSlotUI  hoveredHotbarSlot;
    private PointerEventData    lastPointer;

    // Split-drag: тащим НОВЫЙ под-стак, отделённый от splitSourceItem (он остаётся в сетке).
    // Если дроп не удался — остаток вливаем обратно в splitSourceItem, а не на origin.
    private bool         draggedFromSplit;
    private ItemInstance splitSourceItem;

    private GameObject    ghost;
    private RectTransform ghostRt;
    private CanvasGroup   ghostCG;

    void Awake() => Instance = this;

    void Update()
    {
        if (ghost == null || draggedItem == null) return;
        if (Input.GetKeyDown(KeyCode.R))
        {
            draggedItem.Rotate();
            ResizeGhost();
            // Подсветка целевых ячеек считается только в UpdateDrag (по движению мыши).
            // На R обновляем её вручную по последнему положению курсора, иначе поворот
            // «не виден», пока мышь не сдвинётся.
            if (lastPointer != null) UpdateHighlight(lastPointer);
        }
    }

    // ── Begin ─────────────────────────────────────────────────────────────────

    public void BeginDrag(ItemUI ui, PointerEventData e)
    {
        draggedUI       = ui;
        originContainer = ui.SourceContainer;
        originSlot      = ui.SourceSlot;
        lastPointer     = e;

        // Shift + стакаемый предмет >1 → отделяем половину (округление вверх) в новый под-стак.
        bool wantSplit = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                         && ui.Item.IsStackable && ui.Item.stackCount > 1;
        if (wantSplit)
        {
            draggedFromSplit = true;
            splitSourceItem  = ui.Item;
            int take = Mathf.CeilToInt(ui.Item.stackCount / 2f);
            ui.Item.stackCount -= take;
            draggedItem = new ItemInstance
            {
                instanceId = System.Guid.NewGuid().ToString(),
                definition = ui.Item.definition,
                stackCount = take,
                isRotated  = ui.Item.isRotated
            };
            // Источник остаётся на месте и видимым; его подпись обновится при Refresh в EndDrag.
            CreateGhost(e.position);
            return;
        }

        draggedItem = ui.Item;

        if (originContainer != null)
        {
            originContainer.TryGetPosition(draggedItem, out originPos);
            originContainer.Remove(draggedItem);
        }
        else if (originSlot != null && originSlot.Slot != null)
        {
            // Detach silently — going through InventoryManager.Unequip would fire
            // OnInventoryChanged, refresh the slot UI, and destroy the very ItemUI
            // we're dragging (EventSystem then drops OnDrag/OnEndDrag callbacks).
            originSlot.Slot.EquippedItem = null;
        }

        // Hide visually but keep alive — destroying it breaks the drag chain.
        // Через CanvasGroup, чтобы скрыть и фон, и дочерний Icon, и подпись разом.
        var cg = ui.GetComponent<CanvasGroup>();
        if (cg == null) cg = ui.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        CreateGhost(e.position);
    }

    private void CreateGhost(Vector2 screenPos)
    {
        ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        ghost.transform.SetParent(RootCanvas.transform, false);
        ghost.transform.SetAsLastSibling();

        ghostRt = ghost.GetComponent<RectTransform>();
        ghostRt.pivot = new Vector2(0.5f, 0.5f);

        ghostCG = ghost.GetComponent<CanvasGroup>();
        ghostCG.blocksRaycasts = false;
        ghostCG.alpha          = 0.75f;

        var img = ghost.GetComponent<Image>();
        img.sprite = ItemPreviewRenderer.GetSprite(draggedItem.definition);
        img.preserveAspect = true;
        img.color  = new Color(0.55f, 0.85f, 1f, 1f);

        ResizeGhost();
        MoveGhost(screenPos);
    }

    private void ResizeGhost()
    {
        // Размер — по НЕповёрнутому габариту; поворот на -90° (по часовой) при isRotated.
        var def = draggedItem.definition;
        float uw = def.width  * (GridUI.CellSize + GridUI.CellGap) - GridUI.CellGap;
        float uh = def.height * (GridUI.CellSize + GridUI.CellGap) - GridUI.CellGap;
        ghostRt.sizeDelta        = new Vector2(uw, uh);
        ghostRt.localEulerAngles = draggedItem.isRotated ? new Vector3(0f, 0f, -90f) : Vector3.zero;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public void UpdateDrag(PointerEventData e)
    {
        if (ghost == null) return;
        lastPointer = e;
        MoveGhost(e.position);
        UpdateHighlight(e);
    }

    private void MoveGhost(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RootCanvas.GetComponent<RectTransform>(), screenPos, RootCanvas.worldCamera, out Vector2 local);
        ghostRt.anchoredPosition = local;
    }

    private void UpdateHighlight(PointerEventData e)
    {
        hoveredGrid?.ResetCellColors();
        hoveredGrid = null;

        if (hoveredSlot != null) { hoveredSlot.SetHighlight(false, false); hoveredSlot = null; }
        if (hoveredHotbarSlot != null) { hoveredHotbarSlot.SetHighlight(false, false); hoveredHotbarSlot = null; }

        var hotbarSlot = FindHotbarSlotUnder(e);
        if (hotbarSlot != null)
        {
            hoveredHotbarSlot = hotbarSlot;
            bool ok = WeaponHotbar.Instance != null && WeaponHotbar.Instance.IsUserSlot(hotbarSlot.SlotIndex);
            hotbarSlot.SetHighlight(true, ok);
            return;
        }

        var slot = FindSlotUnder(e);
        if (slot != null)
        {
            hoveredSlot = slot;
            bool ok = slot.Slot != null && slot.Slot.IsEmpty && slot.Slot.Accepts(draggedItem);
            slot.SetHighlight(ok, true);
            return;
        }

        var grid = FindGridUnder(e);
        if (grid == null) return;

        hoveredGrid = grid;

        // Курсор над предметом-контейнером → подсветим его футпринт (вложение, как в Таркове).
        if (TryGetCursorCell(grid, e, out var cur))
        {
            var under = grid.Container.GetItemAt(cur.x, cur.y);
            if (under != null && under != draggedItem && under.nestedContainer != null &&
                grid.Container.TryGetPosition(under, out var upos))
            {
                grid.HighlightCells(upos, under.CurrentWidth, under.CurrentHeight,
                                    under.nestedContainer.CanAcceptSomewhere(draggedItem));
                return;
            }
        }

        if (!TryGetDropCell(grid, e, out var cell)) return;
        grid.HighlightCells(cell, draggedItem.CurrentWidth, draggedItem.CurrentHeight,
                            grid.Container.CanPlace(draggedItem, cell));
    }

    // ── End ───────────────────────────────────────────────────────────────────

    public void EndDrag(PointerEventData e)
    {
        hoveredGrid?.ResetCellColors();
        hoveredGrid = null;
        if (hoveredSlot != null) { hoveredSlot.SetHighlight(false, false); hoveredSlot = null; }
        if (hoveredHotbarSlot != null) { hoveredHotbarSlot.SetHighlight(false, false); hoveredHotbarSlot = null; }

        bool placed = false;

        // 1) Hotbar user slot: предмет остаётся в инвентаре (возвращается на origin),
        //    в хотбаре сохраняется ссылка.
        // Под-стак из split нельзя класть в хотбар/экипировку — там должна остаться ссылка
        // на цельный предмет, а под-стак мы вольём обратно в источник.
        var hotbarTarget = !draggedFromSplit ? FindHotbarSlotUnder(e) : null;
        if (hotbarTarget != null && WeaponHotbar.Instance != null &&
            WeaponHotbar.Instance.IsUserSlot(hotbarTarget.SlotIndex))
        {
            WeaponHotbar.Instance.AssignUserSlot(hotbarTarget.SlotIndex, draggedItem);
            RestoreToOrigin();
            placed = true;
        }

        // 2) Try equipment slot target
        if (!placed && !draggedFromSplit)
        {
            var targetSlot = FindSlotUnder(e);
            if (targetSlot != null)
            {
                if (InventoryManager.Instance != null &&
                    InventoryManager.Instance.TryEquip(targetSlot.SlotType, draggedItem))
                {
                    placed = true;
                }
            }
        }

        // 3) Try grid container target
        GridUI targetGrid = null;
        if (!placed)
        {
            targetGrid = FindGridUnder(e);
            if (targetGrid != null)
            {
                bool haveCursor = TryGetCursorCell(targetGrid, e, out var curCell);
                var  underCursor = haveCursor ? targetGrid.Container.GetItemAt(curCell.x, curCell.y) : null;
                // Курсор над предметом-контейнером (не над самим собой) → вложение, а не раскладка.
                bool overContainer = underCursor != null && underCursor != draggedItem &&
                                     underCursor.nestedContainer != null;

                // 3a) Слияние со стаком того же типа прямо под курсором.
                if (underCursor != null && draggedItem.IsStackable &&
                    underCursor.CanStackWith(draggedItem) && underCursor.FreeStackSpace > 0)
                {
                    underCursor.MergeFrom(draggedItem);
                    if (draggedItem.stackCount > 0) RestoreToOrigin(); // остаток — назад
                    placed = true;
                    InventoryManager.Instance?.NotifyChanged();
                }

                // 3b) Вложение в контейнер под курсором (как в Таркове).
                if (!placed && overContainer &&
                    TryDropIntoContainer(underCursor.nestedContainer, draggedItem))
                {
                    if (draggedItem.nestedContainer != null)
                        InventoryManager.Instance?.RegisterContainer(draggedItem.nestedContainer);
                    placed = true;
                    InventoryManager.Instance?.NotifyChanged();
                }

                // 3c) Обычная установка под якорь. Над контейнером не раскладываем по сетке —
                //     если он не принял (полон/не лезет), предмет вернётся на origin.
                if (!placed && !overContainer && TryGetDropCell(targetGrid, e, out var cell) &&
                    targetGrid.Container.TryPlace(draggedItem, cell))
                {
                    if (draggedItem.nestedContainer != null)
                        InventoryManager.Instance?.RegisterContainer(draggedItem.nestedContainer);
                    placed = true;
                    InventoryManager.Instance?.NotifyChanged();
                }
            }
        }

        if (!placed) RestoreToOrigin();

        // Refresh source UI
        if (draggedUI != null)
        {
            if (draggedUI.SourceGrid != null) draggedUI.SourceGrid.Refresh();
            else if (draggedUI.SourceSlot != null) draggedUI.SourceSlot.Refresh();
        }
        // Refresh targets
        if (targetGrid != null && (draggedUI == null || targetGrid != draggedUI.SourceGrid))
            targetGrid.Refresh();
        if (placed) InventoryUI.Instance?.Refresh();

        DestroyGhost();
        draggedUI        = null;
        draggedItem      = null;
        originContainer  = null;
        originSlot       = null;
        lastPointer      = null;
        draggedFromSplit = false;
        splitSourceItem  = null;
    }

    public void CancelDrag()
    {
        if (draggedItem == null) return;

        hoveredGrid?.ResetCellColors();
        hoveredGrid = null;
        if (hoveredSlot != null) { hoveredSlot.SetHighlight(false, false); hoveredSlot = null; }
        if (hoveredHotbarSlot != null) { hoveredHotbarSlot.SetHighlight(false, false); hoveredHotbarSlot = null; }

        RestoreToOrigin();

        if (draggedUI != null)
        {
            if (draggedUI.SourceGrid != null) draggedUI.SourceGrid.Refresh();
            else if (draggedUI.SourceSlot != null) draggedUI.SourceSlot.Refresh();
        }

        DestroyGhost();
        draggedItem      = null;
        draggedUI        = null;
        originContainer  = null;
        originSlot       = null;
        lastPointer      = null;
        draggedFromSplit = false;
        splitSourceItem  = null;
    }

    private void RestoreToOrigin()
    {
        // Под-стак из split возвращаем слиянием в источник, а не размещением в сетке
        // (нового instance в контейнере нет).
        if (draggedFromSplit && splitSourceItem != null)
        {
            splitSourceItem.stackCount += draggedItem.stackCount;
            draggedItem.stackCount = 0;
            return;
        }

        if (originContainer != null)
        {
            bool restored = originContainer.TryPlace(draggedItem, originPos);
            if (!restored)
            {
                for (int y = 0; y < originContainer.height && !restored; y++)
                    for (int x = 0; x < originContainer.width && !restored; x++)
                        restored = originContainer.TryPlace(draggedItem, new Vector2Int(x, y));
            }
        }
        else if (originSlot != null)
        {
            InventoryManager.Instance?.TryEquip(originSlot.SlotType, draggedItem);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool TryGetDropCell(GridUI grid, PointerEventData e, out Vector2Int cell)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                grid.cellsParent, e.position, e.pressEventCamera, out var local))
        {
            cell = default; return false;
        }

        // Ячейка прямо под курсором — детект меняется ровно на границах сетки,
        // как только курсор переходит в соседнюю клетку.
        Vector2Int cursorCell = grid.PixelToCell(local);
        // Курсор держим у центра предмета: смещаем якорь на половину габарита в ЯЧЕЙКАХ.
        cell = new Vector2Int(
            cursorCell.x - (draggedItem.CurrentWidth  - 1) / 2,
            cursorCell.y - (draggedItem.CurrentHeight - 1) / 2);
        cell.x = Mathf.Clamp(cell.x, 0, grid.Container.width  - draggedItem.CurrentWidth);
        cell.y = Mathf.Clamp(cell.y, 0, grid.Container.height - draggedItem.CurrentHeight);
        return true;
    }

    /// <summary>Положить <paramref name="item"/> внутрь вложенного контейнера: сперва долить
    /// в стаки того же типа, затем — в первую свободную ячейку. Возвращает true, если предмет
    /// полностью поглощён/размещён.</summary>
    private static bool TryDropIntoContainer(GridContainer nested, ItemInstance item)
    {
        if (nested == null || item == null) return false;

        if (item.IsStackable)
        {
            nested.TryStackInto(item);
            if (item.stackCount <= 0) return true;
        }

        for (int y = 0; y < nested.height; y++)
            for (int x = 0; x < nested.width; x++)
                if (nested.TryPlace(item, new Vector2Int(x, y)))
                    return true;

        return false;
    }

    /// <summary>Сырая ячейка прямо под курсором (без смещения на центр предмета).
    /// Нужна для детекта стака-цели при слиянии.</summary>
    private bool TryGetCursorCell(GridUI grid, PointerEventData e, out Vector2Int cell)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                grid.cellsParent, e.position, e.pressEventCamera, out var local))
        {
            cell = default; return false;
        }
        cell = grid.PixelToCell(local);
        return cell.x >= 0 && cell.y >= 0 &&
               cell.x < grid.Container.width && cell.y < grid.Container.height;
    }

    private GridUI FindGridUnder(PointerEventData e)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);
        foreach (var r in results)
        {
            var g = r.gameObject.GetComponentInParent<GridUI>();
            if (g != null) return g;
        }
        return null;
    }

    private EquipmentSlotUI FindSlotUnder(PointerEventData e)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);
        foreach (var r in results)
        {
            var s = r.gameObject.GetComponentInParent<EquipmentSlotUI>();
            if (s != null) return s;
        }
        return null;
    }

    private WeaponHotbarSlotUI FindHotbarSlotUnder(PointerEventData e)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);
        foreach (var r in results)
        {
            var s = r.gameObject.GetComponentInParent<WeaponHotbarSlotUI>();
            if (s != null) return s;
        }
        return null;
    }

    private void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null; ghostRt = null; ghostCG = null;
    }
}
