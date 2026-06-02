using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-user overrides поверх <see cref="InventoryLayout"/> SO. Сохраняется в JSON
/// рядом с инвентарём (<see cref="InventoryLayoutPersistence"/>). Применяется в
/// runtime к копии Layout, чтобы не загрязнять SO-ассет.
///
/// Ключи панелей:
///   "Backpack" / "Rig" / "SecureCase" — большие гриды-контейнеры.
///   "Pocket1" .. "Pocket4"            — четыре 1×1-кармана.
/// Equipment-слоты и индикаторы идентифицируются <see cref="EquipmentSlotType"/>.
/// </summary>
[Serializable]
public class InventoryLayoutCustomization
{
    [Serializable]
    public class PanelOverride
    {
        public string  id;
        public Vector2 anchoredPosition;
    }

    [Serializable]
    public class EquipmentOverride
    {
        public EquipmentSlotType type;
        public Vector2           anchoredPosition;
    }

    public bool  cellSizeSet;
    public float cellSize = 50f;

    public bool  slotSizeSet;
    public float slotSize = 54f;

    // Позиция и размер главного окна инвентаря (меняются в режиме EDIT:
    // перетаскивание за заголовок + ресайз за угол). Сохраняются в JSON.
    public bool    windowPosSet;
    public Vector2 windowPos;
    public bool    windowSizeSet;
    public Vector2 windowSize = new(960f, 640f);

    public List<PanelOverride>     panelOverrides     = new();
    public List<EquipmentOverride> equipmentOverrides = new();
    public List<EquipmentOverride> indicatorOverrides = new();

    // ── Write helpers ────────────────────────────────────────────────────────

    public void SetPanel(string id, Vector2 pos)
    {
        if (string.IsNullOrEmpty(id)) return;
        for (int i = 0; i < panelOverrides.Count; i++)
            if (panelOverrides[i].id == id) { panelOverrides[i].anchoredPosition = pos; return; }
        panelOverrides.Add(new PanelOverride { id = id, anchoredPosition = pos });
    }

    /// <summary>
    /// Прочитать сохранённую позицию по ID. Нужен для элементов, у которых нет
    /// собственного поля в <see cref="InventoryLayout"/> (например, 7 индивидуальных
    /// столбиков здоровья). Возвращает true и заполняет <paramref name="pos"/>, если
    /// override существует.
    /// </summary>
    public bool TryGetPanel(string id, out Vector2 pos)
    {
        for (int i = 0; i < panelOverrides.Count; i++)
            if (panelOverrides[i].id == id) { pos = panelOverrides[i].anchoredPosition; return true; }
        pos = default;
        return false;
    }

    public void SetEquipment(EquipmentSlotType type, Vector2 pos)
    {
        for (int i = 0; i < equipmentOverrides.Count; i++)
            if (equipmentOverrides[i].type == type) { equipmentOverrides[i].anchoredPosition = pos; return; }
        equipmentOverrides.Add(new EquipmentOverride { type = type, anchoredPosition = pos });
    }

    public void SetIndicator(EquipmentSlotType type, Vector2 pos)
    {
        for (int i = 0; i < indicatorOverrides.Count; i++)
            if (indicatorOverrides[i].type == type) { indicatorOverrides[i].anchoredPosition = pos; return; }
        indicatorOverrides.Add(new EquipmentOverride { type = type, anchoredPosition = pos });
    }

    public void SetCellSize(float v) { cellSizeSet = true; cellSize = Mathf.Max(8f, v); }
    public void SetSlotSize(float v) { slotSizeSet = true; slotSize = Mathf.Max(16f, v); }

    public void SetWindowPos(Vector2 pos)  { windowPosSet  = true; windowPos  = pos; }
    public void SetWindowSize(Vector2 size) { windowSizeSet = true; windowSize = size; }

    public void ResetAll()
    {
        cellSizeSet = false; slotSizeSet = false;
        cellSize = 50f; slotSize = 54f;
        windowPosSet = false; windowSizeSet = false;
        windowPos = Vector2.zero; windowSize = new Vector2(960f, 640f);
        panelOverrides.Clear();
        equipmentOverrides.Clear();
        indicatorOverrides.Clear();
    }

    // ── Apply ────────────────────────────────────────────────────────────────

    /// <summary>Записывает overrides поверх рантайм-копии <see cref="InventoryLayout"/>.</summary>
    public void ApplyTo(InventoryLayout layout)
    {
        if (layout == null) return;

        if (cellSizeSet)   layout.cellSize   = cellSize;
        if (slotSizeSet)   layout.slotSize   = slotSize;
        if (windowPosSet)  layout.windowPos  = windowPos;
        if (windowSizeSet) layout.windowSize = windowSize;

        foreach (var p in panelOverrides)
        {
            switch (p.id)
            {
                case "Backpack":   layout.backpackPanelPos   = p.anchoredPosition; break;
                case "Rig":        layout.rigPanelPos        = p.anchoredPosition; break;
                case "SecureCase": layout.secureCasePanelPos = p.anchoredPosition; break;
                case "Pocket1":    layout.pocket1Pos         = p.anchoredPosition; break;
                case "Pocket2":    layout.pocket2Pos         = p.anchoredPosition; break;
                case "Pocket3":    layout.pocket3Pos         = p.anchoredPosition; break;
                case "Pocket4":    layout.pocket4Pos         = p.anchoredPosition; break;
                case "Hunger":     layout.hungerBarPos       = p.anchoredPosition; break;
                case "Thirst":     layout.thirstBarPos       = p.anchoredPosition; break;
                // "HealthLabel" — позиция надписи «ЗДОРОВЬЕ». Также служит anchor'ом
                // для дефолтной row-раскладки 7 столбиков (если у них нет своих
                // override-позиций). По сути это «якорь группы здоровья».
                case "HealthLabel": layout.healthPanelPos    = p.anchoredPosition; break;
                // "Health_<Part>" — индивидуальные позиции столбиков здоровья.
                // В Layout нет соответствующих полей: InventoryUI читает их через
                // TryGetPanel при BuildHealthPanel. Здесь — no-op.
            }
        }

        foreach (var eo in equipmentOverrides)
            for (int i = 0; i < layout.equipmentSlots.Count; i++)
                if (layout.equipmentSlots[i] != null && layout.equipmentSlots[i].type == eo.type)
                    layout.equipmentSlots[i].anchoredPosition = eo.anchoredPosition;

        foreach (var io in indicatorOverrides)
        {
            if (io.type == EquipmentSlotType.Backpack   && layout.backpackSlotIndicator   != null)
                layout.backpackSlotIndicator.anchoredPosition   = io.anchoredPosition;
            else if (io.type == EquipmentSlotType.Rig        && layout.rigSlotIndicator        != null)
                layout.rigSlotIndicator.anchoredPosition        = io.anchoredPosition;
            else if (io.type == EquipmentSlotType.SecureCase && layout.secureCaseSlotIndicator != null)
                layout.secureCaseSlotIndicator.anchoredPosition = io.anchoredPosition;
        }
    }
}
