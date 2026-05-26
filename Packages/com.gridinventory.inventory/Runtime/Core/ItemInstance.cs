using System;

[Serializable]
public class ItemInstance
{
    public string instanceId;
    public ItemDefinition definition;
    public int stackCount = 1;
    public bool isRotated;
    public GridContainer nestedContainer;

    public int CurrentWidth  => isRotated ? definition.height : definition.width;
    public int CurrentHeight => isRotated ? definition.width  : definition.height;

    public float TotalWeight =>
        definition.weightPerUnit * stackCount + (nestedContainer?.TotalWeight ?? 0f);

    public ItemInstance() { }

    public ItemInstance(ItemDefinition def, int count = 1)
    {
        instanceId = Guid.NewGuid().ToString();
        definition  = def;
        stackCount  = Math.Clamp(count, 1, def.maxStackSize);

        if (def.isContainer)
            nestedContainer = new GridContainer(def.containerWidth, def.containerHeight, def.containerMaxWeight);
    }

    public void Rotate()
    {
        if (definition.canRotate) isRotated = !isRotated;
    }
}
