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
        }
    }

    // ── Begin ─────────────────────────────────────────────────────────────────

    public void BeginDrag(ItemUI ui, PointerEventData e)
    {
        draggedUI       = ui;
        draggedItem     = ui.Item;
        originContainer = ui.SourceContainer;
        originSlot      = ui.SourceSlot;

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
        ui.GetComponent<Image>().color = Color.clear;
        var label = ui.GetComponentInChildren<Text>();
        if (label != null) label.enabled = false;

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
        img.sprite = draggedItem.definition.icon;
        img.color  = new Color(0.55f, 0.85f, 1f, 1f);

        ResizeGhost();
        MoveGhost(screenPos);
    }

    private void ResizeGhost()
    {
        float w = draggedItem.CurrentWidth  * (GridUI.CellSize + GridUI.CellGap) - GridUI.CellGap;
        float h = draggedItem.CurrentHeight * (GridUI.CellSize + GridUI.CellGap) - GridUI.CellGap;
        ghostRt.sizeDelta = new Vector2(w, h);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public void UpdateDrag(PointerEventData e)
    {
        if (ghost == null) return;
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
        var hotbarTarget = FindHotbarSlotUnder(e);
        if (hotbarTarget != null && WeaponHotbar.Instance != null &&
            WeaponHotbar.Instance.IsUserSlot(hotbarTarget.SlotIndex))
        {
            WeaponHotbar.Instance.AssignUserSlot(hotbarTarget.SlotIndex, draggedItem);
            RestoreToOrigin();
            placed = true;
        }

        // 2) Try equipment slot target
        if (!placed)
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
            if (targetGrid != null && TryGetDropCell(targetGrid, e, out var cell))
            {
                if (targetGrid.Container.TryPlace(draggedItem, cell))
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
        draggedUI       = null;
        draggedItem     = null;
        originContainer = null;
        originSlot      = null;
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
        draggedItem     = null;
        draggedUI       = null;
        originContainer = null;
        originSlot      = null;
    }

    private void RestoreToOrigin()
    {
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

        float offX = draggedItem.CurrentWidth  * (GridUI.CellSize + GridUI.CellGap) * 0.5f;
        float offY = draggedItem.CurrentHeight * (GridUI.CellSize + GridUI.CellGap) * 0.5f;
        cell = grid.PixelToCell(new Vector2(local.x - offX, local.y - offY));
        cell.x = Mathf.Clamp(cell.x, 0, grid.Container.width  - draggedItem.CurrentWidth);
        cell.y = Mathf.Clamp(cell.y, 0, grid.Container.height - draggedItem.CurrentHeight);
        return true;
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
