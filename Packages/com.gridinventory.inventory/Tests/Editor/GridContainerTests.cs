using NUnit.Framework;
using UnityEngine;

public class GridContainerTests
{
    /// <summary>Готовый ItemDefinition без обращения к AssetDatabase — для in-memory тестов.</summary>
    private static ItemDefinition MakeDefinition(int w, int h, bool canRotate = true,
                                                 float weightPerUnit = 0f,
                                                 bool isContainer = false,
                                                 int containerW = 1, int containerH = 1,
                                                 float containerMaxWeight = 0f)
    {
        var d = ScriptableObject.CreateInstance<ItemDefinition>();
        d.itemId = "test-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        d.displayName = d.itemId;
        d.width = w;
        d.height = h;
        d.canRotate = canRotate;
        d.weightPerUnit = weightPerUnit;
        d.maxStackSize = 1;
        d.isContainer = isContainer;
        d.containerWidth = containerW;
        d.containerHeight = containerH;
        d.containerMaxWeight = containerMaxWeight;
        return d;
    }

    // ── Placement ────────────────────────────────────────────────────────────

    [Test]
    public void Place_FitsInsideBounds_Succeeds()
    {
        var c = new GridContainer(4, 3);
        var item = new ItemInstance(MakeDefinition(2, 2));

        Assert.IsTrue(c.TryPlace(item, new Vector2Int(0, 0)));
        Assert.IsTrue(c.TryGetPosition(item, out var pos));
        Assert.AreEqual(new Vector2Int(0, 0), pos);
    }

    [Test]
    public void Place_OutsideBounds_Fails()
    {
        var c = new GridContainer(4, 3);
        var item = new ItemInstance(MakeDefinition(2, 2));

        Assert.IsFalse(c.TryPlace(item, new Vector2Int(3, 2)), "(3,2)+2×2 выходит за границы");
        Assert.IsFalse(c.TryPlace(item, new Vector2Int(-1, 0)), "отрицательная X");
        Assert.IsFalse(c.TryPlace(item, new Vector2Int(0, -1)), "отрицательная Y");
    }

    [Test]
    public void Place_OnOccupiedCell_Fails()
    {
        var c = new GridContainer(4, 3);
        var a = new ItemInstance(MakeDefinition(2, 2));
        var b = new ItemInstance(MakeDefinition(1, 1));

        Assert.IsTrue(c.TryPlace(a, new Vector2Int(0, 0)));
        Assert.IsFalse(c.TryPlace(b, new Vector2Int(1, 1)),
                       "клетка (1,1) занята предметом a");
    }

    [Test]
    public void Place_Replace_SamePosition_Works()
    {
        var c = new GridContainer(4, 3);
        var item = new ItemInstance(MakeDefinition(2, 2));

        Assert.IsTrue(c.TryPlace(item, new Vector2Int(0, 0)));
        // Тот же instance в ту же позицию — не должно ломаться.
        Assert.IsTrue(c.TryPlace(item, new Vector2Int(1, 0)),
                      "перемещение на смежную клетку (с собственной освобождённой ячейкой) допустимо");
        Assert.IsTrue(c.TryGetPosition(item, out var pos));
        Assert.AreEqual(new Vector2Int(1, 0), pos);
    }

    // ── Rotation ─────────────────────────────────────────────────────────────

    [Test]
    public void Rotate_SwapsWidthHeight()
    {
        var item = new ItemInstance(MakeDefinition(2, 3));
        Assert.AreEqual(2, item.CurrentWidth);
        Assert.AreEqual(3, item.CurrentHeight);

        item.Rotate();
        Assert.AreEqual(3, item.CurrentWidth);
        Assert.AreEqual(2, item.CurrentHeight);
    }

    [Test]
    public void Rotate_RespectsCanRotateFlag()
    {
        var item = new ItemInstance(MakeDefinition(2, 3, canRotate: false));
        item.Rotate();
        Assert.IsFalse(item.isRotated, "canRotate=false → поворот не применяется");
    }

    [Test]
    public void Place_AfterRotation_UsesNewBounds()
    {
        var c = new GridContainer(3, 3);
        var item = new ItemInstance(MakeDefinition(1, 3));

        Assert.IsTrue(c.TryPlace(item, new Vector2Int(0, 0)), "1×3 помещается вертикально");
        c.Remove(item);
        item.Rotate(); // теперь 3×1
        Assert.IsTrue(c.TryPlace(item, new Vector2Int(0, 0)), "3×1 помещается горизонтально");
    }

