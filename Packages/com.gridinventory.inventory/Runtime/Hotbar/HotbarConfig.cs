using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Конфигурация хотбара (часть <see cref="InventoryConfig"/>).
/// Описывает количество и тип каждого слота, а также горячую клавишу быстрого возврата
/// на «зарезервированный» слот (например, на нож).
///
/// Дефолт-значения подобраны под Tarkov-like: 10 слотов, нож на 0, 3 equipment-привязанных слота
/// (PrimaryWeapon / SecondaryWeapon / Holster), остальные 6 — пользовательские.
/// </summary>
[Serializable]
public class HotbarConfig
{
    [Tooltip("Глобальный тогл. Если false — хотбар отключён: цифровые клавиши не активны, " +
             "UI-панель внизу экрана не строится.")]
    public bool enabled = true;

    [Tooltip("Желаемое количество слотов. 0 = использовать длину списка <slots>. " +
             "Иначе список автоматически подрезается / расширяется до этого размера " +
             "(новые слоты добавляются как UserItem). Цифровые клавиши закреплены за индексами " +
             "0..9 (1, 2, … 9, 0); слоты >9 доступны только через программный SelectSlot.")]
    [Min(0)] public int slotCount = 0;

    [Tooltip("Все слоты хотбара по порядку. Клавиши активации привязаны индексу: 0→'1', 1→'2', …, 9→'0'. " +
             "Слотов больше 10 — клавиши доступны только через программный SelectSlot().")]
    public List<HotbarSlotEntry> slots = new();

    [Tooltip("Клавиша быстрого возврата на reservedHotkeyTarget (например, H → нож).")]
    public KeyCode reservedHotkey = KeyCode.H;

    [Tooltip("Индекс слота, который активируется при нажатии reservedHotkey.")]
    public int reservedHotkeyTarget = 0;

    /// <summary>Подгоняет список slots до <see cref="slotCount"/>, если он &gt; 0.
    /// Слот, добавляемый «лишним», создаётся как UserItem. Лишние срезаются.</summary>
    public void ApplySlotCountOverride()
    {
        if (slotCount <= 0) return;
        while (slots.Count < slotCount) slots.Add(new HotbarSlotEntry { kind = HotbarSlotKind.UserItem });
        if (slots.Count > slotCount) slots.RemoveRange(slotCount, slots.Count - slotCount);
    }

    /// <summary>Tarkov-like дефолт хотбара. Используется, если InventoryConfig.hotbar пуст.</summary>
    public static HotbarConfig CreateDefault()
    {
        return new HotbarConfig
        {
            slots = new List<HotbarSlotEntry>
            {
                new() { kind = HotbarSlotKind.Reserved },
                new() { kind = HotbarSlotKind.EquipmentWeapon, equipmentSource = EquipmentSlotType.PrimaryWeapon },
                new() { kind = HotbarSlotKind.EquipmentWeapon, equipmentSource = EquipmentSlotType.SecondaryWeapon },
                new() { kind = HotbarSlotKind.EquipmentWeapon, equipmentSource = EquipmentSlotType.Holster },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
            },
            reservedHotkey       = KeyCode.H,
            reservedHotkeyTarget = 0,
        };
    }
}

[Serializable]
public class HotbarSlotEntry
{
    public HotbarSlotKind kind = HotbarSlotKind.UserItem;

    [Tooltip("Только для EquipmentWeapon: какой equipment-слот зеркалит этот слот хотбара.")]
    public EquipmentSlotType equipmentSource;

    [Tooltip("Только для Reserved: префаб, который активируется в этом слоте (например, нож). " +
             "Если null — слот неактивен (Reserved-слот не показывает ItemInstance иконку).")]
    public GameObject reservedPrefab;
}
