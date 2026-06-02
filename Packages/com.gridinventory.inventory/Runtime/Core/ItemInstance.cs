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

    // ── Стакирование ───────────────────────────────────────────────────────────

    /// <summary>Предельный размер стака для этого предмета (из определения).</summary>
    public int MaxStack => definition != null ? definition.MaxStack : 1;

    /// <summary>Сколько ещё единиц можно добавить в этот стак.</summary>
    public int FreeStackSpace => Math.Max(0, MaxStack - stackCount);

    /// <summary>Может ли предмет в принципе стакаться. Контейнеры (с вложенным содержимым)
    /// не стакаются — каждый экземпляр уникален.</summary>
    public bool IsStackable => definition != null && definition.canStack && nestedContainer == null;

    /// <summary>Можно ли долить <paramref name="other"/> в этот стак: оба стакаемы и одного типа.</summary>
    public bool CanStackWith(ItemInstance other) =>
        other != null && other != this && IsStackable && other.IsStackable &&
        other.definition == definition;

    /// <summary>Перелить как можно больше единиц из <paramref name="other"/> в этот стак.
    /// Мутирует <see cref="stackCount"/> у обоих. Возвращает число перенесённых единиц.</summary>
    public int MergeFrom(ItemInstance other)
    {
        if (!CanStackWith(other)) return 0;
        int move = Math.Min(FreeStackSpace, other.stackCount);
        stackCount       += move;
        other.stackCount -= move;
        return move;
    }

    public ItemInstance() { }

    public ItemInstance(ItemDefinition def, int count = 1)
    {
        instanceId = Guid.NewGuid().ToString();
        definition  = def;
        stackCount  = Math.Clamp(count, 1, def.MaxStack);

        if (def.isContainer)
            nestedContainer = new GridContainer(def.containerWidth, def.containerHeight, def.containerMaxWeight);
    }

    public void Rotate()
    {
        if (definition.canRotate) isRotated = !isRotated;
    }
}
