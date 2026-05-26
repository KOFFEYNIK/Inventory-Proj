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
    [Tooltip("Все слоты хотбара по порядку. Клавиши активации привязаны индексу: 0→'1', 1→'2', …, 9→'0'. " +
             "Слотов больше 10 — клавиши доступны только через программный SelectSlot().")]
    public List<HotbarSlotEntry> slots = new();

    [Tooltip("Клавиша быстрого возврата на reservedHotkeyTarget (например, H → нож).")]
    public KeyCode reservedHotkey = KeyCode.H;

    [Tooltip("Индекс слота, который активируется при нажатии reservedHotkey.")]
    public int reservedHotkeyTarget = 0;

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
