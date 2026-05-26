/// <summary>
/// Глобальная точка доступа к зарегистрированному <see cref="IInventoryService"/>.
/// Реализация (обычно InventoryManager) регистрирует себя в Awake:
/// <code>Inventory.Service = this;</code>
/// Плагин-код (WorldItemBase и т.п.) обращается через этот статический доступ.
/// </summary>
public static class Inventory
{
    public static IInventoryService Service { get; set; }
}
