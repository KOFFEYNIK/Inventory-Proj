using System;

public enum EquipmentSlotType
{
    Backpack = 0,
    Rig,
    SecureCase,
    BodyArmor,
    Helmet,
    FaceCover,
    PrimaryWeapon,
    SecondaryWeapon,
    Holster
}

[Serializable]
public class EquipmentSlot
{
    public EquipmentSlotType SlotType;
    public ItemType AcceptedItemType;
    public ItemInstance EquippedItem;

    public EquipmentSlot(EquipmentSlotType slotType, ItemType accepted)
    {
        SlotType = slotType;
        AcceptedItemType = accepted;
    }

    public bool IsEmpty => EquippedItem == null;

    public bool Accepts(ItemInstance item)
    {
        if (item == null || item.definition == null) return false;
        var type = item.definition.itemType;
        if (type == AcceptedItemType) return true;

        // Дополнительный слот для оружия принимает и основное оружие — игрок может
        // носить две винтовки (например, для разных дистанций). Holster по-прежнему
        // фильтрует только пистолеты — туда длинноствол не лезет.
        if (SlotType == EquipmentSlotType.SecondaryWeapon && type == ItemType.PrimaryWeapon)
            return true;

        return false;
    }
}
