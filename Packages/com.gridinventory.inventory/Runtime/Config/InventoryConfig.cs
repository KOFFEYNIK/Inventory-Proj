using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Описывает структуру инвентаря: какие карманы и какие слоты экипировки доступны.
/// Назначается в инспекторе <see cref="InventoryManager"/>. Если поле не назначено —
/// используется встроенный Tarkov-like дефолт (4 кармана 1×1 + 9 equipment-слотов).
///
/// Примеры пресетов (создавать через CreateAssetMenu → Inventory/Inventory Config):
///   - Tarkov-like:    4 кармана 1×1, все 9 equipment-слотов.
///   - Diablo-like:    0 карманов, только Backpack + BodyArmor + Helmet + 2 Weapon.
///   - Minecraft-like: 0 карманов, только Hotbar (равно set 9 экипировочных-сетки 1×1).
/// </summary>
[CreateAssetMenu(fileName = "InventoryConfig", menuName = "Inventory/Inventory Config")]
public class InventoryConfig : ScriptableObject
{
    [Header("Pockets")]
    [Tooltip("Список карманов (отдельные мелкие сетки рядом с инвентарём). Пустой массив = карманов нет.")]
    public List<PocketEntry> pockets = new();

    [Header("Equipment Slots")]
    [Tooltip("Слоты экипировки. Каждый принимает только указанный ItemType (через EquipmentSlot.Accepts). " +
             "Пустой массив = экипировки нет.")]
    public List<EquipmentSlotEntry> equipment = new();

    [Header("Hotbar")]
    [Tooltip("Конфигурация хотбара (слоты + горячие клавиши). Если slots пусто — WeaponHotbar возьмёт дефолт.")]
    public HotbarConfig hotbar = new();

    /// <summary>Tarkov-like дефолт. Используется, если InventoryManager.config == null.</summary>
    public static InventoryConfig CreateDefault()
    {
        var c = ScriptableObject.CreateInstance<InventoryConfig>();
        c.hotbar = HotbarConfig.CreateDefault();
        c.pockets = new List<PocketEntry>
        {
            new() { id = "Pocket1", width = 1, height = 1 },
            new() { id = "Pocket2", width = 1, height = 1 },
            new() { id = "Pocket3", width = 1, height = 1 },
            new() { id = "Pocket4", width = 1, height = 1 },
        };
        c.equipment = new List<EquipmentSlotEntry>
        {
            new() { type = EquipmentSlotType.Backpack,        accepts = ItemType.Backpack },
            new() { type = EquipmentSlotType.Rig,             accepts = ItemType.Rig },
            new() { type = EquipmentSlotType.SecureCase,      accepts = ItemType.SecureCase },
            new() { type = EquipmentSlotType.BodyArmor,       accepts = ItemType.BodyArmor },
            new() { type = EquipmentSlotType.Helmet,          accepts = ItemType.Helmet },
            new() { type = EquipmentSlotType.FaceCover,       accepts = ItemType.FaceCover },
            new() { type = EquipmentSlotType.PrimaryWeapon,   accepts = ItemType.PrimaryWeapon },
            new() { type = EquipmentSlotType.SecondaryWeapon, accepts = ItemType.SecondaryWeapon },
            new() { type = EquipmentSlotType.Holster,         accepts = ItemType.Sidearm },
        };
        return c;
    }
}

[Serializable]
public class PocketEntry
{
    [Tooltip("Уникальный id кармана (используется при сохранении / загрузке). Например 'Pocket1'.")]
    public string id = "Pocket1";

    [Min(1)] public int   width     = 1;
    [Min(1)] public int   height    = 1;
    [Min(0f)] public float maxWeight = 0f; // 0 = без лимита
}

[Serializable]
public class EquipmentSlotEntry
{
    public EquipmentSlotType type;
    public ItemType          accepts;
}
