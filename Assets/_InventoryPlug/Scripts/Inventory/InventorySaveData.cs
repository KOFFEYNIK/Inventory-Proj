using System;
using System.Collections.Generic;

[Serializable]
public class InventorySaveData
{
    public List<ContainerSaveData>      containers     = new();
    public List<EquipmentSlotSaveData>  equipmentSlots = new();
}

[Serializable]
public class ContainerSaveData
{
    public string            containerId;
    public int               width;
    public int               height;
    public float             maxWeight;
    public List<ItemSaveData> items = new();
}

[Serializable]
public class ItemSaveData
{
    public string instanceId;
    public string definitionId;
    public int    stackCount;
    public bool   isRotated;
    public int    posX;
    public int    posY;
    public string nestedContainerId;
}

[Serializable]
public class EquipmentSlotSaveData
{
    public int          slotType;
    public string       equippedInstanceId;
    public ItemSaveData equippedItem;
}