    // ── Nested containers ────────────────────────────────────────────────────

    [Test]
    public void NestedContainer_AutoCreated_WhenIsContainer()
    {
        var item = new ItemInstance(MakeDefinition(1, 1, isContainer: true,
                                                   containerW: 4, containerH: 3));
        Assert.IsNotNull(item.nestedContainer);
        Assert.AreEqual(4, item.nestedContainer.width);
        Assert.AreEqual(3, item.nestedContainer.height);
    }

    [Test]
    public void Place_ContainerInsideItself_Fails()
    {
        var outer = new ItemInstance(MakeDefinition(1, 1, isContainer: true, containerW: 4, containerH: 3));
        Assert.IsFalse(outer.nestedContainer.TryPlace(outer, new Vector2Int(0, 0)),
                       "контейнер не может быть положен в самого себя");
    }

    [Test]
    public void Place_ContainerInsideOwnNested_Fails()
    {
        var outer = new ItemInstance(MakeDefinition(1, 1, isContainer: true, containerW: 4, containerH: 3));
        var inner = new ItemInstance(MakeDefinition(1, 1, isContainer: true, containerW: 2, containerH: 2));

        Assert.IsTrue(outer.nestedContainer.TryPlace(inner, new Vector2Int(0, 0)));
        // Попытка вложить outer внутрь inner создаёт цикл.
        Assert.IsFalse(inner.nestedContainer.TryPlace(outer, new Vector2Int(0, 0)),
                       "цикл outer→inner→outer должен быть заблокирован");
    }

    // ── Weight limit ─────────────────────────────────────────────────────────

    [Test]
    public void WeightLimit_Zero_MeansUnlimited()
    {
        var c = new GridContainer(4, 4, maxW:0f);
        var heavy = new ItemInstance(MakeDefinition(1, 1, weightPerUnit: 1000f));
        Assert.IsTrue(c.TryPlace(heavy, new Vector2Int(0, 0)));
    }

    [Test]
    public void WeightLimit_RejectsOverweight()
    {
        var c = new GridContainer(4, 4, maxW:5f);
        var heavy = new ItemInstance(MakeDefinition(1, 1, weightPerUnit: 10f));
        Assert.IsFalse(c.TryPlace(heavy, new Vector2Int(0, 0)),
                       "10kg > maxWeight 5kg → отказ");
    }

    [Test]
    public void WeightLimit_AccumulatesAcrossItems()
    {
        var c = new GridContainer(4, 4, maxW:5f);
        var a = new ItemInstance(MakeDefinition(1, 1, weightPerUnit: 3f));
        var b = new ItemInstance(MakeDefinition(1, 1, weightPerUnit: 3f));

        Assert.IsTrue(c.TryPlace(a, new Vector2Int(0, 0)));
        Assert.IsFalse(c.TryPlace(b, new Vector2Int(1, 0)),
                       "3+3=6 > 5 → второй предмет не должен влезть");
    }

    // ── Removal ──────────────────────────────────────────────────────────────

    [Test]
    public void Remove_FreesCells()
    {
        var c = new GridContainer(4, 3);
        var a = new ItemInstance(MakeDefinition(2, 2));
        var b = new ItemInstance(MakeDefinition(2, 2));

        Assert.IsTrue(c.TryPlace(a, new Vector2Int(0, 0)));
        Assert.IsFalse(c.TryPlace(b, new Vector2Int(0, 0)));
        c.Remove(a);
        Assert.IsTrue(c.TryPlace(b, new Vector2Int(0, 0)), "после Remove(a) клетки свободны");
    }

    [Test]
    public void GetItemAt_ReturnsPlacedItem()
    {
        var c = new GridContainer(4, 3);
        var item = new ItemInstance(MakeDefinition(2, 2));
        c.TryPlace(item, new Vector2Int(1, 1));

        Assert.AreSame(item, c.GetItemAt(1, 1));
        Assert.AreSame(item, c.GetItemAt(2, 2), "any cell внутри bbox 2×2 указывает на тот же instance");
        Assert.IsNull(c.GetItemAt(0, 0), "пустая клетка");
        Assert.IsNull(c.GetItemAt(-1, 0), "выход за границы → null");
    }
}
