using System.Collections.Generic;

/// <summary>
/// Минимальная поверхность inventory-фасада, нужная плагин-коду
/// (например, <see cref="WorldItemBase"/> при подборе предмета).
/// Реализуется на стороне проекта (обычно — InventoryManager).
/// Глобальный доступ — через <see cref="Inventory.Service"/>.
/// </summary>
public interface IInventoryService
{
    /// <summary>Регистрирует контейнер и все вложенные контейнеры в реестре, чтобы их можно было найти по containerId.</summary>
    void RegisterContainerRecursive(GridContainer c);

    /// <summary>Пробует надеть предмет в первый свободный подходящий equipment-слот. Возвращает true при успехе.</summary>
    bool TryEquipAnyMatchingSlot(ItemInstance item);

    /// <summary>Порядок приоритета подбора: карманы → разгрузка → рюкзак. Используется при автоматическом подборе предмета.</summary>
    IEnumerable<GridContainer> GetPickupContainers();

    /// <summary>Триггер события OnInventoryChanged — UI и хотбар обновятся.</summary>
    void NotifyChanged();

    /// <summary>Открыть окно стороннего контейнера (мировой ящик/сундук) рядом с инвентарём игрока.
    /// Регистрирует контейнер, открывает панель инвентаря (если закрыта) и показывает окно содержимого.</summary>
    void OpenContainerWindow(GridContainer container, string title);
}
