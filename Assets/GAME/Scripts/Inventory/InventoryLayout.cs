using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryLayout", menuName = "Inventory/Inventory Layout")]
public class InventoryLayout : ScriptableObject
{
    [Header("Window")]
    public Vector2 windowSize       = new(960f, 640f);
    public float   titleBarHeight   = 30f;

    [Header("Cells")]
    [Tooltip("Pixel size of a single grid cell.")]
    public float cellSize = 50f;
    [Tooltip("Gap between adjacent cells.")]
    public float cellGap  = 2f;
    [Tooltip("Visual size of single equipment slot (1×1).")]
    public float slotSize = 54f;

    [Header("Backpack Panel")]
    public bool     backpackEnabled = true;
    [Tooltip("Top-left anchored position of the backpack grid (relative to window top-left). Y is negative.")]
    public Vector2  backpackPanelPos = new(280f, -50f);

    [Header("Rig Panel")]
    public bool     rigEnabled = true;
    public Vector2  rigPanelPos = new(8f, -50f);

    [Header("Secure Case Panel")]
    public bool     secureCaseEnabled = true;
    public Vector2  secureCasePanelPos = new(8f, -250f);

    [Header("Pockets (4 separate 1×1 slots)")]
    public bool    pocketsEnabled = true;
    public Vector2 pocket1Pos = new(8f, -450f);
    public Vector2 pocket2Pos = new(66f, -450f);
    public Vector2 pocket3Pos = new(124f, -450f);
    public Vector2 pocket4Pos = new(182f, -450f);

    [Header("Equipment Slots")]
    [Tooltip("Per-slot anchored positions. Add/edit entries to place each slot freely.")]
    public List<EquipmentSlotLayout> equipmentSlots = new()
    {
        new EquipmentSlotLayout { type = EquipmentSlotType.Helmet,          enabled = true, anchoredPosition = new Vector2(800f, -50f)  },
        new EquipmentSlotLayout { type = EquipmentSlotType.FaceCover,       enabled = true, anchoredPosition = new Vector2(800f, -110f) },
        new EquipmentSlotLayout { type = EquipmentSlotType.BodyArmor,       enabled = true, anchoredPosition = new Vector2(800f, -170f) },
        new EquipmentSlotLayout { type = EquipmentSlotType.PrimaryWeapon,   enabled = true, anchoredPosition = new Vector2(800f, -230f) },
        new EquipmentSlotLayout { type = EquipmentSlotType.SecondaryWeapon, enabled = true, anchoredPosition = new Vector2(800f, -290f) },
        new EquipmentSlotLayout { type = EquipmentSlotType.Holster,         enabled = true, anchoredPosition = new Vector2(800f, -350f) }
    };

    [Header("Container Slot Indicators (1×1 slot showing the equipped backpack/rig/case item icon)")]
    [Tooltip("Per-slot anchored position for the small icon showing the equipped backpack item. Optional — disable if not needed.")]
    public EquipmentSlotLayout backpackSlotIndicator = new()
    {
        type = EquipmentSlotType.Backpack,
        enabled = true,
        anchoredPosition = new Vector2(700f, -50f)
    };
    public EquipmentSlotLayout rigSlotIndicator = new()
    {
        type = EquipmentSlotType.Rig,
        enabled = true,
        anchoredPosition = new Vector2(700f, -110f)
    };
    public EquipmentSlotLayout secureCaseSlotIndicator = new()
    {
        type = EquipmentSlotType.SecureCase,
        enabled = true,
        anchoredPosition = new Vector2(700f, -170f)
    };

    [Header("Bottom Buttons")]
    public Vector2 saveButtonPos = new(8f, 4f);
    public Vector2 loadButtonPos = new(104f, 4f);
    public Vector2 buttonSize    = new(90f, 26f);

    [Header("Vitals bars (in inventory window)")]
    public bool    vitalsBarsEnabled = true;
    [Tooltip("Позиция полосы голода (жёлтая) — anchored от верхнего-левого угла окна.")]
    public Vector2 hungerBarPos      = new(240f, -580f);
    [Tooltip("Позиция полосы жажды (синяя).")]
    public Vector2 thirstBarPos      = new(240f, -606f);
    public Vector2 vitalsBarSize     = new(300f, 22f);

    [Header("Health panel (in inventory window)")]
    public bool    healthPanelEnabled = true;
    [Tooltip("Позиция левого верхнего угла блока полосок здоровья (anchored от верхнего-левого угла окна). " +
             "Дефолт — нижний правый угол окна 960×640. Блок ≈ 200×160 (ярлык + 7 полосок).")]
    public Vector2 healthPanelPos     = new(750f, -488f);
    public Vector2 healthBarSize      = new(200f, 18f);
    public float   healthBarGap       = 3f;
}

[Serializable]
public class EquipmentSlotLayout
{
    public EquipmentSlotType type;
    public bool              enabled = true;
    public Vector2           anchoredPosition;
}
